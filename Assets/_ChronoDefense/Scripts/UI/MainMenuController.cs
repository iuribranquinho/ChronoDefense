// Menu inicial (cena MainMenu). Com a arte pintada do usuário (Resources/Sprites/UI/Menu/MENU_E1_Main): tela inteira
// com toque nas pedras das fases, painel da fase (nome, miniatura, estrelas) e botões pintados. Sem ela: mapa da saga
// antigo ou cartões. Abaixo, o menu de cartões: título, cartões das fases do capítulo com miniatura do mapa, nome, estrelas
// e selo de chefe; a fase de teste "Prado" em separado. Tocar num cartão abre a cena Gameplay com a fase.
// Construído em código (uGUI + TMP), no mesmo estilo do HUD (madeira, verde, contorno escuro). Não é hot loop.
using ChronoDefense.Core;
using ChronoDefense.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChronoDefense.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public CampaignSO Campaign;
        [Tooltip("Fase de teste visual (LVL_TEST_Prado); opcional")]
        public LevelDataSO TestLevel;
        [Tooltip("Era mostrada (1..5)")]
        public int Era = 1;

        static readonly Color Wood = new Color32(139, 90, 43, 255), WoodDark = new Color32(92, 56, 24, 255);
        static readonly Color Green = new Color32(76, 187, 23, 255), Red = new Color32(214, 64, 52, 255);
        static readonly Color Yellow = new Color32(255, 196, 30, 255), Ink = new Color32(18, 14, 10, 255);
        static readonly Color StarOff = new Color32(70, 55, 40, 255);

        TMP_FontAsset _font;
        Sprite _rounded, _star, _circle;
        readonly System.Collections.Generic.List<Object> _owned = new System.Collections.Generic.List<Object>();

        void Start()
        {
            _font = TMP_Settings.defaultFontAsset;
            _rounded = MakeRounded();
            _star = MakeStar();
            _circle = MakeCircle();
            Build();
        }

        void OnDestroy()
        {
            foreach (var o in _owned) if (o != null) Destroy(o);
        }

        // ------------------------------------------------------------------ layout
        void Build()
        {
            var canvasGo = new GameObject("Menu", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // Menu pintado do usuário (tela inteira) quando existir.
            if (UiArt.MenuMain(Era) != null) { BuildPainted(root); return; }

            // Fundo: céu azul em degradê (como as telas do BTD6)
            var bg = New("Background", root);
            Stretch(bg);
            bg.gameObject.AddComponent<RawImage>().texture = Own(MakeGradient(new Color32(74, 138, 196, 255), new Color32(23, 58, 99, 255)));

            // Mapa da saga pintado (04_Interface_Mapa_Saga) quando existir; senão, os cartões.
            if (UiArt.SagaMap(Era) != null) BuildSaga(root);
            else BuildGrid(root);
        }

        // ------------------------------------------------------------------ menu pintado (arte do usuário, 27/11)
        bool _painted;
        RectTransform _art;
        RectTransform[] _rings;
        TMP_Text _toast;
        float _toastUntil;
        int _thumbW = 520, _thumbH = 290;

        /// <summary>Retângulo da arte (px, y de cima) → filho ancorado proporcionalmente (acompanha qualquer tela).</summary>
        RectTransform At(string name, Rect px, RectTransform parent = null)
        {
            var r = New(name, parent != null ? parent : _art);
            Vector2 size = UiArt.MenuSize;
            r.anchorMin = new Vector2(px.xMin / size.x, 1f - px.yMax / size.y);
            r.anchorMax = new Vector2(px.xMax / size.x, 1f - px.yMin / size.y);
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        static Rect Around(Vector2 c, float w, float h) => Rect.MinMaxRect(c.x - w * 0.5f, c.y - h * 0.5f, c.x + w * 0.5f, c.y + h * 0.5f);

        /// <summary>Botão invisível sobre um botão pintado: só um clarão suave ao passar/tocar.</summary>
        Button HotSpot(string name, Rect px, UnityAction onClick)
        {
            var r = At(name, px);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = _rounded;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.10f);
            cb.pressedColor = new Color(1f, 1f, 1f, 0.25f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.disabledColor = new Color(0f, 0f, 0f, 0.35f);
            b.colors = cb;
            b.onClick.AddListener(onClick);
            return b;
        }

        TMP_Text PaintedText(Rect px, string text, float size, Color color, Color outline)
        {
            var t = AddText(At("Text", px), text, size, color);
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true;
            t.fontSizeMin = 18;
            t.fontSizeMax = size;
            t.outlineWidth = 0.22f;
            t.outlineColor = outline;
            t.raycastTarget = false;
            return t;
        }

        void BuildPainted(RectTransform root)
        {
            _painted = true;
            EraSO era = Campaign != null && Campaign.Eras != null && Era - 1 < Campaign.Eras.Length ? Campaign.Eras[Era - 1] : null;
            _levels = new LevelDataSO[10];
            for (int i = 0; i < 10; i++) _levels[i] = era != null && era.Levels != null && i < era.Levels.Length ? era.Levels[i] : null;

            // Arte inteira sempre visível (botões ficam perto das bordas): encaixa na tela; se a tela for mais larga ou
            // mais alta que 16:9, as sobras mostram a mesma arte esticada e escurecida atrás.
            Texture2D tex = UiArt.MenuMain(Era);
            var back = New("Back", root);
            Stretch(back);
            var backImg = back.gameObject.AddComponent<RawImage>();
            backImg.texture = tex;
            backImg.color = new Color(0.45f, 0.45f, 0.5f, 1f);
            backImg.raycastTarget = false;
            _art = New("MenuArt", root);
            _art.anchorMin = _art.anchorMax = new Vector2(0.5f, 0.5f);
            var raw = _art.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            var fit = _art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = UiArt.MenuSize.x / UiArt.MenuSize.y;

            Texture2D starTex = UiArt.MenuStar != null ? UiArt.MenuStar : UiArt.SagaStar;
            Texture2D ringTex = UiArt.MenuRing;
            _stones = new RectTransform[10];
            _rings = new RectTransform[10];
            for (int i = 0; i < 10; i++)
            {
                Vector3 c = UiArt.MenuStones[i];
                int index = i;
                // anel dourado da fase escolhida (atrás do toque)
                var ring = At("Ring" + (i + 1), Around(new Vector2(c.x, c.y), c.z * 3f, c.z * 3f));
                var ri = ring.gameObject.AddComponent<RawImage>();
                ri.texture = ringTex;
                ri.raycastTarget = false;
                if (ringTex == null) { ri.texture = null; ri.color = new Color(1f, 0.85f, 0.3f, 0.35f); }
                ring.gameObject.SetActive(false);
                _rings[i] = ring;

                var hit = HotSpot("Stone" + (i + 1), Around(new Vector2(c.x, c.y), c.z * 2.4f, c.z * 2.4f), () => SelectLevel(index));
                hit.GetComponent<Image>().sprite = _circle;
                hit.GetComponent<Image>().type = Image.Type.Simple;
                if (_levels[i] == null) hit.interactable = false;
                _stones[i] = ring;

                // 3 estrelas logo abaixo do disco (douradas = conquistadas)
                int stars = GameSession.BestStars(_levels[i]);
                float ss = c.z * 1.0f;
                for (int k = 0; k < 3; k++)
                {
                    var sr = At("Star", Around(new Vector2(c.x + (k - 1) * ss * 1.0f, c.y + c.z * 1.08f + (k == 1 ? ss * 0.08f : 0f)), ss, ss));
                    var si = sr.gameObject.AddComponent<RawImage>();
                    si.texture = starTex;
                    si.raycastTarget = false;
                    si.color = k < stars ? Color.white : new Color(0.22f, 0.17f, 0.12f, 0.95f);
                }
            }

            // Painel da fase escolhida
            _selTitle = PaintedText(UiArt.MenuTitle, "", 60, new Color32(255, 246, 226, 255), new Color32(58, 32, 12, 255));
            _selMap = PaintedText(UiArt.MenuSubtitle, "", 34, new Color32(255, 214, 92, 255), new Color32(58, 32, 12, 255));
            _thumbW = 640; _thumbH = Mathf.RoundToInt(640f * UiArt.MenuThumb.height / UiArt.MenuThumb.width);
            _selThumb = At("Thumb", UiArt.MenuThumb).gameObject.AddComponent<RawImage>();
            _selThumb.raycastTarget = false;
            var badge = At("Boss", Rect.MinMaxRect(UiArt.MenuThumb.xMax - 112f, UiArt.MenuThumb.yMin + 8f, UiArt.MenuThumb.xMax - 8f, UiArt.MenuThumb.yMin + 42f));
            var bimg = badge.gameObject.AddComponent<Image>();
            bimg.sprite = _rounded;
            bimg.type = Image.Type.Sliced;
            bimg.color = Red;
            bimg.raycastTarget = false;
            Stretch((RectTransform)AddText(New("Text", badge), "CHEFE", 26, Color.white).transform);
            _selBoss = badge.gameObject;
            Sprite gold = starTex != null ? OwnSprite(starTex) : _star;
            _selStars = new Image[3];
            for (int k = 0; k < 3; k++)
            {
                var sr = At("SelStar", Around(UiArt.MenuStarSlots[k], UiArt.MenuStarSlotSize, UiArt.MenuStarSlotSize));
                _selStars[k] = sr.gameObject.AddComponent<Image>();
                _selStars[k].sprite = gold;
                _selStars[k].raycastTarget = false;
            }

            // Botões pintados
            HotSpot("Play", UiArt.MenuPlay, () => { if (_selected >= 0) Play(_levels[_selected]); });
            var test = HotSpot("Test", UiArt.MenuTest, () => Play(TestLevel));
            test.interactable = TestLevel != null;
            HotSpot("Quit", UiArt.MenuQuit, Application.Quit);
            HotSpot("Settings", UiArt.MenuGear, () => { _toast.gameObject.SetActive(true); _toastUntil = Time.unscaledTime + 1.8f; })
                .GetComponent<Image>().sprite = _circle;
            _toast = PaintedText(Rect.MinMaxRect(1200f, 104f, 1660f, 140f), "Configurações em breve", 30, Color.white, Ink);
            _toast.gameObject.SetActive(false);

            int first = 0;
            for (int i = 0; i < 10; i++) if (_levels[i] != null && GameSession.BestStars(_levels[i]) < 3) { first = i; break; }
            SelectLevel(first);
        }

        Sprite OwnSprite(Texture2D t)
        {
            var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            _owned.Add(s);
            return s;
        }

        // ------------------------------------------------------------------ mapa da saga
        LevelDataSO[] _levels;
        RectTransform[] _stones;
        int _selected = -1;
        TMP_Text _selTitle, _selMap;
        RawImage _selThumb;
        Image[] _selStars;
        GameObject _selBoss;
        readonly System.Collections.Generic.Dictionary<LevelDataSO, Texture2D> _thumbs = new System.Collections.Generic.Dictionary<LevelDataSO, Texture2D>();

        void BuildSaga(RectTransform root)
        {
            EraSO era = Campaign != null && Campaign.Eras != null && Era - 1 < Campaign.Eras.Length ? Campaign.Eras[Era - 1] : null;
            _levels = new LevelDataSO[10];
            for (int i = 0; i < 10; i++) _levels[i] = era != null && era.Levels != null && i < era.Levels.Length ? era.Levels[i] : null;

            // Mapa (proporção da arte), à esquerda
            Texture2D mapTex = UiArt.SagaMap(Era);
            float mapH = 1040f, mapW = mapH * UiArt.SagaMapSize.x / UiArt.SagaMapSize.y;
            var map = New("SagaMap", root);
            map.anchorMin = map.anchorMax = new Vector2(0.5f, 0.5f);
            map.sizeDelta = new Vector2(mapW, mapH);
            map.anchoredPosition = new Vector2(-330f, 0f);
            var mapImg = map.gameObject.AddComponent<RawImage>();
            mapImg.texture = mapTex;
            mapImg.raycastTarget = false;

            // Pedras das fases sobre a estrada, com as estrelas conquistadas
            float s = mapW / UiArt.SagaMapSize.x;
            Texture2D starTex = UiArt.SagaStar;
            _stones = new RectTransform[10];
            for (int i = 0; i < 10; i++)
            {
                Vector3 c = UiArt.SagaStones[i];
                Texture2D stoneTex = UiArt.SagaStone(i + 1);
                float sw = c.z * 1.18f * s;
                float sh = stoneTex != null ? sw * stoneTex.height / stoneTex.width : sw * 0.55f;
                float boxTop = c.y - c.z * 0.5f;                       // topo da pedra pintada (px do mapa)
                float x = (c.x / UiArt.SagaMapSize.x - 0.5f) * mapW;
                float yTop = (0.5f - boxTop / UiArt.SagaMapSize.y) * mapH;
                var st = New("Stone" + (i + 1), map);
                st.anchorMin = st.anchorMax = new Vector2(0.5f, 0.5f);
                st.pivot = new Vector2(0.5f, 1f);
                st.sizeDelta = new Vector2(sw, sh);
                st.anchoredPosition = new Vector2(x, yTop + 2f);
                var img = st.gameObject.AddComponent<RawImage>();
                img.texture = stoneTex;
                if (stoneTex == null) img.color = new Color(0.6f, 0.6f, 0.6f);
                int index = i;
                var btn = st.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SelectLevel(index));
                _stones[i] = st;

                // 3 estrelas logo abaixo da pedra (acesas conforme o melhor resultado)
                int stars = GameSession.BestStars(_levels[i]);
                float ss = sw * 0.3f;
                for (int k = 0; k < 3; k++)
                {
                    var sr = New("Star", st);
                    sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0f);
                    sr.sizeDelta = new Vector2(ss, ss);
                    sr.anchoredPosition = new Vector2((k - 1) * ss * 0.95f, -ss * 0.25f + (k == 1 ? -ss * 0.12f : 0f));
                    var si = sr.gameObject.AddComponent<RawImage>();
                    si.texture = starTex;
                    si.raycastTarget = false;
                    si.color = k < stars ? Color.white : new Color(0.25f, 0.22f, 0.2f, 1f);
                    if (starTex == null) si.color = k < stars ? Yellow : StarOff;
                }
                if (_levels[i] == null) btn.interactable = false;
            }

            // Painel da fase escolhida, à direita
            MakeText(root, "CHRONO DEFENSE", 92, Yellow, new Vector2(0.5f, 1f), new Vector2(560, -30), new Vector2(900, 110));
            MakeText(root, Era == 1 ? "Era 1 · Pré-História" : $"Era {Era}", 38, Color.white, new Vector2(0.5f, 1f), new Vector2(560, -135), new Vector2(900, 50));

            var panel = New("Selected", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(600, 640);
            panel.anchoredPosition = new Vector2(560, -40);
            var pbg = panel.gameObject.AddComponent<Image>();
            pbg.sprite = _rounded;
            pbg.type = Image.Type.Sliced;
            pbg.color = Wood;
            var po = panel.gameObject.AddComponent<Outline>();
            po.effectColor = WoodDark;
            po.effectDistance = new Vector2(6, -6);
            _selTitle = MakeText(panel, "", 60, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(560, 72));
            _selMap = MakeText(panel, "", 32, new Color(1f, 0.9f, 0.6f), new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(560, 44));
            var th = New("Thumb", panel);
            th.anchorMin = th.anchorMax = new Vector2(0.5f, 1f);
            th.pivot = new Vector2(0.5f, 1f);
            th.sizeDelta = new Vector2(520, 290);
            th.anchoredPosition = new Vector2(0, -146);
            _selThumb = th.gameObject.AddComponent<RawImage>();
            _selThumb.raycastTarget = false;
            _selStars = new Image[3];
            for (int k = 0; k < 3; k++)
            {
                var sr = New("Star", panel);
                sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f);
                sr.pivot = new Vector2(0.5f, 1f);
                sr.sizeDelta = new Vector2(70, 70);
                sr.anchoredPosition = new Vector2((k - 1) * 80, -448);
                _selStars[k] = sr.gameObject.AddComponent<Image>();
                _selStars[k].sprite = _star;
                _selStars[k].raycastTarget = false;
            }
            var badge = New("Boss", panel);
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
            badge.pivot = new Vector2(1f, 1f);
            badge.sizeDelta = new Vector2(130, 46);
            badge.anchoredPosition = new Vector2(-14, -14);
            var bimg = badge.gameObject.AddComponent<Image>();
            bimg.sprite = _rounded;
            bimg.type = Image.Type.Sliced;
            bimg.color = Red;
            Stretch((RectTransform)AddText(New("Text", badge), "CHEFE", 28, Color.white).transform);
            _selBoss = badge.gameObject;
            var play = MakeButton(panel, "JOGAR", new Vector2(0.5f, 0f), new Vector2(-190, 24), new Vector2(380, 110), Green, () => { if (_selected >= 0) Play(_levels[_selected]); });
            play.GetComponentInChildren<TMP_Text>().fontSize = 56;

            if (TestLevel != null)
                MakeButton(root, "Fase de teste: Prado", new Vector2(0.5f, 0f), new Vector2(260, 24), new Vector2(400, 84), Wood, () => Play(TestLevel))
                    .GetComponentInChildren<TMP_Text>().fontSize = 30;
            MakeButton(root, "Sair", new Vector2(0.5f, 0f), new Vector2(680, 24), new Vector2(180, 84), Red, Application.Quit);

            // Começa na primeira fase ainda sem 3 estrelas
            int first = 0;
            for (int i = 0; i < 10; i++) if (_levels[i] != null && GameSession.BestStars(_levels[i]) < 3) { first = i; break; }
            SelectLevel(first);
        }

        void SelectLevel(int i)
        {
            LevelDataSO level = _levels[i];
            if (level == null) return;
            if (_selected == i) { Play(level); return; }   // tocar de novo na pedra já escolhida = jogar
            _selected = i;
            _selTitle.text = "Fase " + (i + 1);
            _selMap.text = level.Layout != null && !string.IsNullOrEmpty(level.Layout.DisplayName) ? level.Layout.DisplayName : "";
            if (level.Layout != null)
            {
                if (!_thumbs.TryGetValue(level, out Texture2D t)) { t = Own(MapThumbnail(level.Layout, level.ResolvedBiome, _thumbW, _thumbH)); _thumbs[level] = t; }
                _selThumb.texture = t;
            }
            int stars = GameSession.BestStars(level);
            for (int k = 0; k < 3; k++)
                _selStars[k].color = k < stars ? (_painted ? Color.white : Yellow) : (_painted ? Color.clear : StarOff);   // pintado: a casa escura já está na arte
            _selBoss.SetActive(level.Boss != null);
            if (_rings != null) for (int k = 0; k < _rings.Length; k++) _rings[k].gameObject.SetActive(k == i);
        }

        void Update()
        {
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toast.gameObject.SetActive(false);
            // pedra escolhida "respira" para mostrar a seleção (no menu pintado, o anel dourado)
            if (_stones == null) return;
            for (int i = 0; i < _stones.Length; i++)
            {
                float k = i == _selected ? 1.12f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
                _stones[i].localScale = new Vector3(k, k, 1f);
            }
        }

        // ------------------------------------------------------------------ cartões (sem a arte do mapa)
        void BuildGrid(RectTransform root)
        {
            MakeText(root, "CHRONO DEFENSE", 110, Yellow, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1400, 130));
            string eraName = Era == 1 ? "Era 1 · Pré-História — Capítulo 1: Tundra" : $"Era {Era}";
            MakeText(root, eraName, 40, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1400, 60));

            // Cartões das 10 fases (5 × 2)
            var grid = New("Levels", root);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(5 * 320, 2 * 300);
            grid.anchoredPosition = new Vector2(0, -40);
            EraSO era = Campaign != null && Campaign.Eras != null && Era - 1 < Campaign.Eras.Length ? Campaign.Eras[Era - 1] : null;
            for (int i = 0; i < 10; i++)
            {
                LevelDataSO level = era != null && era.Levels != null && i < era.Levels.Length ? era.Levels[i] : null;
                var pos = new Vector2(-2 * 320 + (i % 5) * 320, 150 - (i / 5) * 300);
                LevelCard(grid, level, $"Fase {i + 1}", pos);
            }
            if (era == null)
                MakeText(root, "Rode Tools ▸ Chrono ▸ Gerar dados do MVP", 36, Red, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 60));

            // Fase de teste e sair
            if (TestLevel != null)
            {
                var test = MakeButton(root, "Fase de teste: Prado", new Vector2(0f, 0f), new Vector2(40, 40), new Vector2(460, 90), Wood, () => Play(TestLevel));
                test.GetComponentInChildren<TMP_Text>().fontSize = 34;
            }
            MakeButton(root, "Sair", new Vector2(1f, 0f), new Vector2(-40 - 220, 40), new Vector2(220, 90), Red, Application.Quit);
        }

        void LevelCard(RectTransform parent, LevelDataSO level, string title, Vector2 pos)
        {
            bool playable = level != null && level.Layout != null;
            var card = New(title, parent);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(300, 280);
            card.anchoredPosition = pos;
            var bg = card.gameObject.AddComponent<Image>();
            bg.sprite = _rounded;
            bg.type = Image.Type.Sliced;
            bg.color = playable ? Wood : new Color(0.35f, 0.3f, 0.27f);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = WoodDark;
            o.effectDistance = new Vector2(5, -5);
            if (playable)
            {
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                b.onClick.AddListener(() => Play(level));
            }

            // Miniatura do mapa
            var thumb = New("Map", card);
            thumb.anchorMin = thumb.anchorMax = new Vector2(0.5f, 1f);
            thumb.pivot = new Vector2(0.5f, 1f);
            thumb.sizeDelta = new Vector2(270, 150);
            thumb.anchoredPosition = new Vector2(0, -12);
            var raw = thumb.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = playable ? Own(MapThumbnail(level.Layout, level.ResolvedBiome, 270, 150)) : null;
            raw.color = playable ? Color.white : new Color(0.2f, 0.2f, 0.2f);

            MakeText(card, title, 36, Color.white, new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(290, 46), anchorPivotBottom: true);
            string mapName = playable && !string.IsNullOrEmpty(level.Layout.DisplayName) ? level.Layout.DisplayName : playable ? level.Layout.name : "—";
            MakeText(card, mapName, 24, new Color(1f, 0.9f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0, 62), new Vector2(290, 34), anchorPivotBottom: true);

            // Estrelas (melhor resultado)
            int stars = GameSession.BestStars(level);
            for (int s = 0; s < 3; s++)
            {
                var st = New("Star", card);
                st.anchorMin = st.anchorMax = new Vector2(0.5f, 0f);
                st.pivot = new Vector2(0.5f, 0f);
                st.sizeDelta = new Vector2(44, 44);
                st.anchoredPosition = new Vector2((s - 1) * 50, 12);
                var img = st.gameObject.AddComponent<Image>();
                img.sprite = _star;
                img.color = s < stars ? Yellow : StarOff;
                img.raycastTarget = false;
            }

            // Selo de chefe
            if (level != null && level.Boss != null)
            {
                var badge = New("Boss", card);
                badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
                badge.pivot = new Vector2(1f, 1f);
                badge.sizeDelta = new Vector2(110, 40);
                badge.anchoredPosition = new Vector2(-6, -6);
                var bimg = badge.gameObject.AddComponent<Image>();
                bimg.sprite = _rounded;
                bimg.type = Image.Type.Sliced;
                bimg.color = Red;
                bimg.raycastTarget = false;
                var t = AddText(New("Text", badge), "CHEFE", 24, Color.white);
                Stretch((RectTransform)t.transform);
            }
        }

        static void Play(LevelDataSO level)
        {
            if (level == null) return;
            GameSession.SelectedLevel = level;
            SceneManager.LoadScene(GameSession.GameplayScene);
        }

        // ------------------------------------------------------------------ miniatura do mapa
        /// <summary>
        /// Desenha o quadro do layout (CameraBounds) visto de cima: chão do bioma, sombra e contorno do caminho e
        /// lajotas. Mantém a proporção do mapa (sobra = borda mais escura). Feito uma vez ao abrir o menu.
        /// </summary>
        static Texture2D MapThumbnail(LevelLayoutSO l, GroundStyle biome, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Bounds b = l.CameraBounds;
            float scale = Mathf.Min(w / b.size.x, h / b.size.z);   // px por metro
            float ox = (w - b.size.x * scale) * 0.5f, oy = (h - b.size.z * scale) * 0.5f;
            Color ground, frame, stone;
            switch (biome)
            {
                case GroundStyle.Meadow: ground = new Color32(130, 196, 70, 255); frame = new Color32(80, 140, 45, 255); stone = new Color32(207, 202, 192, 255); break;
                case GroundStyle.Forest: ground = new Color32(104, 178, 64, 255); frame = new Color32(52, 112, 40, 255); stone = new Color32(220, 203, 164, 255); break;
                case GroundStyle.Savanna: ground = new Color32(190, 190, 100, 255); frame = new Color32(150, 140, 70, 255); stone = new Color32(220, 203, 164, 255); break;
                case GroundStyle.Desert: ground = new Color32(226, 200, 140, 255); frame = new Color32(181, 143, 87, 255); stone = new Color32(196, 175, 134, 255); break;
                case GroundStyle.Highland: ground = new Color32(100, 158, 74, 255); frame = new Color32(110, 116, 108, 255); stone = new Color32(201, 194, 178, 255); break;
                default: ground = new Color32(228, 239, 246, 255); frame = new Color32(160, 186, 204, 255); stone = new Color32(163, 156, 146, 255); break;
            }
            Color edge = new Color32(75, 70, 63, 255);
            float half = l.PathWidth * 0.5f;
            Vector3[] p = l.PathPolyline;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float wx = b.min.x + (x + 0.5f - ox) / scale;
                float wz = b.min.z + (y + 0.5f - oy) / scale;
                bool inside = wx >= b.min.x && wx <= b.max.x && wz >= b.min.z && wz <= b.max.z;
                float d = float.MaxValue;
                for (int s = 0; s < p.Length - 1; s++)
                {
                    Vector2 a = new Vector2(p[s].x, p[s].z), ab = new Vector2(p[s + 1].x - p[s].x, p[s + 1].z - p[s].z);
                    float t = Mathf.Clamp01(Vector2.Dot(new Vector2(wx, wz) - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                    d = Mathf.Min(d, Vector2.Distance(new Vector2(wx, wz), a + ab * t));
                }
                Color c = inside ? ground : frame;
                if (d < half + 0.9f) c = Color.Lerp(c, Color.black, 0.18f);   // sombra
                if (d < half + 0.3f) c = edge;                                  // contorno
                if (d < half) c = stone;                                         // lajotas
                px[y * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // ------------------------------------------------------------------ helpers
        T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        static RectTransform New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        TMP_Text AddText(RectTransform r, string text, float size, Color color)
        {
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.3f;
            t.outlineColor = Ink;
            t.raycastTarget = false;
            return t;
        }

        TMP_Text MakeText(RectTransform parent, string text, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, bool anchorPivotBottom = false)
        {
            var r = New("Text", parent);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, anchorPivotBottom ? 0f : 1f);
            r.sizeDelta = box;
            r.anchoredPosition = pos;
            return AddText(r, text, size, color);
        }

        Button MakeButton(RectTransform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color, UnityAction onClick)
        {
            var r = New(label, parent);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = Vector2.zero;
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = _rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(4, -4);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            var t = AddText(New("Text", r), label, 40, Color.white);
            Stretch((RectTransform)t.transform);
            return b;
        }

        Texture2D MakeGradient(Color top, Color bottom)
        {
            var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 4; x++)
                t.SetPixel(x, y, Color.Lerp(bottom, top, y / 63f));
            t.Apply();
            return t;
        }

        Sprite MakeRounded()
        {
            const int size = 64;
            const float radius = 18f;
            var tex = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                float qy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
            }
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Own(Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border));
        }

        Sprite MakeCircle()
        {
            const int size = 64;
            var tex = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude - (size * 0.5f - 1f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
            }
            tex.Apply();
            return Own(Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f));
        }

        /// <summary>Estrela de 5 pontas branca com contorno escuro (tingida pela cor da Image).</summary>
        Sprite MakeStar()
        {
            const int size = 64;
            // polígono de 10 vértices alternando ponta (0,46) e vale (0,19), ponta para cima
            var v = new Vector2[10];
            for (int k = 0; k < 10; k++)
            {
                float a = Mathf.PI / 2f + k * Mathf.PI / 5f;
                float r = k % 2 == 0 ? 0.46f : 0.19f;
                v[k] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.02f);
            }
            var tex = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                bool inside = false;
                float dist = float.MaxValue;
                for (int k = 0, j = 9; k < 10; j = k++)
                {
                    if ((v[k].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[k].x) * (p.y - v[k].y) / (v[j].y - v[k].y) + v[k].x) inside = !inside;
                    Vector2 ab = v[j] - v[k];
                    float t = Mathf.Clamp01(Vector2.Dot(p - v[k], ab) / ab.sqrMagnitude);
                    dist = Mathf.Min(dist, Vector2.Distance(p, v[k] + ab * t));
                }
                float d = inside ? -dist : dist;
                Color c = d > -0.045f ? new Color(0.25f, 0.2f, 0.15f, 1f) : Color.white;
                c.a = Mathf.Clamp01(0.5f - d * size);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Own(Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f));
        }
    }
}
