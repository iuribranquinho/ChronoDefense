// GDD §4.2 — simulação de combate em tick fixo de 30 Hz. Dono de todo o estado mutável da partida.
// Load() é o único ponto que aloca; Step() e os comandos do jogador são zero-GC.
using System;
using ChronoDefense.Core;
using ChronoDefense.Data;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ChronoDefense.Systems
{
    public enum SimPhase : byte { Loading, Building, WaveActive, Intermission, Won, Lost }

    public sealed class CombatSimulation : IDisposable
    {
        public const float TickRate = 30f;
        public const float Dt = 1f / TickRate;
        public const int HardCreepCap = 150;           // GDD §3.4
        public const int ProjectileCapacity = 1024;    // GDD §3.4
        public const int MaxUnits = 32;                // torres + heróis
        public const int MaxZones = 4;
        public const float LeakRadius = 1.0f;
        public const float LaneOffsetMax = 0.6f;       // GDD §4.3
        public const float FirstWaveDelay = 30f;       // PROV: tempo de preparação antes da onda 1
        public const float IntermissionTime = 12f;     // PROV: contagem entre ondas
        public const float TorchTelegraph = 1.5f;      // GDD §9.2
        public const float CrowdBonusCap = 1.5f;       // GDD §6.2 (Krog: máx. +150%)

        // Posicionamento livre (26/11, no estilo do BTD6): sem pedestais; a unidade vai em qualquer chão fora do
        // caminho. Internamente cada unidade ocupa um "ponto" (o antigo pedestal), criado onde o jogador tocou.
        public const int MaxSpots = MaxUnits;
        /// <summary>Raio da base de uma unidade no chão (m): afasta do caminho e das outras unidades.</summary>
        public const float FootprintRadius = 1.3f;
        /// <summary>Folga em volta do covil e do Núcleo onde não se constrói (m).</summary>
        public const float LandmarkClearance = 4f;

        // Soldados do Guardião (26/11, rebalanceado às 23h45). Cada soldado segura UM inimigo (não voador, nunca o
        // chefe); quem segura apanha (12 + 0,35 × massa) dano/s × a mesma escala de vida do inimigo naquela fase/onda
        // (lobo 15,5 · tigre 22,5 · rinoceronte 40 · Mamute 82 na base). Soldado (150 de vida) aguenta ~10 s um lobo,
        // ~7 s um tigre e ~4 s um rinoceronte; morto, volta em 10 s; parado, recupera 3 de vida/s.
        public const int SoldiersPerGuard = 3;
        public const float SoldierRespawn = 10f;
        public const float SoldierSpeed = 3.5f;          // m/s
        // Alcance dos soldados = alcance do Guardião: saem do posto atrás de qualquer inimigo no raio do quartel.
        public const float SoldierLeash = 1f;            // folga além do alcance antes de largar o inimigo
        public const float SoldierReach = 1.4f;          // distância de golpe
        public const float SoldierSpacing = 1.4f;        // entre postos, ao longo do caminho
        public const float SoldierRegen = 3f;            // vida/s parado
        public const float CreepMeleeBase = 12f, CreepMeleePerMass = 0.35f;

        // ---- estado de simulação (Burst)
        public StructPool<CreepState> Creeps;
        public StructPool<ProjectileState> Projectiles;
        public NativeArray<TowerState> Units;
        public NativeArray<SkillZone> Zones;
        public FlowField Flow;
        public SpatialHashGrid Grid;
        NativeArray<int> _hashScratch, _queryScratch;

        public readonly GameEventBuffer Events = new GameEventBuffer(512);

        // ---- partida
        public LevelDataSO Level { get; private set; }
        public int GlobalLevel { get; private set; }
        public int Era { get; private set; }
        public float EraPower { get; private set; }
        public int MaxTier { get; private set; }
        public SimPhase Phase { get; private set; }
        public float PhaseTimer { get; private set; }
        public int Gold, Lives, StartLives, LivesLost;
        public int WaveNumber { get; private set; }     // 1-based; 0 = antes da primeira
        public int WaveCount { get; private set; }
        public int TickCount { get; private set; }
        public int Stars { get; private set; }
        public float Alpha = BalanceFormulas.AlphaNormal;
        public bool InfiniteLives;                      // testes de estresse
        public bool AutoStartWaves = true;

        // ---- catálogos (somente leitura)
        public EnemyDataSO[] EnemyTypes { get; private set; }
        public TowerDataSO[] TowerDefs { get; private set; }
        public HeroDataSO[] Loadout { get; private set; }

        // ---- pontos de construção (ex-pedestais) e unidades
        /// <summary>Posições dos pontos; só os primeiros <see cref="SpotCount"/> existem.</summary>
        public Vector3[] Pedestals { get; private set; }
        public int[] PedestalUnit { get; private set; }    // índice em Units ou -1
        public int SpotCount { get; private set; }
        public SoldierState[] Soldiers { get; private set; }
        Vector3[] _path;
        float _pathHalfWidth;
        Rect _playArea;
        public UpgradeState[] Upgrades { get; private set; }
        public int[] Spent { get; private set; }
        public float[] HeroCooldown { get; private set; }
        public int[] HeroUnit { get; private set; }        // índice em Units ou -1

        // ---- ondas
        struct SpawnEvent { public float Time; public int Type; }
        SpawnEvent[] _spawns;
        int[] _waveFirst, _waveEnd;
        int _cursor;
        float _waveTime;

        // ---- chefe
        BossDataSO _boss;
        int _bossType = -1, _bossSlot = -1, _bossSerial;
        bool[] _bossPhaseFired;
        float _torchTimer, _torchTelegraph;
        int _torchPedestal = -1;
        public bool BossActive => _bossSlot >= 0;
        public float BossHpFraction { get; private set; }

        Unity.Mathematics.Random _rng;
        int _serial;
        float2 _lairPos, _corePos;
        public float2 LairPos => _lairPos;
        public float2 CorePos => _corePos;

        // =====================================================================================
        // Carregamento (aloca)
        // =====================================================================================
        public void Load(LevelDataSO level, TowerDataSO[] towerDefs, HeroDataSO[] loadout, int creepCapacityOverride = 0, uint seed = 12345)
        {
            Dispose();
            Level = level;
            GlobalLevel = level.GlobalIndex;
            Era = BalanceFormulas.EraOf(GlobalLevel);
            EraPower = BalanceFormulas.EraPower(Era);
            MaxTier = BalanceFormulas.MaxTierForLevel(GlobalLevel);
            TowerDefs = towerDefs;
            Loadout = loadout ?? Array.Empty<HeroDataSO>();
            _rng = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);

            LevelLayoutSO layout = level.Layout;
            _lairPos = new float2(layout.LairPosition.x, layout.LairPosition.z);
            _corePos = new float2(layout.CorePosition.x, layout.CorePosition.z);

            // Flow field (bake no carregamento)
            Flow = new FlowField();
            Flow.Bake(layout.PathPolyline, layout.PathWidth, FlowField.DefaultOrigin);

            // Catálogo de inimigos: ids de pool atribuídos aqui (GDD §3.4)
            BuildEnemyCatalog(level);
            BuildWaveTimeline(level, out int peak);

            int creepCap = creepCapacityOverride > 0 ? creepCapacityOverride
                         : math.clamp(PoolManifest.SizeFor(peak), 16, HardCreepCap);
            Creeps = new StructPool<CreepState>(creepCap);
            Projectiles = new StructPool<ProjectileState>(ProjectileCapacity);
            Units = new NativeArray<TowerState>(MaxUnits, Allocator.Persistent);
            Zones = new NativeArray<SkillZone>(MaxZones, Allocator.Persistent);
            Grid = new SpatialHashGrid(creepCap, FlowField.DefaultOrigin);
            _hashScratch = new NativeArray<int>(SpatialHashGrid.CellCount, Allocator.Persistent);
            _queryScratch = new NativeArray<int>(256, Allocator.Persistent);

            // Pontos: capacidade fixa (zero-GC ao construir). Os pontos sugeridos do layout entram como pontos
            // vazios (invisíveis), reaproveitados quando o jogador constrói em outro lugar.
            Pedestals = new Vector3[MaxSpots];
            PedestalUnit = new int[MaxSpots];
            for (int i = 0; i < PedestalUnit.Length; i++) PedestalUnit[i] = -1;
            SpotCount = 0;
            if (layout.Pedestals != null)
                for (int i = 0; i < layout.Pedestals.Length && SpotCount < MaxSpots; i++) Pedestals[SpotCount++] = layout.Pedestals[i];
            _path = layout.PathPolyline;
            _pathHalfWidth = layout.PathWidth * 0.5f;
            Bounds cb = layout.CameraBounds;
            _playArea = Rect.MinMaxRect(cb.min.x + FootprintRadius, cb.min.z + FootprintRadius,
                                        cb.max.x - FootprintRadius, cb.max.z - FootprintRadius);
            Upgrades = new UpgradeState[MaxUnits];
            Soldiers = new SoldierState[MaxUnits * SoldiersPerGuard];
            Spent = new int[MaxUnits];
            HeroCooldown = new float[Loadout.Length];
            HeroUnit = new int[Loadout.Length];
            for (int i = 0; i < HeroUnit.Length; i++) HeroUnit[i] = -1;

            _boss = level.Boss;
            _bossSlot = -1;
            _bossPhaseFired = new bool[_boss != null && _boss.Phases != null ? _boss.Phases.Length : 0];
            _torchPedestal = -1;

            Gold = level.StartGold;
            StartLives = Lives = level.Lives;
            LivesLost = 0;
            WaveNumber = 0;
            TickCount = 0;
            Stars = 0;
            Phase = SimPhase.Building;
            PhaseTimer = FirstWaveDelay;
            Events.Clear();
        }

        void BuildEnemyCatalog(LevelDataSO level)
        {
            var list = new System.Collections.Generic.List<EnemyDataSO>();
            void Add(EnemyDataSO e)
            {
                if (e != null && !list.Contains(e)) list.Add(e);
            }
            if (level.Waves != null)
                foreach (var w in level.Waves)
                    if (w != null && w.Groups != null)
                        foreach (var g in w.Groups) Add(g.Enemy);
            if (level.Boss != null)
            {
                Add(level.Boss);
                if (level.Boss.Phases != null)
                    foreach (var p in level.Boss.Phases) Add(p.SummonType);
            }
            EnemyTypes = list.ToArray();
            for (int i = 0; i < EnemyTypes.Length; i++) EnemyTypes[i].PoolId = i;
            _bossType = level.Boss != null ? level.Boss.PoolId : -1;
        }

        void BuildWaveTimeline(LevelDataSO level, out int peak)
        {
            var spawns = new System.Collections.Generic.List<SpawnEvent>();
            WaveCount = level.Waves != null ? level.Waves.Length : 0;
            _waveFirst = new int[WaveCount];
            _waveEnd = new int[WaveCount];
            peak = 1;
            int summons = 0;
            if (level.Boss != null && level.Boss.Phases != null)
                foreach (var p in level.Boss.Phases) summons += p.SummonCount;

            for (int w = 0; w < WaveCount; w++)
            {
                _waveFirst[w] = spawns.Count;
                int start = spawns.Count, units = 0;
                WaveDataSO wave = level.Waves[w];
                if (wave != null && wave.Groups != null)
                {
                    foreach (var g in wave.Groups)
                    {
                        if (g.Enemy == null) continue;
                        for (int k = 0; k < g.Count; k++)
                            spawns.Add(new SpawnEvent { Time = g.StartDelay + k * g.Interval, Type = g.Enemy.PoolId });
                        units += g.Count;
                        if (g.Enemy is BossDataSO) units += summons;
                    }
                }
                spawns.Sort(start, spawns.Count - start, SpawnComparer.Instance);
                _waveEnd[w] = spawns.Count;
                peak = Math.Max(peak, units);   // limite superior da concorrência (GDD §3.4)
            }
            _spawns = spawns.ToArray();
        }

        sealed class SpawnComparer : System.Collections.Generic.IComparer<SpawnEvent>
        {
            public static readonly SpawnComparer Instance = new SpawnComparer();
            public int Compare(SpawnEvent a, SpawnEvent b) => a.Time.CompareTo(b.Time);
        }

        // =====================================================================================
        // Tick (zero-GC)
        // =====================================================================================
        public void Step()
        {
            if (Phase == SimPhase.Won || Phase == SimPhase.Lost || Phase == SimPhase.Loading) return;
            TickCount++;

            // 1. Fase e spawn
            if (Phase == SimPhase.Building || Phase == SimPhase.Intermission)
            {
                if (AutoStartWaves)
                {
                    PhaseTimer -= Dt;
                    if (PhaseTimer <= 0f) StartNextWave(false);
                }
            }
            if (Phase == SimPhase.WaveActive) SpawnDue();

            for (int h = 0; h < HeroCooldown.Length; h++)
                if (HeroCooldown[h] > 0f) HeroCooldown[h] = math.max(0f, HeroCooldown[h] - Dt);

            UpdateBoss();

            // 2–8. Jobs
            JobHandle handle = new StatusMoveJob
            {
                Creeps = Creeps.Items, Active = Creeps.Active, Flow = Flow.View,
                CorePos = _corePos, Dt = Dt, LeakRadius = LeakRadius,
            }.Schedule(Creeps.Capacity, 32);
            handle = Grid.ScheduleBuild(Creeps.Items, Creeps.Active, _hashScratch, handle);
            handle = new TargetingJob
            {
                Towers = Units, Creeps = Creeps.Items,
                CellStart = Grid.CellStart, CellCount = Grid.CellCount_, Entities = Grid.Entities, Origin = Grid.Origin,
            }.Schedule(MaxUnits, 4, handle);
            handle = new FireAndResolveJob
            {
                Towers = Units, Creeps = Creeps.Items, Zones = Zones, Projectiles = Projectiles,
                CellStart = Grid.CellStart, CellCount = Grid.CellCount_, Entities = Grid.Entities,
                Scratch = _queryScratch, Origin = Grid.Origin, Dt = Dt, Seed = (uint)(TickCount * 2654435761u) | 1u,
            }.Schedule(handle);
            handle.Complete();

            // 8b. Soldados do Guardião: seguram, batem e apanham (antes do Commit contar as mortes)
            UpdateSoldiers();

            // 9. Commit
            Commit();

            if (Phase == SimPhase.WaveActive && _cursor >= _waveEnd[WaveNumber - 1] && Creeps.ActiveCount == 0)
                OnWaveCleared();
        }

        void SpawnDue()
        {
            _waveTime += Dt;
            int end = _waveEnd[WaveNumber - 1];
            while (_cursor < end && _spawns[_cursor].Time <= _waveTime)
            {
                // Pool esgotado: o spawn espera um slot (atrasa no máximo alguns ticks, GDD §3.4)
                if (Spawn(_spawns[_cursor].Type, _lairPos) < 0) break;
                _cursor++;
            }
        }

        /// <summary>Aluga um slot de creep. Retorna o índice ou -1 se o pool estiver cheio.</summary>
        public int Spawn(int type, float2 at)
        {
            int idx = Creeps.Rent();
            if (idx < 0) return -1;
            EnemyDataSO def = EnemyTypes[type];
            bool boss = type == _bossType;
            float hp = boss ? BalanceFormulas.BossHp(((BossDataSO)def).HpBase, GlobalLevel)
                            : BalanceFormulas.CreepHp(def.BaseHp, GlobalLevel, math.max(1, WaveNumber), Alpha);
            bool flying = (def.Traits & EnemyTraits.Flying) != 0;
            float2 dir = flying ? math.normalizesafe(_corePos - at) : Flow.View.SampleDirection(at);
            float lane = boss ? 0f : _rng.NextFloat(-LaneOffsetMax, LaneOffsetMax);
            float2 pos = at + new float2(-dir.y, dir.x) * lane;

            Creeps.Items[idx] = new CreepState
            {
                BasePos = at, Pos = pos, PrevPos = pos, Dir = dir, LaneOffset = lane,
                Progress = Flow.View.SampleIntegration(at),
                Hp = hp, MaxHp = hp, Shield = (def.Traits & EnemyTraits.Shield) != 0 ? hp * 0.5f : 0f,
                Armor = def.Armor, MagicResist = def.MagicResist, Speed = def.Speed, Mass = def.Mass,
                Traits = (uint)def.Traits, TypeIndex = type, Serial = ++_serial,
                LeakWeight = def.LeakWeight, Gold = def.KillGold,
                Flags = CreepFlags.Alive, IsBoss = (byte)(boss ? 1 : 0),
            };
            if (boss)
            {
                _bossSlot = idx;
                _bossSerial = _serial;
                _torchTimer = BossTorchInterval();
                BossHpFraction = 1f;
                Events.Push(GameEventType.BossSpawned, idx, type, at, hp);
            }
            return idx;
        }

        void Commit()
        {
            for (int i = 0; i < Creeps.Capacity; i++)
            {
                if (Creeps.Active[i] == 0) continue;
                CreepState c = Creeps.Items[i];
                if ((c.Flags & CreepFlags.Dead) != 0)
                {
                    Gold += c.Gold;
                    Events.Push(GameEventType.Kill, i, c.TypeIndex, c.Pos, c.Gold);
                    if (c.IsBoss != 0) OnBossDefeated(c);
                    Creeps.Return(i);
                }
                else if ((c.Flags & CreepFlags.Leaked) != 0)
                {
                    int lost = c.IsBoss != 0 ? Lives : c.LeakWeight;   // chefe custa todas as vidas (§1.2)
                    LivesLost += lost;
                    if (!InfiniteLives) Lives = math.max(0, Lives - lost);
                    Events.Push(GameEventType.Leak, i, c.TypeIndex, c.Pos, lost);
                    if (c.IsBoss != 0) _bossSlot = -1;
                    Creeps.Return(i);
                }
            }
            if (!InfiniteLives && Lives <= 0 && Phase != SimPhase.Lost)
            {
                Phase = SimPhase.Lost;
                Stars = 0;
                Events.Push(GameEventType.Defeat, 0, 0, _corePos, 0);
            }
        }

        void OnWaveCleared()
        {
            int bonus = Level.Waves[WaveNumber - 1] != null ? Level.Waves[WaveNumber - 1].GetBonusGold(WaveNumber) : BalanceFormulas.WaveGold(WaveNumber);
            Gold += bonus;
            Events.Push(GameEventType.WaveCleared, WaveNumber, 0, _corePos, bonus);
            if (WaveNumber >= WaveCount)
            {
                Phase = SimPhase.Won;
                Stars = BalanceFormulas.Stars(true, LivesLost, StartLives);
                Events.Push(GameEventType.Victory, Stars, 0, _corePos, Stars);
            }
            else
            {
                Phase = SimPhase.Intermission;
                PhaseTimer = IntermissionTime;
            }
        }

        // =====================================================================================
        // Chefe (GDD §9.2)
        // =====================================================================================
        float BossTorchInterval()
        {
            if (_boss == null || _boss.Phases == null) return 0f;
            for (int i = 0; i < _boss.Phases.Length; i++)
                if (_boss.Phases[i].Action == BossAction.DisablePedestal) return _boss.Phases[i].Interval;
            return 0f;
        }

        void UpdateBoss()
        {
            if (_bossSlot < 0 || _boss == null || _boss.Phases == null) return;
            CreepState c = Creeps.Items[_bossSlot];
            if (Creeps.Active[_bossSlot] == 0 || c.Serial != _bossSerial || (c.Flags & CreepFlags.Alive) == 0) return;
            BossHpFraction = c.Hp / c.MaxHp;

            BossPhase[] phases = _boss.Phases;
            for (int i = 0; i < phases.Length; i++)
            {
                BossPhase p = phases[i];
                if (p.Action == BossAction.DisablePedestal && p.HpThreshold >= 1f)
                {
                    UpdateTorch(p);
                    continue;
                }
                if (_bossPhaseFired[i] || BossHpFraction > p.HpThreshold) continue;
                _bossPhaseFired[i] = true;
                Events.Push(GameEventType.BossPhase, i, (int)p.Action, c.Pos, p.HpThreshold);
                switch (p.Action)
                {
                    case BossAction.Summon:
                        if (p.SummonType != null)
                            for (int k = 0; k < p.SummonCount; k++) Spawn(p.SummonType.PoolId, _lairPos);
                        break;
                    case BossAction.Shield:
                        c.Shield += c.MaxHp * p.Value;
                        Creeps.Items[_bossSlot] = c;
                        break;
                }
            }
        }

        /// <summary>Pisão Glacial: telegrafa 1,5 s numa unidade e a congela por Value s.</summary>
        void UpdateTorch(BossPhase p)
        {
            if (_torchPedestal >= 0)
            {
                _torchTelegraph -= Dt;
                if (_torchTelegraph > 0f) return;
                int unit = PedestalUnit[_torchPedestal];
                if (unit >= 0)
                {
                    TowerState t = Units[unit];
                    t.DisabledTimer = p.Value;
                    Units[unit] = t;
                }
                Events.Push(GameEventType.PedestalDisabled, _torchPedestal, unit, PedestalXZ(_torchPedestal), p.Value);
                _torchPedestal = -1;
                _torchTimer = p.Interval;
                return;
            }
            _torchTimer -= Dt;
            if (_torchTimer > 0f) return;

            int occupied = 0;
            for (int i = 0; i < PedestalUnit.Length; i++) if (PedestalUnit[i] >= 0) occupied++;
            if (occupied == 0) { _torchTimer = p.Interval; return; }
            int pick = _rng.NextInt(0, occupied);
            for (int i = 0; i < PedestalUnit.Length; i++)
            {
                if (PedestalUnit[i] < 0) continue;
                if (pick-- == 0) { _torchPedestal = i; break; }
            }
            _torchTelegraph = TorchTelegraph;
            Events.Push(GameEventType.PedestalTelegraph, _torchPedestal, 0, PedestalXZ(_torchPedestal), TorchTelegraph);
        }

        void OnBossDefeated(in CreepState c)
        {
            _bossSlot = -1;
            BossHpFraction = 0f;
            Events.Push(GameEventType.BossDefeated, 0, c.TypeIndex, c.Pos, 0);
            if (Level.UnlocksHero != null)
                Events.Push(GameEventType.HeroRescued, Level.UnlocksHero.Id, 0, c.Pos, 0);
        }

        // =====================================================================================
        // Comandos do jogador (zero-GC)
        // =====================================================================================
        public float2 PedestalXZ(int pedestal) => new float2(Pedestals[pedestal].x, Pedestals[pedestal].z);

        public bool StartNextWave(bool early)
        {
            if (Phase != SimPhase.Building && Phase != SimPhase.Intermission) return false;
            if (WaveNumber >= WaveCount) return false;
            if (early)
            {
                int bonus = math.min(BalanceFormulas.MaxEarlyCallGold, (int)math.ceil(PhaseTimer));   // GDD §8.5
                Gold += bonus;
            }
            WaveNumber++;
            _cursor = _waveFirst[WaveNumber - 1];
            _waveTime = 0f;
            Phase = SimPhase.WaveActive;
            PhaseTimer = 0f;
            Events.Push(GameEventType.WaveStarted, WaveNumber, WaveCount, _lairPos, 0);
            return true;
        }

        public int EarlyCallBonus => (Phase == SimPhase.Building || Phase == SimPhase.Intermission)
            ? math.min(BalanceFormulas.MaxEarlyCallGold, (int)math.ceil(PhaseTimer)) : 0;

        public bool IsArchetypeAllowed(TowerDataSO def) => BalanceFormulas.IsArchetypeUnlocked(def.Archetype, GlobalLevel);

        // ---------------------------------------------------------------- posicionamento livre (zero-GC)
        /// <summary>Distância do ponto ao eixo do caminho (m).</summary>
        public float DistanceToPath(Vector3 p)
        {
            float best = float.MaxValue;
            for (int s = 0; s < _path.Length - 1; s++)
            {
                float ax = _path[s].x, az = _path[s].z;
                float bx = _path[s + 1].x - ax, bz = _path[s + 1].z - az;
                float len2 = bx * bx + bz * bz;
                float t = len2 > 0f ? math.saturate(((p.x - ax) * bx + (p.z - az) * bz) / len2) : 0f;
                float dx = p.x - (ax + bx * t), dz = p.z - (az + bz * t);
                best = math.min(best, dx * dx + dz * dz);
            }
            return math.sqrt(best);
        }

        /// <summary>
        /// Pode construir aqui? Dentro da área do mapa, fora do caminho (com a base inteira), longe do covil e do
        /// Núcleo e sem encostar em outra unidade. <paramref name="ignoreSpot"/>: ponto que está sendo movido.
        /// </summary>
        public bool CanPlaceAt(Vector3 p, int ignoreSpot = -1)
        {
            if (!_playArea.Contains(new Vector2(p.x, p.z))) return false;
            if (_path == null || DistanceToPath(p) < _pathHalfWidth + FootprintRadius) return false;
            float lx = p.x - _lairPos.x, lz = p.z - _lairPos.y, cx = p.x - _corePos.x, cz = p.z - _corePos.y;
            float clear2 = LandmarkClearance * LandmarkClearance;
            if (lx * lx + lz * lz < clear2 || cx * cx + cz * cz < clear2) return false;
            float min2 = 4f * FootprintRadius * FootprintRadius;
            for (int i = 0; i < SpotCount; i++)
            {
                if (i == ignoreSpot || PedestalUnit[i] < 0) continue;
                float dx = p.x - Pedestals[i].x, dz = p.z - Pedestals[i].z;
                if (dx * dx + dz * dz < min2) return false;
            }
            return true;
        }

        /// <summary>Ponto vazio movido (ou criado) em <paramref name="p"/>; -1 se não pode construir ali.</summary>
        public int AcquireSpot(Vector3 p)
        {
            if (!CanPlaceAt(p)) return -1;
            p.y = 0f;
            for (int i = 0; i < SpotCount; i++)
                if (PedestalUnit[i] < 0) { Pedestals[i] = p; return i; }
            if (SpotCount >= MaxSpots) return -1;
            Pedestals[SpotCount] = p;
            return SpotCount++;
        }

        /// <summary>Unidade (ponto ocupado) mais perto de <paramref name="p"/> num raio; -1 se nenhuma.</summary>
        public int OccupiedSpotNear(Vector3 p, float radius)
        {
            int best = -1;
            float bestD = radius * radius;
            for (int i = 0; i < SpotCount; i++)
            {
                if (PedestalUnit[i] < 0) continue;
                float dx = Pedestals[i].x - p.x, dz = Pedestals[i].z - p.z;
                float d = dx * dx + dz * dz;
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public bool CanBuild(int pedestal, int defIndex) =>
            pedestal >= 0 && pedestal < SpotCount && PedestalUnit[pedestal] < 0
            && defIndex >= 0 && defIndex < TowerDefs.Length && IsArchetypeAllowed(TowerDefs[defIndex])
            && Gold >= TowerDefs[defIndex].BaseCost && IsPlaying;

        public bool IsPlaying => Phase == SimPhase.Building || Phase == SimPhase.WaveActive || Phase == SimPhase.Intermission;

        public int BuildTower(int pedestal, int defIndex)
        {
            if (!CanBuild(pedestal, defIndex)) return -1;
            TowerDataSO def = TowerDefs[defIndex];
            int slot = PlaceUnit(PedestalXZ(pedestal), pedestal, defIndex, UnitKind.Tower, default);
            if (slot < 0) return -1;
            Gold -= def.BaseCost;
            Spent[slot] = def.BaseCost;
            PedestalUnit[pedestal] = slot;
            Events.Push(GameEventType.TowerBuilt, slot, pedestal, PedestalXZ(pedestal), def.BaseCost);
            return slot;
        }

        /// <summary>Posiciona uma torre sem custo e fora de pedestal (testes de estresse/ferramentas).</summary>
        public int PlaceTowerFree(float2 pos, int defIndex, UpgradeState upgrades) =>
            PlaceUnit(pos, -1, defIndex, UnitKind.Tower, upgrades);

        int PlaceUnit(float2 pos, int pedestal, int defIndex, byte kind, UpgradeState upgrades)
        {
            for (int i = 0; i < MaxUnits; i++)
            {
                if (Units[i].Active != 0) continue;
                Units[i] = new TowerState { Pos = pos, Pedestal = pedestal, DefIndex = defIndex, Kind = kind, Active = 1, Target = -1 };
                Upgrades[i] = upgrades;
                Spent[i] = 0;
                if (kind == UnitKind.Tower) RecomputeTower(i); else RecomputeHero(i);
                return i;
            }
            return -1;
        }

        public int UpgradeCost(int slot, int path)
        {
            TowerDataSO def = TowerDefs[Units[slot].DefIndex];
            int next = Upgrades[slot].Get(path) + 1;
            return next > UpgradeState.MaxTier ? int.MaxValue : def.GetTierCost(path, next);
        }

        public bool CanUpgrade(int slot, int path) =>
            IsPlaying && slot >= 0 && Units[slot].Active != 0 && Units[slot].Kind == UnitKind.Tower
            && Upgrades[slot].CanUpgrade(path, MaxTier) && Gold >= UpgradeCost(slot, path);

        /// <summary>Upgrade travado por regra (5-2-0 ou onboarding), independente do ouro.</summary>
        public bool IsPathLocked(int slot, int path) => !Upgrades[slot].CanUpgrade(path, MaxTier);

        public bool Upgrade(int slot, int path)
        {
            if (!CanUpgrade(slot, path)) return false;
            int cost = UpgradeCost(slot, path);
            UpgradeState u = Upgrades[slot];
            u.TryUpgrade(path, MaxTier);
            Upgrades[slot] = u;
            Gold -= cost;
            Spent[slot] += cost;
            RecomputeTower(slot);
            Events.Push(GameEventType.TowerUpgraded, slot, path, Units[slot].Pos, u.Get(path));
            return true;
        }

        public int SellValue(int slot) => BalanceFormulas.SellValue(Spent[slot]);

        public void Sell(int slot)
        {
            if (slot < 0 || Units[slot].Active == 0 || Units[slot].Kind != UnitKind.Tower || !IsPlaying) return;
            int value = SellValue(slot);
            Gold += value;
            int pedestal = Units[slot].Pedestal;
            if (pedestal >= 0) PedestalUnit[pedestal] = -1;
            ClearSoldiers(slot);
            Events.Push(GameEventType.TowerSold, slot, pedestal, Units[slot].Pos, value);
            Units[slot] = default;
            Upgrades[slot] = default;
            Spent[slot] = 0;
        }

        public void SetPriority(int slot, TargetPriority priority)
        {
            TowerState t = Units[slot];
            t.Priority = (byte)priority;
            Units[slot] = t;
        }

        void RecomputeTower(int slot)
        {
            TowerState t = Units[slot];
            TowerDataSO def = TowerDefs[t.DefIndex];
            UpgradeState u = Upgrades[slot];
            t.Archetype = (byte)def.Archetype;
            t.Interval = math.max(0.05f, def.GetStat(StatId.Interval, def.Interval, u));
            t.Range = def.GetStat(StatId.Range, def.Range, u);
            t.CritChance = def.GetStat(StatId.CritChance, 0f, u);
            t.HitsFlying = (byte)(def.HitsFlying ? 1 : 0);
            bool camo = def.DetectsCamo;

            HitPayload p = default;
            p.DamageType = (byte)def.DamageType;
            p.Damage = def.GetStat(StatId.Damage, def.Damage, u) * EraPower;
            p.Splash = def.GetStat(StatId.SplashRadius, def.SplashRadius, u);
            p.ArmorIgnore = def.GetStat(StatId.ArmorIgnore, 0f, u);
            p.KnockbackBossMul = 0.25f;

            if (def.Archetype == TowerArchetype.Guard)
            {
                // Quartel: a torre não ataca; o golpe (com os efeitos das melhorias) é de cada soldado.
                p.Damage = def.GetStat(StatId.Damage, def.SoldierDamage, u) * EraPower;
                t.Interval = 1e6f;
            }

            for (int path = 0; path < UpgradeState.PathCount; path++)
            {
                int tier = u.Get(path);
                for (int k = 0; k < tier; k++)
                    if (def.Paths[path].Tiers[k].GrantsCamoDetection) camo = true;
                if (tier == 0) continue;
                SkillEffect[] fx = def.Paths[path].Tiers[tier - 1].OnHitEffects;   // vale o maior tier comprado
                if (fx == null) continue;
                for (int e = 0; e < fx.Length; e++) ApplyOnHitEffect(ref p, fx[e]);
            }
            t.DetectsCamo = (byte)(camo ? 1 : 0);
            t.ProjectileSpeed = ProjectileSpeedFor(def.Archetype);
            t.Payload = p;
            Units[slot] = t;
            if (def.Archetype == TowerArchetype.Guard) SetupSoldiers(slot, def);
        }

        // =====================================================================================
        // Soldados do Guardião (zero-GC: array fixo, laços simples)
        // =====================================================================================
        /// <summary>Ponto do caminho mais perto de <paramref name="p"/> e a direção do trecho.</summary>
        float2 NearestOnPath(float2 p, out float2 tangent)
        {
            float best = float.MaxValue;
            float2 point = p;
            tangent = new float2(1f, 0f);
            for (int s = 0; s < _path.Length - 1; s++)
            {
                float2 a = new float2(_path[s].x, _path[s].z), b = new float2(_path[s + 1].x, _path[s + 1].z);
                float2 ab = b - a;
                float len2 = math.lengthsq(ab);
                if (len2 < 1e-6f) continue;
                float t = math.saturate(math.dot(p - a, ab) / len2);
                float2 q = a + ab * t;
                float d = math.distancesq(p, q);
                if (d < best) { best = d; point = q; tangent = ab / math.sqrt(len2); }
            }
            return point;
        }

        /// <summary>Postos no caminho (no alcance do quartel). Soldados novos saem do quartel um por vez.</summary>
        void SetupSoldiers(int slot, TowerDataSO def)
        {
            TowerState t = Units[slot];
            float2 tangent = new float2(1f, 0f);
            float2 rally = t.Pos;
            if (_path != null) rally = NearestOnPath(t.Pos, out tangent);
            float2 toRally = rally - t.Pos;
            float d = math.length(toRally);
            if (d > t.Range && d > 1e-4f) rally = t.Pos + toRally / d * t.Range;   // caminho longe: fica na borda do alcance
            int count = math.clamp(def.SoldierCount, 1, SoldiersPerGuard);
            float hp = math.max(1f, def.SoldierHp) * EraPower;
            float interval = math.max(0.2f, def.SoldierInterval);
            for (int k = 0; k < SoldiersPerGuard; k++)
            {
                ref SoldierState s = ref Soldiers[slot * SoldiersPerGuard + k];
                if (k >= count) { s = default; continue; }
                s.Post = rally + tangent * ((k - (count - 1) * 0.5f) * SoldierSpacing);
                s.MaxHp = hp;
                s.Interval = interval;
                if (s.Active != 0) continue;            // melhoria: mantém quem já está em campo
                s.Active = 1;
                s.Alive = 0;
                s.Respawn = 0.3f + 0.6f * k;            // saem em fila
                s.Engaged = -1;
                s.Hp = hp;
                s.Pos = s.PrevPos = t.Pos;
            }
        }

        void ClearSoldiers(int slot)
        {
            for (int k = 0; k < SoldiersPerGuard; k++) Soldiers[slot * SoldiersPerGuard + k] = default;
        }

        /// <summary>
        /// Alvo do soldado dentro do alcance do Guardião (<paramref name="center"/>, <paramref name="radius"/>):
        /// primeiro um inimigo LIVRE (o soldado o segura e apanha dele); se não houver, ajuda outro soldado no
        /// inimigo já segurado mais perto (ou no chefe) — bate sem apanhar, porque o inimigo luta com quem segura.
        /// </summary>
        int FindBlockTarget(float2 center, float radius, float2 from, out int serial, out byte holder)
        {
            int free = FindFreeTarget(center, radius, from, out serial);
            holder = 1;
            if (free >= 0) return free;
            holder = 0;
            serial = 0;
            int best = -1;
            float r2 = radius * radius, bestD = float.MaxValue;
            var items = Creeps.Items;
            for (int i = 0; i < Creeps.Capacity; i++)
            {
                if (Creeps.Active[i] == 0) continue;
                CreepState c = items[i];
                if ((c.Flags & CreepFlags.Alive) == 0 || (c.Traits & (uint)EnemyTraits.Flying) != 0) continue;
                if (c.Blocked == 0 && c.IsBoss == 0) continue;   // só reforça quem já está em combate
                if (math.distancesq(c.Pos, center) > r2) continue;
                float d = math.distancesq(c.Pos, from);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0)
            {
                serial = items[best].Serial;
                if (items[best].IsBoss != 0) holder = 1;   // o chefe pisoteia todo mundo em volta
            }
            return best;
        }

        /// <summary>Inimigo livre (não segurado, não voador) no alcance, o mais perto do soldado; reivindica.</summary>
        int FindFreeTarget(float2 center, float radius, float2 from, out int serial)
        {
            serial = 0;
            int best = -1;
            float r2 = radius * radius, bestD = float.MaxValue;
            var items = Creeps.Items;
            for (int i = 0; i < Creeps.Capacity; i++)
            {
                if (Creeps.Active[i] == 0) continue;
                CreepState c = items[i];
                if ((c.Flags & CreepFlags.Alive) == 0 || c.Blocked != 0) continue;
                if ((c.Traits & (uint)EnemyTraits.Flying) != 0) continue;
                if (math.distancesq(c.Pos, center) > r2) continue;
                float d = math.distancesq(c.Pos, from);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return -1;
            CreepState chosen = items[best];
            serial = chosen.Serial;
            if (chosen.IsBoss == 0) { chosen.Blocked = 1; items[best] = chosen; }
            return best;
        }

        /// <summary>
        /// Dano/s que um inimigo causa no soldado que o segura. Cresce junto com a vida do inimigo (fase, onda,
        /// Era), para os soldados não ficarem fortes demais no fim do jogo.
        /// </summary>
        public float CreepMeleeDps(in CreepState c)
        {
            float baseHp = EnemyTypes[c.TypeIndex].BaseHp;
            float scale = baseHp > 0f ? c.MaxHp / baseHp : EraPower;
            return (CreepMeleeBase + CreepMeleePerMass * c.Mass) * scale;
        }

        void UpdateSoldiers()
        {
            var items = Creeps.Items;
            var active = Creeps.Active;
            // Bloqueio vale só enquanto alguém segura: limpa e reivindica de novo a cada tick.
            for (int i = 0; i < Creeps.Capacity; i++)
                if (active[i] != 0 && items[i].Blocked != 0) { CreepState c = items[i]; c.Blocked = 0; items[i] = c; }

            // Passe 1: confirma quem continua segurando quem (antes de procurar alvos novos).
            for (int s = 0; s < Soldiers.Length; s++)
            {
                ref SoldierState so = ref Soldiers[s];
                if (so.Active == 0 || so.Alive == 0 || so.Engaged < 0) continue;
                int e = so.Engaged;
                if (active[e] == 0 || items[e].Serial != so.EngagedSerial || (items[e].Flags & CreepFlags.Alive) == 0) { so.Engaged = -1; continue; }
                TowerState owner = Units[s / SoldiersPerGuard];
                float leash = owner.Range + SoldierLeash;
                if (math.distancesq(items[e].Pos, owner.Pos) > leash * leash) { so.Engaged = -1; continue; }   // saiu do alcance
                if (items[e].IsBoss != 0) { so.Holder = 1; continue; }
                // O primeiro da fila segura (e apanha); os outros no mesmo inimigo só ajudam.
                if (items[e].Blocked == 0) { CreepState c = items[e]; c.Blocked = 1; items[e] = c; so.Holder = 1; }
                else so.Holder = 0;
            }

            // Passe 2: renascer, andar, bater e apanhar.
            for (int s = 0; s < Soldiers.Length; s++)
            {
                ref SoldierState so = ref Soldiers[s];
                if (so.Active == 0) continue;
                TowerState tower = Units[s / SoldiersPerGuard];
                if (tower.Active == 0) { so = default; continue; }
                so.PrevPos = so.Pos;
                if (so.Swing > 0f) so.Swing -= Dt;
                if (so.Alive == 0)
                {
                    so.Respawn -= Dt;
                    if (so.Respawn > 0f) continue;
                    so.Alive = 1;
                    so.Hp = so.MaxHp;
                    so.Engaged = -1;
                    so.AttackTimer = 0f;
                    so.Pos = so.PrevPos = tower.Pos;   // sai do quartel
                    continue;
                }

                if (so.Engaged < 0) so.Engaged = FindBlockTarget(tower.Pos, tower.Range, so.Pos, out so.EngagedSerial, out so.Holder);
                else if (so.Holder == 0)
                {
                    // Ajudante: se aparecer um inimigo livre no alcance, larga o reforço e vai segurá-lo.
                    int free = FindFreeTarget(tower.Pos, tower.Range, so.Pos, out int freeSerial);
                    if (free >= 0) { so.Engaged = free; so.EngagedSerial = freeSerial; so.Holder = 1; }
                }
                float2 goal = so.Post;
                if (so.Engaged >= 0)
                {
                    float2 cp = items[so.Engaged].Pos;
                    goal = cp + math.normalizesafe(so.Pos - cp, new float2(0f, 1f)) * 1.0f;   // encosta no inimigo
                }
                float2 delta = goal - so.Pos;
                float dist = math.length(delta), step = SoldierSpeed * Dt;
                so.Pos = dist <= step ? goal : so.Pos + delta / dist * step;

                if (so.Engaged < 0) { so.Hp = math.min(so.MaxHp, so.Hp + SoldierRegen * Dt); continue; }
                int e2 = so.Engaged;
                CreepState creep = items[e2];
                if (math.distance(so.Pos, creep.Pos) > SoldierReach) continue;
                so.AttackTimer -= Dt;
                if (so.AttackTimer <= 0f)
                {
                    DamageCalculator.ApplyHit(ref creep, tower.Payload);
                    items[e2] = creep;
                    so.AttackTimer += so.Interval;
                    so.Swing = 0.25f;
                }
                if (so.Holder != 0) so.Hp -= CreepMeleeDps(creep) * Dt;   // revida em quem segura
                if (so.Hp <= 0f)
                {
                    so.Alive = 0;
                    so.Respawn = SoldierRespawn;
                    so.Engaged = -1;
                }
            }
        }

        void ApplyOnHitEffect(ref HitPayload p, in SkillEffect fx)
        {
            switch (fx.Type)
            {
                case EffectType.DoT:
                    p.DotDps = fx.Magnitude * EraPower;
                    p.DotDuration = fx.Duration;
                    p.DotType = (byte)fx.DamageType;
                    break;
                case EffectType.Knockback:
                    p.Knockback = fx.Magnitude;
                    p.KnockbackBossMul = fx.BossMultiplier;
                    break;
                case EffectType.Stun:
                    p.StunDuration = fx.Duration;
                    break;
                case EffectType.Shred:
                    p.ArmorIgnore = math.max(p.ArmorIgnore, fx.Magnitude);
                    break;
            }
        }

        /// <summary>PROV: velocidade de projétil por arquétipo (m/s). 0 = acerto instantâneo.</summary>
        static float ProjectileSpeedFor(TowerArchetype a)
        {
            switch (a)
            {
                case TowerArchetype.Marksman: return 22f;
                case TowerArchetype.Launcher: return 11f;
                case TowerArchetype.Alchemist: return 16f;
                default: return 0f;
            }
        }

        // =====================================================================================
        // Heróis (GDD §6.1)
        // =====================================================================================
        public bool HeroesAllowed(bool devOverride) => devOverride || BalanceFormulas.HeroSlotsForLevel(GlobalLevel) > 0;

        public bool CanSummonHero(int hero, int pedestal) =>
            IsPlaying && hero >= 0 && hero < Loadout.Length && HeroUnit[hero] < 0
            && pedestal >= 0 && pedestal < SpotCount && PedestalUnit[pedestal] < 0
            && Gold >= Loadout[hero].SummonCost;

        public int SummonHero(int hero, int pedestal)
        {
            if (!CanSummonHero(hero, pedestal)) return -1;
            int slot = PlaceUnit(PedestalXZ(pedestal), pedestal, hero, UnitKind.Hero, default);
            if (slot < 0) return -1;
            Gold -= Loadout[hero].SummonCost;
            PedestalUnit[pedestal] = slot;
            HeroUnit[hero] = slot;
            Events.Push(GameEventType.HeroSummoned, hero, pedestal, PedestalXZ(pedestal), 0);
            return slot;
        }

        /// <summary>Posiciona um herói sem custo e fora de pedestal (testes de estresse).</summary>
        public int PlaceHeroFree(int hero, float2 pos)
        {
            int slot = PlaceUnit(pos, -1, hero, UnitKind.Hero, default);
            if (slot >= 0) HeroUnit[hero] = slot;
            return slot;
        }

        void RecomputeHero(int slot)
        {
            TowerState t = Units[slot];
            HeroDataSO h = Loadout[t.DefIndex];
            t.Interval = math.max(0.05f, h.AttackInterval);
            // Simplificação do MVP: o herói ataca do pedestal tudo o que entra no raio de guarda.
            t.Range = math.max(h.AttackRange, h.GuardRadius);
            t.HitsFlying = (byte)(h.AttackRange >= 3f ? 1 : 0);
            t.ProjectileSpeed = h.AttackRange >= 3f ? 18f : 0f;
            t.Archetype = 255;
            HitPayload p = default;
            p.DamageType = (byte)h.AttackType;
            p.Damage = h.AttackDamage * EraPower;
            p.KnockbackBossMul = 0.25f;
            t.Payload = p;
            if (p.Damage <= 0f) t.Range = 0f;
            Units[slot] = t;
        }

        public float HeroSkillCooldownMax(int hero) => Loadout[hero].Skill != null ? Loadout[hero].Skill.Cooldown : 0f;

        public bool CanCastHeroSkill(int hero) =>
            IsPlaying && hero >= 0 && hero < Loadout.Length && HeroUnit[hero] >= 0
            && HeroCooldown[hero] <= 0f && Loadout[hero].Skill != null;

        /// <summary>SkillSystem do MVP: interpreta o array de SkillEffect (GDD §10.3).</summary>
        public bool CastHeroSkill(int hero, float2 point)
        {
            if (!CanCastHeroSkill(hero)) return false;
            SkillDataSO s = Loadout[hero].Skill;
            if (s.Targeting == SkillTargeting.Self || s.Targeting == SkillTargeting.Global)
                point = Units[HeroUnit[hero]].Pos;
            float radius = s.Targeting == SkillTargeting.Global ? 1000f : s.Radius;

            float crowd = 0f;
            for (int i = 0; i < s.Effects.Length; i++)
                if (s.Effects[i].Type == EffectType.CrowdBonus) crowd = s.Effects[i].Magnitude;

            for (int i = 0; i < s.Effects.Length; i++)
            {
                SkillEffect fx = s.Effects[i];
                if (fx.Type == EffectType.DoT && s.Duration > 0f)
                {
                    OpenZone(point, radius, s.Duration, fx.Magnitude * EraPower, (byte)fx.DamageType, crowd);
                    continue;
                }
                ApplyInstantEffect(fx, point, radius);
            }
            HeroCooldown[hero] = s.Cooldown;
            Events.Push(GameEventType.SkillCast, hero, s.Id, point, radius);
            return true;
        }

        void OpenZone(float2 center, float radius, float duration, float dps, byte type, float crowdPerEnemy)
        {
            for (int z = 0; z < MaxZones; z++)
            {
                if (Zones[z].Active != 0) continue;
                Zones[z] = new SkillZone
                {
                    Center = center, Radius = radius, Timer = duration, Duration = duration, Dps = dps, DamageType = type,
                    CrowdBonusPerEnemy = crowdPerEnemy, CrowdBonusMax = CrowdBonusCap, Active = 1,
                };
                return;
            }
        }

        void ApplyInstantEffect(in SkillEffect fx, float2 center, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < Creeps.Capacity; i++)
            {
                if (Creeps.Active[i] == 0) continue;
                CreepState c = Creeps.Items[i];
                if ((c.Flags & CreepFlags.Alive) == 0 || math.distancesq(c.Pos, center) > r2) continue;
                if (fx.RequiresTraits != EnemyTraits.None && (c.Traits & (uint)fx.RequiresTraits) == 0) continue;
                float bossMul = c.IsBoss != 0 ? fx.BossMultiplier : 1f;
                switch (fx.Type)
                {
                    case EffectType.Damage:
                        DamageCalculator.ApplyDamage(ref c, DamageCalculator.AgainstCreep(c, fx.Magnitude * EraPower, (byte)fx.DamageType, false, 0f));
                        break;
                    case EffectType.Slow: DamageCalculator.ApplySlow(ref c, fx.Magnitude * bossMul, fx.Duration); break;
                    case EffectType.Stun: DamageCalculator.ApplyStun(ref c, fx.Duration); break;
                    case EffectType.Knockback: c.PendingKnockback += fx.Magnitude * bossMul; break;
                    case EffectType.Vulnerability: c.Vuln = math.max(c.Vuln, fx.Magnitude); c.VulnTimer = math.max(c.VulnTimer, fx.Duration); break;
                    case EffectType.Shred:
                        if (fx.DamageType != DamageType.Magical) c.ShredArmor = math.max(c.ShredArmor, fx.Magnitude);
                        if (fx.DamageType != DamageType.Physical) c.ShredMagic = math.max(c.ShredMagic, fx.Magnitude);
                        c.ShredTimer = math.max(c.ShredTimer, fx.Duration);
                        break;
                    case EffectType.RemoveShield: c.Shield = 0f; break;
                }
                Creeps.Items[i] = c;
            }
        }

        public void Dispose()
        {
            Creeps.Dispose();
            Projectiles.Dispose();
            if (Units.IsCreated) Units.Dispose();
            if (Zones.IsCreated) Zones.Dispose();
            Grid.Dispose();
            if (_hashScratch.IsCreated) _hashScratch.Dispose();
            if (_queryScratch.IsCreated) _queryScratch.Dispose();
            Flow?.Dispose();
            Flow = null;
        }
    }
}
