// Bootstrap da partida: carrega o nível, roda a simulação em tick fixo de 30 Hz (acumulado no Update,
// sem FixedUpdate, GDD §4.2) e sincroniza a apresentação com interpolação.
// Física desligada (GDD §2): Physics/Physics2D em SimulationMode.Script.
using System;
using System.Threading;
using ChronoDefense.Data;
using ChronoDefense.Systems;
using ChronoDefense.UI;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace ChronoDefense.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const int MaxTicksPerFrame = 20;   // evita espiral de recuperação em 16× ou picos
        public const int FloatyTextPoolSize = 64; // GDD §3.4

        [Header("Conteúdo")]
        public CampaignSO Campaign;
        public LevelDataSO Level;
        [Tooltip("Heróis do loadout (máx. 2)")]
        public HeroDataSO[] Loadout;
        [Tooltip("DEV: libera heróis mesmo no capítulo 1 (GDD §1.3 diz 0 heróis no cap. 1; M4 testa o Krog jogável)")]
        public bool DevUnlockHeroes = true;
        public QualityTier Quality = QualityTier.Medium;

        [Header("Testes")]
        [Tooltip("> 0: executa exatamente N ticks por frame (testes determinísticos de estresse)")]
        public int ManualTicksPerFrame;
        public int CreepCapacityOverride;
        public bool AutoLoad = true;
        // O BTD6 não mostra o ouro de cada abate (polui a tela com dezenas de "+4"); o saldo no HUD basta.
        public bool FloatyTextEnabled = false;

        public CombatSimulation Sim { get; private set; }
        public LevelRenderer Renderer { get; private set; }
        public UnitRenderer Units { get; private set; }
        public CameraRig Rig { get; private set; }
        public TowerPlacer Placer { get; private set; }
        public HudController Hud { get; private set; }
        public ObjectPoolManager Pools { get; private set; }
        public bool Loaded { get; private set; }
        public int Speed { get; private set; } = 1;
        public bool Paused { get; private set; }

        float _accumulator;
        Quaternion _billboard;
        int _floatyPool = -1;
        FloatyText[] _floaties;
        CancellationTokenSource _cts;

        void Awake()
        {
            Physics.simulationMode = SimulationMode.Script;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Application.targetFrameRate = Quality == QualityTier.Low ? 30 : 60;
        }

        async void Start()
        {
            if (GameSession.SelectedLevel != null) Level = GameSession.SelectedLevel;   // escolhida no menu inicial
            if (!AutoLoad) return;
            try { await LoadAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        /// <summary>Carregamento completo (único trecho que aloca). GDD §11.3.</summary>
        public async Awaitable LoadAsync()
        {
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;
            if (Level == null) throw new InvalidOperationException("GameBootstrap: Level não definido.");
            TowerDataSO[] towers = Campaign != null ? Campaign.Towers : Array.Empty<TowerDataSO>();
            HeroDataSO[] loadout = Loadout ?? Array.Empty<HeroDataSO>();
            if (loadout.Length > BalanceFormulas.MaxLoadoutHeroes) Array.Resize(ref loadout, BalanceFormulas.MaxLoadoutHeroes);

            // Simulação (atribui PoolIds e faz o bake do flow field)
            Sim = new CombatSimulation();
            Sim.Load(Level, towers, loadout, CreepCapacityOverride);

            // Câmera
            Camera cam = GetComponentInChildren<Camera>();
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null) cam = new GameObject("MainCamera").AddComponent<Camera>();
            Rig = cam.gameObject.GetComponent<CameraRig>();
            if (Rig == null) Rig = cam.gameObject.AddComponent<CameraRig>();
            Rig.Init(cam, Level.Layout.CameraBounds);
            _billboard = cam.transform.rotation;

            // Cenário
            Renderer = new GameObject("LevelRenderer").AddComponent<LevelRenderer>();
            Renderer.transform.SetParent(transform, false);
            Renderer.Camera = cam;
            EraBiomeThemeSO theme = Level.Era != null ? Level.Era.Theme : null;
            await Renderer.LoadAsync(Level, theme, ct);
            await Renderer.LoadHeroViewsAsync(loadout, ct);
            Renderer.BuildPortraits();   // fotos dos modelos 3D para a loja e o painel (HudController)
            Renderer.ApplyQualityTier(Quality);
            Renderer.FitFogToCamera(Rig.BaseDistance);
            Units = new UnitRenderer(Renderer.Art, Sim.Creeps.Capacity, CombatSimulation.ProjectileCapacity);

            // Pools de views (GDD §3.4)
            Pools = gameObject.AddComponent<ObjectPoolManager>();
            var floatyRoot = new GameObject("FloatyTexts").transform;
            floatyRoot.SetParent(transform, false);
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            _floatyPool = Pools.Register(FloatyTextPoolSize, _ => FloatyText.Create(floatyRoot, _billboard, font), recycleOldest: true);
            _floaties = new FloatyText[FloatyTextPoolSize];

            // Interação
            var range = new GameObject("RangeIndicator").AddComponent<RangeIndicator>();
            range.transform.SetParent(transform, false);
            range.Init(Renderer.Art);
            Placer = gameObject.AddComponent<TowerPlacer>();
            Hud = gameObject.AddComponent<HudController>();
            Placer.Init(Sim, Renderer, range, Rig, Hud);
            Rig.Placer = Placer;
            Hud.Init(this, Sim, Placer, Renderer.Art, Sim.HeroesAllowed(DevUnlockHeroes) && loadout.Length > 0);

            // Rede de segurança antes da onda 1 (GDD §3.3)
            GC.Collect();
            AsyncOperation unload = Resources.UnloadUnusedAssets();
            while (!unload.isDone) await Awaitable.NextFrameAsync(ct);
            Loaded = true;
        }

        public void SetSpeed(int speed)
        {
            Speed = Mathf.Max(1, speed);
            Paused = false;
        }

        public void TogglePause() => Paused = !Paused;

        void Update()
        {
            if (!Loaded) return;

            // Tick fixo (GDD §4.2): velocidade k executa k ticks por período de 1/30 s
            int ticks;
            if (ManualTicksPerFrame > 0) ticks = Paused ? 0 : ManualTicksPerFrame;
            else
            {
                if (!Paused) _accumulator += Time.unscaledDeltaTime * Speed;
                ticks = (int)(_accumulator / CombatSimulation.Dt);
                if (ticks > MaxTicksPerFrame) { ticks = MaxTicksPerFrame; _accumulator = 0f; }
                else _accumulator -= ticks * CombatSimulation.Dt;
            }
            for (int i = 0; i < ticks; i++) Sim.Step();

            // Apresentação
            float alpha = ManualTicksPerFrame > 0 ? 1f : math.saturate(_accumulator / CombatSimulation.Dt);
            Units.Render(Sim, _billboard, alpha);
            ConsumeEvents();
            TickFloaties(Time.unscaledDeltaTime);
        }

        void ConsumeEvents()
        {
            GameEventBuffer events = Sim.Events;
            for (int i = 0; i < events.Count; i++)
            {
                ref readonly GameEvent e = ref events[i];
                Placer.OnGameEvent(e);
                Hud.OnGameEvent(e);
                switch (e.Type)
                {
                    case GameEventType.Kill:
                        if (FloatyTextEnabled) SpawnFloaty(new Vector3(e.Position.x, 0f, e.Position.z), (int)e.Value);
                        break;
                    case GameEventType.WaveStarted:
                        bool bossWave = Level.Boss != null && e.A == Sim.WaveCount;
                        Renderer.SetRuneState(bossWave ? RuneVisualState.BossWave : RuneVisualState.WaveActive);
                        break;
                    case GameEventType.WaveCleared:
                    case GameEventType.Victory:
                    case GameEventType.Defeat:
                        Renderer.SetRuneState(RuneVisualState.Idle);
                        break;
                }
            }
            events.Clear();
        }

        void SpawnFloaty(Vector3 pos, int gold)
        {
            FloatyText f = Pools.Rent<FloatyText>(_floatyPool);
            if (f == null) return;
            f.Show(pos, gold);
            for (int i = 0; i < _floaties.Length; i++)
            {
                if (_floaties[i] == f) return;   // reciclado: já está na lista
                if (_floaties[i] == null) { _floaties[i] = f; return; }
            }
        }

        void TickFloaties(float dt)
        {
            for (int i = 0; i < _floaties.Length; i++)
            {
                FloatyText f = _floaties[i];
                if (f == null) continue;
                f.Tick(dt);
                if (!f.Alive)
                {
                    Pools.Return(_floatyPool, f);
                    _floaties[i] = null;
                }
            }
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            Units?.Dispose();
            Sim?.Dispose();
        }
    }
}
