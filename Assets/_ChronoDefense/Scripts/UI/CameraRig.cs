// Câmera tática (Bíblia §3.2): perspectiva FOV 32°, rotação fixa (42°, 0°, 0°), sem rotação pelo jogador.
// Pan: 1 dedo / WASD / setas / botão direito ou do meio. Zoom: pinça / roda do mouse (aproxima até 2×, afasta 15%).
// Enquadra LevelLayoutSO.CameraBounds na tela; com o mapa inteiro visível o pan fica travado no centro.
// Toques sem arrasto viram "tap" para o TowerPlacer.
// Zero-GC: Touch.activeTouches é um ReadOnlyArray (struct) e não há Camera.main no loop.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ChronoDefense.UI
{
    public sealed class CameraRig : MonoBehaviour
    {
        // Referencia_BTD6.md §2: câmera quase vertical, sem horizonte; o mapa ocupa a tela toda.
        // (Antes: FOV 32°, X 42°, com céu — Bíblia §3.2 v2.0.) FOV baixo = pouca distorção entre perto e longe.
        public const float Fov = 26f;
        // 26/11: 65° → 55° para ver a frente dos personagens (rosto, arma) sem zoom, com o mapa inteiro na tela.
        public const float Pitch = 55f;
        // Distância = base × zoom. Com o mapa inteiro na tela as unidades ficam pequenas (~55 m de largura),
        // então a aproximação vai até 0,5 (2×); o afastamento continua em +15%.
        public const float ZoomMin = 0.5f;
        public const float ZoomMax = 1.15f;
        public const float TapSlopPixels = 14f;
        public const float KeyPanSpeed = 14f;          // m/s

        /// <summary>Fração da largura da tela ocupada pela loja à direita (HudController usa a mesma).</summary>
        public const float ShopFraction = 0.14f;

        /// <summary>
        /// Região da tela (viewport 0..1) onde a área jogável precisa caber. Como no BTD6, vidas, ouro e onda
        /// ficam por cima do mapa (sem faixas reservadas); só a coluna da loja à direita fica de fora.
        /// </summary>
        public static readonly Rect SafeViewport = Rect.MinMaxRect(0f, 0f, 1f - ShopFraction, 1f);

        public Camera Cam { get; private set; }
        public float BaseDistance => _baseDistance;
        public float Zoom => _zoom;
        public Vector3 Focus => _focus;
        /// <summary>Foco que centraliza o CameraBounds na região segura (posição travada do pan).</summary>
        public Vector3 FittedFocus => _center;
        /// <summary>Com zoom ≥ 1 o mapa inteiro cabe na tela: pan travado no centro.</summary>
        public bool PanLocked => _zoom >= 1f - 1e-4f;
        public TowerPlacer Placer;
        public bool InputEnabled = true;

        Bounds _bounds;
        Vector3 _focus, _center;
        float _baseDistance;
        float _zoom = 1f;
        float _fittedAspect;
        readonly Vector3[] _corners = new Vector3[8];

        // gesto
        bool _pointerDown, _dragging;
        Vector2 _downPos, _lastPos;
        int _touchCount;
        float _lastPinch;

        public void Init(Camera cam, Bounds bounds)
        {
            Cam = cam;
            _bounds = bounds;
            cam.orthographic = false;
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            for (int i = 0; i < 8; i++)
                _corners[i] = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                                          (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                                          (i & 4) == 0 ? bounds.min.z : bounds.max.z);
            if (Application.isPlaying && !EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            Refit();
        }

        /// <summary>
        /// Enquadra o CameraBounds na SafeViewport. A câmera é em perspectiva (FOV 32°, X 42°, Bíblia §3.2),
        /// então em vez de "orthographic size" calcula-se a menor distância em que os 8 cantos cabem,
        /// com o foco ajustado para centralizar a projeção (o chão inclinado projeta assimétrico).
        /// Chamado também quando o aspecto da tela muda (rotação, redimensionamento).
        /// </summary>
        public void Refit()
        {
            _fittedAspect = Cam.aspect;
            float lo = 1f, hi = 500f;
            Vector3 focus = new Vector3(_bounds.center.x, 0f, _bounds.center.z);
            for (int it = 0; it < 32; it++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 f = CenterFocus(mid);
                if (Fits(mid, f)) { hi = mid; focus = f; } else lo = mid;
            }
            _baseDistance = hi;
            _center = CenterFocus(hi);
            _focus = _center;
            _zoom = 1f;
            Apply();
        }

        void Place(Vector3 focus, float distance)
        {
            Transform t = Cam.transform;
            t.position = focus - t.forward * distance;
        }

        /// <summary>Viewport min/max dos 8 cantos com a câmera em (foco, distância).</summary>
        bool Project(Vector3 focus, float distance, out Vector2 min, out Vector2 max)
        {
            Place(focus, distance);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 v = Cam.WorldToViewportPoint(_corners[i]);
                if (v.z <= Cam.nearClipPlane) return false;
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return true;
        }

        bool Fits(float distance, Vector3 focus) =>
            Project(focus, distance, out Vector2 min, out Vector2 max)
            && min.x >= SafeViewport.xMin && max.x <= SafeViewport.xMax
            && min.y >= SafeViewport.yMin && max.y <= SafeViewport.yMax;

        /// <summary>Desloca o foco até o centro da projeção coincidir com o centro da SafeViewport.</summary>
        Vector3 CenterFocus(float distance)
        {
            Vector3 f = new Vector3(_bounds.center.x, 0f, _bounds.center.z);
            Vector2 target = SafeViewport.center;
            for (int it = 0; it < 8; it++)
            {
                if (!Project(f, distance, out Vector2 min, out Vector2 max)) break;
                Vector2 mid = (min + max) * 0.5f;
                float spanX = Mathf.Max(0.01f, max.x - min.x), spanY = Mathf.Max(0.01f, max.y - min.y);
                f.x += (mid.x - target.x) * _bounds.size.x / spanX;
                f.z += (mid.y - target.y) * _bounds.size.z / spanY;
            }
            return f;
        }

        /// <summary>Pan em metros no plano XZ (respeita a trava).</summary>
        public void PanBy(Vector3 worldDelta)
        {
            _focus += worldDelta;
            Apply();
        }

        public void SetZoom(float zoom)
        {
            _zoom = zoom;
            Apply();
        }

        void Update()
        {
            if (Cam == null || !InputEnabled) return;
            if (!Mathf.Approximately(Cam.aspect, _fittedAspect)) Refit();
            float dt = Time.unscaledDeltaTime;

            // Teclado
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.z -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 0f) _focus += move.normalized * KeyPanSpeed * dt;
            }

            var touches = ETouch.activeTouches;
            if (touches.Count > 0) HandleTouches(touches);
            else HandleMouse();

            Apply();
        }

        void HandleTouches(UnityEngine.InputSystem.Utilities.ReadOnlyArray<ETouch> touches)
        {
            if (touches.Count >= 2)
            {
                float pinch = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                if (_touchCount < 2) _lastPinch = pinch;
                else if (_lastPinch > 1f) _zoom *= _lastPinch / Mathf.Max(1f, pinch);
                _lastPinch = pinch;
                _touchCount = 2;
                _dragging = true;   // um gesto de pinça nunca vira tap
                return;
            }
            ETouch t = touches[0];
            switch (t.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    BeginPointer(t.screenPosition, IsOverUi(t.touchId));
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    MovePointer(t.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    EndPointer(t.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    _pointerDown = false;
                    break;
            }
            _touchCount = 1;
        }

        void HandleMouse()
        {
            _touchCount = 0;
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(-1)) _zoom *= scroll > 0f ? 0.95f : 1.05f;

            // Botão direito/meio: pan direto
            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            {
                if (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame) _lastPos = pos;
                PanByScreen(_lastPos, pos);
                _lastPos = pos;
                return;
            }

            // Botão esquerdo: arrastar = pan, clicar = tap
            if (mouse.leftButton.wasPressedThisFrame) BeginPointer(pos, IsOverUi(-1));
            else if (mouse.leftButton.isPressed) MovePointer(pos);
            else if (mouse.leftButton.wasReleasedThisFrame) EndPointer(pos);
        }

        void BeginPointer(Vector2 pos, bool overUi)
        {
            _pointerDown = !overUi;
            _dragging = false;
            _downPos = _lastPos = pos;
        }

        void MovePointer(Vector2 pos)
        {
            if (!_pointerDown) return;
            if (!_dragging && (pos - _downPos).sqrMagnitude > TapSlopPixels * TapSlopPixels) _dragging = true;
            if (_dragging) PanByScreen(_lastPos, pos);
            _lastPos = pos;
        }

        void EndPointer(Vector2 pos)
        {
            if (_pointerDown && !_dragging && Placer != null) Placer.HandleTap(pos);
            _pointerDown = false;
            _dragging = false;
        }

        static bool IsOverUi(int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }

        void PanByScreen(Vector2 from, Vector2 to)
        {
            if (ScreenToGround(from, out Vector3 a) && ScreenToGround(to, out Vector3 b))
                _focus += a - b;
        }

        /// <summary>GDD §12.3: raio da câmera × plano y = 0 (sem física).</summary>
        public bool ScreenToGround(Vector2 screen, out Vector3 point)
        {
            Ray ray = Cam.ScreenPointToRay(screen);
            point = default;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }

        void Apply()
        {
            _zoom = Mathf.Clamp(_zoom, ZoomMin, ZoomMax);
            // Trava inteligente: com zoom ≥ 1 o mapa cabe inteiro → foco fixo no centro.
            // Com zoom de aproximação, libera só o que ficou fora da tela (≈ extents × (1 − zoom)).
            float slack = Mathf.Max(0f, 1f - _zoom);
            float rx = _bounds.extents.x * slack, rz = _bounds.extents.z * slack;
            _focus.x = Mathf.Clamp(_focus.x, _center.x - rx, _center.x + rx);
            _focus.z = Mathf.Clamp(_focus.z, _center.z - rz, _center.z + rz);
            _focus.y = 0f;
            Transform t = Cam.transform;
            t.position = _focus - t.forward * (_baseDistance * _zoom);
        }
    }
}
