// Posicionamento livre de torres e heróis (26/11, no estilo do BTD6): sem pedestais.
// Fluxo: toca o cartão na loja → a prévia (alcance) segue o mouse; no toque, aparece onde o dedo tocou →
// toca de novo no mesmo lugar (ou clica, no PC) para construir. Azul = pode; vermelho = caminho, covil,
// Núcleo, borda ou outra unidade. Tocar numa unidade abre as melhorias.
// Entrada: raio da câmera × plano y=0 (GDD §12.3, sem física). Zero-GC: só laços sobre arrays fixos.
using ChronoDefense.Data;
using ChronoDefense.Systems;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChronoDefense.UI
{
    public sealed class TowerPlacer : MonoBehaviour
    {
        public enum Mode : byte { None, PlaceHero, AimSkill, PlaceTower }
        /// <summary>Toque a até esta distância de uma unidade a seleciona (m).</summary>
        public const float PickRadius = 2.0f;
        /// <summary>No toque, um segundo toque a até esta distância da prévia confirma (m).</summary>
        public const float ConfirmRadius = 1.5f;
        public static readonly Color InvalidColor = new Color(1f, 0.32f, 0.28f, 1f);

        CombatSimulation _sim;
        LevelRenderer _renderer;
        RangeIndicator _range;
        CameraRig _rig;
        HudController _hud;

        public int SelectedPedestal { get; private set; } = -1;
        public Mode CurrentMode { get; private set; }
        public int ModeHero { get; private set; } = -1;
        /// <summary>Torre escolhida na loja (Mode.PlaceTower).</summary>
        public int ModeTower { get; private set; } = -1;
        bool _hasPreview;
        Vector3 _preview;
        float[] _lastCooldown;       // por unidade: a recarga só sobe quando ela dispara → recuo/pose de ataque

        public void Init(CombatSimulation sim, LevelRenderer renderer, RangeIndicator range, CameraRig rig, HudController hud)
        {
            _sim = sim;
            _renderer = renderer;
            _range = range;
            _rig = rig;
            _hud = hud;
            _lastCooldown = new float[CombatSimulation.MaxUnits];
        }

        public int SelectedUnit => SelectedPedestal >= 0 ? _sim.PedestalUnit[SelectedPedestal] : -1;
        public bool HasPreview => _hasPreview;

        // ------------------------------------------------------------------ entrada
        public void HandleTap(Vector2 screen)
        {
            if (_sim == null || !_sim.IsPlaying) return;
            if (!_rig.ScreenToGround(screen, out Vector3 ground)) return;
            ground.y = 0f;

            if (CurrentMode == Mode.AimSkill)
            {
                // Toque: 1º mostra onde cai (círculo laranja); 2º no mesmo lugar lança. Mouse: clica e lança.
                bool confirm = _hasPreview && math.distance(new float2(_preview.x, _preview.z), new float2(ground.x, ground.z)) <= ConfirmRadius;
                if (!confirm) { SetPreview(ground); return; }
                int hero = ModeHero;
                CancelMode();
                _sim.CastHeroSkill(hero, new float2(_preview.x, _preview.z));
                return;
            }

            if (CurrentMode == Mode.PlaceTower || CurrentMode == Mode.PlaceHero)
            {
                // Toque: 1º mostra a prévia; 2º no mesmo lugar confirma. Mouse: a prévia já está sob o cursor.
                bool confirm = _hasPreview && math.distance(new float2(_preview.x, _preview.z), new float2(ground.x, ground.z)) <= ConfirmRadius;
                if (!confirm) { SetPreview(ground); return; }
                TryPlace(_preview);
                return;
            }

            int spot = _sim.OccupiedSpotNear(ground, PickRadius);
            if (spot < 0 || spot == SelectedPedestal) Deselect();
            else Select(spot);
        }

        void TryPlace(Vector3 at)
        {
            int spot = _sim.AcquireSpot(at);
            if (spot < 0) { SetPreview(at); return; }   // inválido: a prévia continua vermelha
            _renderer.MoveSpot(spot, _sim.Pedestals[spot]);
            if (CurrentMode == Mode.PlaceHero)
            {
                int hero = ModeHero;
                CancelMode();
                if (_sim.SummonHero(hero, spot) >= 0) Select(spot);
                return;
            }
            int def = ModeTower;
            CancelMode();
            if (_sim.BuildTower(spot, def) >= 0) Select(spot);
        }

        void SetPreview(Vector3 at)
        {
            _hasPreview = true;
            _preview = at;
            RefreshRange();
        }

        // ------------------------------------------------------------------ seleção
        public void Select(int spot)
        {
            CancelMode();
            SelectedPedestal = spot;
            RefreshRange();
            _hud.ShowContext(spot);
        }

        public void Deselect()
        {
            SelectedPedestal = -1;
            _range.Hide();
            _hud.ShowContext(-1);
        }

        public static readonly Color SkillColor = new Color(1f, 0.55f, 0.15f, 1f);

        void RefreshRange()
        {
            if (CurrentMode == Mode.AimSkill)
            {
                if (!_hasPreview) { _range.Hide(); return; }
                var skill = _sim.Loadout[ModeHero].Skill;
                _range.Show(_preview, skill != null ? skill.Radius : 3f, SkillColor);   // área exata onde o fogo cai
                return;
            }
            if (CurrentMode == Mode.PlaceTower || CurrentMode == Mode.PlaceHero)
            {
                if (!_hasPreview) { _range.Hide(); return; }
                float r = CurrentMode == Mode.PlaceTower ? _sim.TowerDefs[ModeTower].Range
                        : math.max(_sim.Loadout[ModeHero].AttackRange, _sim.Loadout[ModeHero].GuardRadius);
                _range.Show(_preview, r, _sim.CanPlaceAt(_preview) ? RangeIndicator.PreviewColor : InvalidColor);
                return;
            }
            if (SelectedPedestal < 0) { _range.Hide(); return; }
            int unit = SelectedUnit;
            if (unit >= 0) _range.Show(_renderer.GetPedestalPosition(SelectedPedestal), _sim.Units[unit].Range, RangeIndicator.ValidColor);
            else _range.Hide();
        }

        // ------------------------------------------------------------------ ações (chamadas pelo HUD)
        /// <summary>Cartão da loja: entra no modo de posicionamento (tocar de novo no mesmo cartão cancela).</summary>
        public void BeginTowerPlacement(int defIndex)
        {
            if (CurrentMode == Mode.PlaceTower && ModeTower == defIndex) { CancelMode(); return; }
            if (!_sim.IsPlaying || defIndex < 0 || defIndex >= _sim.TowerDefs.Length) return;
            if (!_sim.IsArchetypeAllowed(_sim.TowerDefs[defIndex]) || _sim.Gold < _sim.TowerDefs[defIndex].BaseCost) return;
            Deselect();
            CurrentMode = Mode.PlaceTower;
            ModeTower = defIndex;
            _hasPreview = false;
        }

        /// <summary>Constrói no ponto selecionado, se estiver vazio (testes e ferramentas).</summary>
        public void Build(int defIndex)
        {
            if (SelectedPedestal < 0) return;
            if (_sim.BuildTower(SelectedPedestal, defIndex) >= 0) Select(SelectedPedestal);
        }

        public void Upgrade(int path)
        {
            int unit = SelectedUnit;
            if (unit >= 0 && _sim.Upgrade(unit, path)) Select(SelectedPedestal);
        }

        public void Sell()
        {
            int unit = SelectedUnit;
            if (unit < 0) return;
            _sim.Sell(unit);
            Deselect();
        }

        public void CyclePriority()
        {
            int unit = SelectedUnit;
            if (unit < 0) return;
            var next = (TargetPriority)((_sim.Units[unit].Priority + 1) % 4);
            _sim.SetPriority(unit, next);
            _hud.ShowContext(SelectedPedestal);
        }

        public void BeginHeroPlacement(int hero)
        {
            Deselect();
            CurrentMode = Mode.PlaceHero;
            ModeHero = hero;
            _hasPreview = false;
        }

        public void BeginSkillAim(int hero)
        {
            Deselect();
            CurrentMode = Mode.AimSkill;
            ModeHero = hero;
            _hasPreview = false;
        }

        public void CancelMode()
        {
            bool placing = CurrentMode == Mode.PlaceTower || CurrentMode == Mode.PlaceHero || CurrentMode == Mode.AimSkill;
            CurrentMode = Mode.None;
            ModeHero = -1;
            ModeTower = -1;
            _hasPreview = false;
            if (placing && _range != null && SelectedPedestal < 0) _range.Hide();
        }

        // ------------------------------------------------------------------ eventos da simulação
        public void OnGameEvent(in GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.TowerBuilt:
                case GameEventType.TowerUpgraded:
                {
                    int unit = e.A;
                    TowerState t = _sim.Units[unit];
                    if (t.Pedestal < 0) break;
                    UpgradeState u = _sim.Upgrades[unit];
                    _renderer.MoveSpot(t.Pedestal, _sim.Pedestals[t.Pedestal]);
                    _renderer.ShowTowerStage(t.Pedestal, (TowerArchetype)t.Archetype, u.DominantPath, u.VisualStage);
                    if (t.Pedestal == SelectedPedestal) RefreshRange();
                    break;
                }
                case GameEventType.TowerSold:
                    if (e.B >= 0) _renderer.ClearPedestal(e.B);
                    break;
                case GameEventType.HeroSummoned:
                    _renderer.MoveSpot(e.B, _sim.Pedestals[e.B]);
                    _renderer.ShowHero(e.B, e.A);
                    _renderer.PlayHeroPose(e.A, HeroPose.Move, HeroPoseView.MoveHold);   // chegando ao lugar
                    break;
                case GameEventType.SkillCast:
                {
                    _renderer.PlayHeroPose(e.A, HeroPose.Skill, HeroPoseView.SkillHold);
                    int heroUnit = e.A >= 0 && e.A < _sim.HeroUnit.Length ? _sim.HeroUnit[e.A] : -1;
                    if (heroUnit >= 0 && _sim.Units[heroUnit].Pedestal >= 0)
                        _renderer.PlaySkillThrow(_sim.Units[heroUnit].Pedestal, new Vector3(e.Position.x, 0f, e.Position.z));
                    break;
                }
                case GameEventType.Victory:
                    for (int h = 0; h < _sim.HeroUnit.Length; h++) _renderer.PlayHeroPose(h, HeroPose.Victory, 0f);
                    break;
                case GameEventType.PedestalTelegraph:
                    _renderer.ShowPedestalWarning(e.A, true);
                    break;
                case GameEventType.PedestalDisabled:
                    _renderer.ShowPedestalWarning(e.A, false);
                    break;
            }
        }

        void Update()
        {
            if (_sim == null || _renderer == null || _renderer.PedestalCount == 0) return;

            // PC: a prévia segue o cursor. Sobre a interface (loja), o raio cai fora do mapa e a prévia some.
            if ((CurrentMode == Mode.PlaceTower || CurrentMode == Mode.PlaceHero || CurrentMode == Mode.AimSkill) && Mouse.current != null
                && (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled
                    || UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0)
                && _rig.ScreenToGround(Mouse.current.position.ReadValue(), out Vector3 hover))
            {
                hover.y = 0f;
                if (!_hasPreview || (hover - _preview).sqrMagnitude > 0.0025f) SetPreview(hover);
            }

            int n = math.min(_sim.SpotCount, _renderer.PedestalCount);
            for (int p = 0; p < n; p++)
            {
                int unit = _sim.PedestalUnit[p];
                bool disabled = unit >= 0 && _sim.Units[unit].DisabledTimer > 0f;
                PedestalVisualState s;
                if (unit < 0) s = PedestalVisualState.Free;
                else if (p == SelectedPedestal) s = PedestalVisualState.Selected;
                else if (disabled) s = PedestalVisualState.Disabled;
                else if (_sim.Units[unit].Kind == UnitKind.Tower && AnyUpgradeAffordable(unit)) s = PedestalVisualState.UpgradeAvailable;
                else s = PedestalVisualState.Occupied;
                _renderer.SetPedestalState(p, s);
                if (unit < 0) continue;
                _renderer.SetUnitDimmed(p, disabled);

                // Movimento: recuo a cada disparo (pose de ataque no herói) e giro até o alvo.
                TowerState t = _sim.Units[unit];
                if (t.Cooldown > _lastCooldown[unit] + 0.01f)
                {
                    _renderer.Throw(p, t.Interval);   // item sai da mão e é recarregado
                    if (t.Kind == UnitKind.Hero) _renderer.PlayHeroPose(t.DefIndex, HeroPose.Attack, HeroPoseView.AttackHold);
                }
                _lastCooldown[unit] = t.Cooldown;
                if (t.Target >= 0 && t.Target < _sim.Creeps.Capacity && _sim.Creeps.Active[t.Target] != 0)
                    _renderer.SetAim(p, _sim.Creeps.Items[t.Target].Pos);
                else
                    _renderer.ClearAim(p);
            }
        }

        bool AnyUpgradeAffordable(int unit)
        {
            for (int path = 0; path < UpgradeState.PathCount; path++)
                if (_sim.CanUpgrade(unit, path)) return true;
            return false;
        }
    }
}
