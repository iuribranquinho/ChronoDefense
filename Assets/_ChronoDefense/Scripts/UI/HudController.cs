// HUD de combate no estilo do Bloons TD 6 (Referencia_BTD6.md, Fase B), construído em código com uGUI + TMP.
//  - Por cima do mapa, sem painéis: vidas e ouro (sup. esquerda), onda e pausa (sup. direita do mapa).
//  - Coluna da loja à direita (CameraRig.ShopFraction), moldura de madeira: cartão do herói e cartões das torres
//    (retrato + preço). Toca o cartão → toca o chão fora do caminho → a unidade aparece (TowerPlacer).
//  - Canto inferior direito: raio amarelo (habilidade do herói) e botão verde (iniciar onda / acelerar).
//  - Painel de melhorias à esquerda ao tocar numa unidade: 3 caminhos, alvo e vender.
// Zero-GC no loop: valores só são reescritos quando mudam, sempre via TMP_Text.SetText("{0}", v).
// Textos fixos em PT-BR até as tabelas de Localization existirem.
using ChronoDefense.Core;
using ChronoDefense.Data;
using ChronoDefense.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChronoDefense.UI
{
    public sealed class HudController : MonoBehaviour
    {
        public static readonly int[] Speeds = { 1, 3 };   // BTD6: um botão alterna normal ↔ rápido
        public const int FastSpeed = 3;

        // Paleta da interface (madeira, verde de ação, amarelo de destaque — leitura do BTD6 com a nossa arte)
        static readonly Color Wood = new Color32(139, 90, 43, 255), WoodDark = new Color32(92, 56, 24, 255);
        static readonly Color CardColor = new Color32(110, 70, 34, 255), CardHot = new Color32(255, 205, 60, 255);
        static readonly Color CardOff = new Color32(80, 64, 52, 255);
        static readonly Color Green = new Color32(76, 187, 23, 255), GreenDark = new Color32(36, 110, 10, 255);
        static readonly Color Blue = new Color32(43, 108, 176, 255), Red = new Color32(214, 64, 52, 255);
        static readonly Color Yellow = new Color32(255, 196, 30, 255);
        static readonly Color PanelBlue = new Color32(38, 86, 140, 235);
        static readonly Color CostOk = Color.white;
        static readonly Color CostBad = new Color(1f, 0.42f, 0.36f);
        static readonly Color Ink = new Color32(18, 14, 10, 255);

        static readonly string[] ArchetypeNames = { "Atirador", "Lançador", "Alquimista", "Guarda" };
        // Nomes dos campeões da Era 1 (modelos TWR_E1_* do Tripo: caçador de lanças, catapulta, xamã, guardião).
        static readonly string[] ChampionNames = { "Caçador", "Catapulta de Pedra", "Xamã", "Guardião" };
        const string HeroName = "Krog, o Primordial";
        static readonly string[] ArchetypeSubtitles = { "Atirador · longo alcance", "Lançador · dano em área", "Alquimista · magia", "Guarda · bloqueia o caminho" };
        static readonly string[,] PathNames =
        {
            { "Precisão", "Rajada", "Batedor" },
            { "Impacto", "Incendiário", "Concussão" },
            { "Arcano", "Corrosão", "Transmutação" },
            { "Veteranos", "Fúria", "Formação" },
        };
        static readonly string[] PriorityNames = { "Alvo: Primeiro", "Alvo: Último", "Alvo: Mais forte", "Alvo: Mais perto" };

        string _bossToast = "";   // montada uma vez na construção do HUD (zero-GC no combate)

        /// <summary>Nome de exibição do chefe a partir do nome do asset (PROV até haver LocalizedString no BossDataSO).</summary>
        static string BossDisplayName(string asset) =>
            asset.Contains("Mammoth") ? "Mamute-Lanudo Alfa" : asset.Contains("Krog") ? "Krog Corrompido" : asset;

        GameBootstrap _game;
        CombatSimulation _sim;
        TowerPlacer _placer;
        ArtLibrary _art;
        TMP_FontAsset _font;

        RectTransform _safe;
        Rect _lastSafeArea;

        TMP_Text _gold, _lives, _wave, _toast, _hint, _endTitle, _endStars, _nextInfo;
        GameObject _bossBar, _endPanel, _hintGo, _pausePanel;
        Image _bossFill, _playBg, _playIcon, _pauseBg;

        // Loja
        TMP_Text _shopTitle, _shopSubtitle;
        int _shownShopSel = int.MinValue;
        RawImage _unitPhoto;
        Image[] _cardBg;
        RawImage[] _cardIcon;        // arte do usuário (UiArt.TowerIcon); null = cartão procedural
        TMP_Text[] _cardCost;
        Image _heroCardBg;
        TMP_Text _heroCardCost;
        GameObject _heroCard;

        // Habilidade do herói
        GameObject _skillButton;
        Image _skillBg, _skillCooldown;
        RawImage _skillArt, _skillGlow;   // arte do usuário (UiArt.SkillReady/Glow); null = botão procedural
        TMP_Text _skillCdText;
        int _skillCdShown = -1;
        float _skillFlash;
        bool _heroLocked;

        // Painel de melhorias
        GameObject _unitPanel;
        TMP_Text _unitTitle, _sellLabel, _priorityLabel;
        GameObject _sellButton;
        Button[] _pathButtons;
        Image[] _pathBg;
        TMP_Text[] _pathName, _pathInfo;
        GameObject[] _pathRows;

        int _shownGold = int.MinValue, _shownLives = int.MinValue, _shownWave = -1, _shownCountdown = -1, _shownBonus = -1;
        int _shownHint = -1, _heroCardState = -1;
        float _toastTimer;
        int _contextPedestal = -1;

        public void Init(GameBootstrap game, CombatSimulation sim, TowerPlacer placer, ArtLibrary art, bool heroUnlocked)
        {
            _game = game;
            _sim = sim;
            _placer = placer;
            _art = art;
            _font = TMP_Settings.defaultFontAsset;
            _heroLocked = !heroUnlocked;
            BuildLayout();
            ShowContext(-1);
        }

        // ================================================================== construção (carregamento)
        void BuildLayout()
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;   // altura manda: botões do mesmo tamanho em 16:9 e 20:9
            canvasGo.AddComponent<GraphicRaycaster>();

            _safe = NewRect("SafeArea", canvasGo.transform);
            Stretch(_safe);
            ApplySafeArea();

            // Área do mapa (tudo menos a loja) e coluna da loja, com as mesmas frações da câmera.
            var map = NewRect("MapArea", _safe);
            map.anchorMin = Vector2.zero;
            map.anchorMax = new Vector2(1f - CameraRig.ShopFraction, 1f);
            map.offsetMin = map.offsetMax = Vector2.zero;

            // ---- Superior esquerda: vidas e ouro, por cima do mapa (sem painel)
            Icon(map, _art.Heart, Anchor.TopLeft, new Vector2(22, -18), 70);
            _lives = Label(map, "20", 60, TextAlignmentOptions.Left, Anchor.TopLeft, new Vector2(100, -14), new Vector2(170, 80));
            Icon(map, _art.Coin, Anchor.TopLeft, new Vector2(270, -18), 70);
            _gold = Label(map, "0", 60, TextAlignmentOptions.Left, Anchor.TopLeft, new Vector2(348, -14), new Vector2(260, 80));

            // ---- Superior direita do mapa: onda e pausa
            _wave = Label(map, "Onda 0/0", 48, TextAlignmentOptions.Right, Anchor.TopRight, new Vector2(-120, -14), new Vector2(360, 60));
            _nextInfo = Label(map, "", 28, TextAlignmentOptions.Right, Anchor.TopRight, new Vector2(-120, -72), new Vector2(420, 40));
            var pause = RoundButton(map, "Pause", Anchor.TopRight, new Vector2(-106, -16), 88, Blue, TogglePause, out _pauseBg);
            ButtonText(pause, "II", 40);

            // ---- Barra do chefe (topo, centro do mapa)
            var boss = Panel("BossBar", map, Anchor.Top, new Vector2(-330, -16), new Vector2(660, 92), PanelBlue);
            _bossBar = boss.gameObject;
            var portrait = Icon(boss, _art.Portrait, Anchor.Left, new Vector2(12, 0), 70);
            portrait.color = new Color(0.75f, 0.5f, 0.95f);
            string bossDisplay = _sim.Level.Boss != null ? BossDisplayName(_sim.Level.Boss.name) : "";
            Label(boss, bossDisplay, 30, TextAlignmentOptions.Left, Anchor.Left, new Vector2(92, 18), new Vector2(550, 40));
            _bossToast = $"O {bossDisplay} sai da caverna do Krog!";
            var track = Box("Track", boss, Anchor.Left, new Vector2(92, -20), new Vector2(550, 26), new Color(0.15f, 0.05f, 0.08f, 1f));
            _bossFill = Box("Fill", track.rectTransform, Anchor.Left, Vector2.zero, new Vector2(550, 26), ArtLibrary.BossRed);
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossBar.SetActive(false);

            BuildShop();
            BuildUnitPanel(map);

            // ---- Dica de posicionamento, aviso e fim de partida
            var hintRect = Panel("Hint", map, Anchor.Bottom, new Vector2(-430, 120), new Vector2(860, 70), new Color(0f, 0f, 0f, 0.55f));
            _hintGo = hintRect.gameObject;
            _hint = AddText(Fill(hintRect), "", 30, TextAlignmentOptions.Center, Color.white);
            _hintGo.SetActive(false);

            var toastRect = NewRect("Toast", map);
            toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0.74f);
            toastRect.sizeDelta = new Vector2(1200, 90);
            _toast = AddText(toastRect.gameObject, "", 52, TextAlignmentOptions.Center, Color.white);
            _toast.gameObject.SetActive(false);

            var end = Panel("EndPanel", map, Anchor.Center, new Vector2(-380, 200), new Vector2(760, 400), PanelBlue);
            _endPanel = end.gameObject;
            _endTitle = Label(end, "", 80, TextAlignmentOptions.Center, Anchor.Top, new Vector2(-380, -20), new Vector2(760, 110));
            _endStars = Label(end, "", 40, TextAlignmentOptions.Center, Anchor.Top, new Vector2(-380, -140), new Vector2(760, 60));
            var again = PillButton(end, "Restart", Anchor.Bottom, new Vector2(-350, 40), new Vector2(330, 100), Green, Restart, out _);
            ButtonText(again, "Jogar de novo", 36);
            var toMenu = PillButton(end, "Menu", Anchor.Bottom, new Vector2(20, 40), new Vector2(330, 100), Blue, GoToMenu, out _);
            ButtonText(toMenu, "Menu", 36);
            _endPanel.SetActive(false);

            // ---- Pausa: Continuar · Reiniciar · Menu
            var pausePanel = Panel("PausePanel", map, Anchor.Center, new Vector2(-300, 230), new Vector2(600, 460), PanelBlue);
            _pausePanel = pausePanel.gameObject;
            Label(pausePanel, "PAUSA", 72, TextAlignmentOptions.Center, Anchor.Top, new Vector2(-300, -20), new Vector2(600, 100));
            var resume = PillButton(pausePanel, "Resume", Anchor.Top, new Vector2(-200, -140), new Vector2(400, 90), Green, TogglePause, out _);
            ButtonText(resume, "Continuar", 36);
            var restart = PillButton(pausePanel, "Restart", Anchor.Top, new Vector2(-200, -245), new Vector2(400, 90), Blue, Restart, out _);
            ButtonText(restart, "Reiniciar", 36);
            var menu = PillButton(pausePanel, "Menu", Anchor.Top, new Vector2(-200, -350), new Vector2(400, 90), Red, GoToMenu, out _);
            ButtonText(menu, "Menu", 36);
            _pausePanel.SetActive(false);
        }

        void BuildShop()
        {
            // Coluna inteira com moldura de madeira
            var shop = NewRect("Shop", _safe);
            shop.anchorMin = new Vector2(1f - CameraRig.ShopFraction, 0f);
            shop.anchorMax = Vector2.one;
            shop.offsetMin = shop.offsetMax = Vector2.zero;
            var bg = shop.gameObject.AddComponent<Image>();
            bg.sprite = _art.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = Wood;
            var frame = shop.gameObject.AddComponent<Outline>();
            frame.effectColor = WoodDark;
            frame.effectDistance = new Vector2(6, -6);

            // Cabeçalho: nome do campeão escolhido (como o "QUINCY" no topo da loja do BTD6)
            var header = NewRect("Header", shop);
            header.anchorMin = new Vector2(0.04f, 1f);
            header.anchorMax = new Vector2(0.96f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = new Vector2(0f, -10f);
            header.sizeDelta = new Vector2(0f, 84f);
            var hbg = header.gameObject.AddComponent<Image>();
            hbg.sprite = _art.Rounded;
            hbg.type = Image.Type.Sliced;
            hbg.color = WoodDark;
            var titleRect = NewRect("Title", header);
            titleRect.anchorMin = new Vector2(0f, 0.42f); titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
            _shopTitle = AddText(titleRect.gameObject, "Loja", 34, TextAlignmentOptions.Center, Color.white);
            _shopTitle.textWrappingMode = TextWrappingModes.Normal;
            _shopTitle.enableAutoSizing = true;
            _shopTitle.fontSizeMin = 18;
            _shopTitle.fontSizeMax = 34;
            var subRect = NewRect("Subtitle", header);
            subRect.anchorMin = Vector2.zero; subRect.anchorMax = new Vector2(1f, 0.42f);
            subRect.offsetMin = subRect.offsetMax = Vector2.zero;
            _shopSubtitle = AddText(subRect.gameObject, "Toque num campeão", 22, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.6f));

            // Cartões: 2 colunas, largura relativa à coluna (anchors), altura fixa.
            const float cardH = 150f, top = 106f, gap = 12f;
            int row = 0;
            if (_sim.Loadout.Length > 0)
            {
                // Herói ocupa a linha inteira (como o cartão do herói no topo da loja do BTD6)
                var hc = Card(shop, "HeroCard", 0, row, true, cardH, top, gap, OnHeroCard, out _heroCardBg);
                _heroCard = hc.gameObject;
                Photo(hc, HeroPhoto(), Anchor.Left, new Vector2(10, 0), 112);
                Label(hc, "Krog", 30, TextAlignmentOptions.Left, Anchor.Left, new Vector2(128, 26), new Vector2(100, 44));
                _heroCardCost = Label(hc, "", 26, TextAlignmentOptions.Left, Anchor.Left, new Vector2(128, -22), new Vector2(100, 44));
                row++;
            }
            int defs = _sim.TowerDefs.Length;
            _cardBg = new Image[defs];
            _cardCost = new TMP_Text[defs];
            _cardIcon = new RawImage[defs];
            for (int i = 0; i < defs; i++)
            {
                int def = i;
                TowerArchetype arch = _sim.TowerDefs[i].Archetype;
                var c = Card(shop, "Tower" + i, i % 2, row + i / 2, false, cardH, top, gap, () => _placer.BeginTowerPlacement(def), out _cardBg[i]);
                Texture2D icon = UiArt.TowerIcon(arch);
                if (icon != null)
                {
                    // Ícone pintado (moldura de presas + plaquinha com moeda); o preço real vai escrito na plaquinha.
                    _cardBg[i].color = Color.clear;
                    var outline = c.GetComponent<Outline>();
                    if (outline != null) outline.enabled = false;
                    var ir = NewRect("Icon", c);
                    Stretch(ir);
                    var raw = ir.gameObject.AddComponent<RawImage>();
                    raw.texture = icon;
                    raw.raycastTarget = false;
                    var fit = ir.gameObject.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fit.aspectRatio = (float)icon.width / icon.height;
                    _cardIcon[i] = raw;
                    var pr = NewRect("Price", ir);
                    pr.anchorMin = UiArt.PricePlate.min;
                    pr.anchorMax = UiArt.PricePlate.max;
                    pr.offsetMin = pr.offsetMax = Vector2.zero;
                    _cardCost[i] = AddText(pr.gameObject, "", 26, TextAlignmentOptions.Center, Color.white);
                    _cardCost[i].enableAutoSizing = true;
                    _cardCost[i].fontSizeMin = 12;
                    _cardCost[i].fontSizeMax = 26;
                }
                else
                {
                    Photo(c, TowerPhoto((int)arch), Anchor.Top, new Vector2(-50, -8), 100);
                    _cardCost[i] = Label(c, "", 28, TextAlignmentOptions.Center, Anchor.Bottom, new Vector2(-80, 6), new Vector2(160, 40));
                }
                if (_sim.IsArchetypeAllowed(_sim.TowerDefs[i])) _cardCost[i].SetText("{0}", _sim.TowerDefs[i].BaseCost);
                else _cardCost[i].SetText("Trava");
            }

            // Inferior: habilidade à esquerda e iniciar/acelerar (verde) à direita
            Texture2D skillReady = _sim.Loadout.Length > 0 ? UiArt.SkillReady(_sim.Loadout[0].Id) : null;
            if (skillReady != null) BuildSkillArtButton(shop, skillReady, UiArt.SkillGlow(_sim.Loadout[0].Id));
            else
            {
                _skillButton = RoundButton(shop, "Skill", Anchor.BottomLeft, new Vector2(10, 22 + 104), 104, Yellow, OnSkillButton, out _skillBg).gameObject;
                Icon((RectTransform)_skillButton.transform, _art.BoltIcon, Anchor.Center, new Vector2(-36, 0), 72).raycastTarget = false;
                _skillCooldown = Box("Cooldown", (RectTransform)_skillButton.transform, Anchor.Center, new Vector2(-52, 0), new Vector2(104, 104), new Color(0f, 0f, 0f, 0.55f));
            }
            _skillCooldown.sprite = _art.Circle;
            _skillCooldown.type = Image.Type.Filled;
            _skillCooldown.fillMethod = Image.FillMethod.Radial360;
            _skillCooldown.fillOrigin = (int)Image.Origin360.Top;
            _skillCooldown.fillClockwise = false;
            _skillCooldown.raycastTarget = false;

            var play = RoundButton(shop, "Play", Anchor.BottomRight, new Vector2(-8 - 120, 18 + 120), 120, Green, OnPlayButton, out _playBg);
            _playIcon = Icon((RectTransform)play.transform, _art.PlayIcon, Anchor.Center, new Vector2(-44, 0), 88);
            _playIcon.raycastTarget = false;
        }

        /// <summary>
        /// Botão da habilidade com a arte do herói: pronto (pintura acesa), recarga (pintura escurecida + setor escuro
        /// girando + segundos) e brilho pulsando quando pronta; ao lançar, o brilho estoura.
        /// </summary>
        void BuildSkillArtButton(RectTransform shop, Texture2D ready, Texture2D glow)
        {
            const float w = 124f;
            float h = w * ready.height / ready.width;
            var r = NewRect("Skill", shop);
            Place(r, Anchor.BottomLeft, new Vector2(8, 16 + h), new Vector2(w, h));
            _skillButton = r.gameObject;
            _skillBg = r.gameObject.AddComponent<Image>();   // alvo do toque (transparente)
            _skillBg.color = Color.clear;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = _skillBg;
            b.onClick.AddListener(OnSkillButton);

            if (glow != null)
            {
                var gr = NewRect("Glow", r);
                gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
                gr.sizeDelta = new Vector2(w * glow.width / ready.width, h * glow.height / ready.height);
                _skillGlow = gr.gameObject.AddComponent<RawImage>();
                _skillGlow.texture = glow;
                _skillGlow.raycastTarget = false;
                _skillGlow.color = new Color(1f, 1f, 1f, 0f);
            }
            var ar = NewRect("Art", r);
            Stretch(ar);
            _skillArt = ar.gameObject.AddComponent<RawImage>();
            _skillArt.texture = ready;
            _skillArt.raycastTarget = false;

            var cr = NewRect("Cooldown", r);
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(0f, -h * 0.03f);
            cr.sizeDelta = new Vector2(w * UiArt.SkillInnerDisc, w * UiArt.SkillInnerDisc);
            _skillCooldown = cr.gameObject.AddComponent<Image>();
            _skillCooldown.color = new Color(0f, 0f, 0f, 0.55f);

            var tr = NewRect("Seconds", cr);
            Stretch(tr);
            _skillCdText = AddText(tr.gameObject, "", 34, TextAlignmentOptions.Center, new Color(1f, 0.86f, 0.45f));
        }

        /// <summary>Cartão da loja em coluna <paramref name="col"/> (0/1) e linha <paramref name="row"/>; full = linha inteira.</summary>
        RectTransform Card(RectTransform shop, string name, int col, int row, bool full, float h, float top, float gap,
                           UnityAction onClick, out Image bg)
        {
            var r = NewRect(name, shop);
            r.anchorMin = new Vector2(full ? 0.06f : col == 0 ? 0.06f : 0.52f, 1f);
            r.anchorMax = new Vector2(full ? 0.94f : col == 0 ? 0.48f : 0.94f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -(top + row * (h + gap)));
            r.sizeDelta = new Vector2(0f, h);
            bg = r.gameObject.AddComponent<Image>();
            bg.sprite = _art.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = CardColor;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = WoodDark;
            o.effectDistance = new Vector2(3, -3);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(onClick);
            return r;
        }

        void BuildUnitPanel(RectTransform map)
        {
            var unit = Panel("UnitPanel", map, Anchor.Left, new Vector2(16, 350), new Vector2(440, 740), PanelBlue);
            _unitPanel = unit.gameObject;
            _unitPhoto = Photo(unit, null, Anchor.TopLeft, new Vector2(14, -14), 120);
            _unitTitle = Label(unit, "", 34, TextAlignmentOptions.Left, Anchor.TopLeft, new Vector2(146, -40), new Vector2(280, 70));
            _unitTitle.textWrappingMode = TextWrappingModes.Normal;
            var prio = PillButton(unit, "Priority", Anchor.Top, new Vector2(-190, -150), new Vector2(380, 64), Blue, () => _placer.CyclePriority(), out _);
            _priorityLabel = ButtonText(prio, "", 28);

            _pathButtons = new Button[3];
            _pathBg = new Image[3];
            _pathName = new TMP_Text[3];
            _pathInfo = new TMP_Text[3];
            _pathRows = new GameObject[3];
            for (int p = 0; p < 3; p++)
            {
                int path = p;
                var rowRect = Panel("Path" + p, unit, Anchor.Top, new Vector2(-205, -228 - p * 128), new Vector2(410, 116), new Color(0f, 0f, 0f, 0.28f));
                _pathRows[p] = rowRect.gameObject;
                _pathName[p] = Label(rowRect, "", 30, TextAlignmentOptions.Left, Anchor.Left, new Vector2(14, 24), new Vector2(240, 44));
                _pathInfo[p] = Label(rowRect, "", 22, TextAlignmentOptions.Left, Anchor.Left, new Vector2(14, -24), new Vector2(240, 44));
                var b = PillButton(rowRect, "Buy", Anchor.Right, new Vector2(-140, 0), new Vector2(128, 92), Green, () => _placer.Upgrade(path), out _pathBg[p]);
                ButtonText(b, "+", 54);
                _pathButtons[p] = b;
            }
            var sell = PillButton(unit, "Sell", Anchor.Bottom, new Vector2(-190, 18), new Vector2(380, 76), Red, () => _placer.Sell(), out _);
            _sellButton = sell.gameObject;
            _sellLabel = ButtonText(sell, "", 30);
        }

        // ================================================================== loop (zero-GC)
        void Update()
        {
            if (_sim == null) return;
            if (Screen.safeArea != _lastSafeArea) ApplySafeArea();

            if (_sim.Gold != _shownGold) { _shownGold = _sim.Gold; _gold.SetText("{0}", _shownGold); }
            if (_sim.Lives != _shownLives) { _shownLives = _sim.Lives; _lives.SetText("{0}", _shownLives); }
            if (_sim.WaveNumber != _shownWave) { _shownWave = _sim.WaveNumber; _wave.SetText("Onda {0}/{1}", (float)_shownWave, (float)_sim.WaveCount); }

            // Chefe
            bool bossOn = _sim.BossActive;
            if (_bossBar.activeSelf != bossOn) _bossBar.SetActive(bossOn);
            if (bossOn) _bossFill.fillAmount = _sim.BossHpFraction;

            // Botão verde: ▶ entre ondas (com bônus de §8.5) · ▶▶ durante a onda (amarelo quando rápido)
            bool canCall = (_sim.Phase == SimPhase.Building || _sim.Phase == SimPhase.Intermission) && _sim.WaveNumber < _sim.WaveCount;
            Sprite icon = canCall ? _art.PlayIcon : _art.FastIcon;
            if (_playIcon.sprite != icon) _playIcon.sprite = icon;
            _playBg.color = !canCall && _game.Speed >= FastSpeed ? Yellow : Green;
            _pauseBg.color = _game.Paused ? Yellow : Blue;
            if (canCall)
            {
                int countdown = Mathf.CeilToInt(_sim.PhaseTimer);
                int bonus = _sim.EarlyCallBonus;
                if (countdown != _shownCountdown || bonus != _shownBonus)
                {
                    _shownCountdown = countdown;
                    _shownBonus = bonus;
                    _nextInfo.SetText("Próxima onda em {0}s (adiantar: +{1} ouro)", (float)countdown, (float)bonus);
                }
            }
            else if (_shownCountdown != -1) { _shownCountdown = -1; _nextInfo.SetText(""); }

            UpdateShop();
            UpdateHero();
            UpdateHint();
            UpdateContextAffordability();

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0f) _toast.gameObject.SetActive(false);
            }
        }

        void UpdateShop()
        {
            // Cabeçalho: nome e função do campeão escolhido (só reescreve quando muda)
            int sel = _placer.CurrentMode == TowerPlacer.Mode.PlaceTower ? _placer.ModeTower
                    : _placer.CurrentMode == TowerPlacer.Mode.PlaceHero ? 100 : -1;
            if (sel != _shownShopSel)
            {
                _shownShopSel = sel;
                if (sel == 100) { _shopTitle.SetText(HeroName); _shopSubtitle.SetText("Herói · {0} ouro", _sim.Loadout[0].SummonCost); }
                else if (sel >= 0)
                {
                    int a = (int)_sim.TowerDefs[sel].Archetype;
                    _shopTitle.SetText(ChampionNames[a]);
                    _shopSubtitle.SetText(ArchetypeSubtitles[a]);
                }
                else { _shopTitle.SetText("Loja"); _shopSubtitle.SetText("Toque num campeão"); }
            }

            for (int i = 0; i < _cardBg.Length; i++)
            {
                TowerDataSO def = _sim.TowerDefs[i];
                bool allowed = _sim.IsArchetypeAllowed(def);
                bool afford = _sim.Gold >= def.BaseCost;
                bool active = _placer.CurrentMode == TowerPlacer.Mode.PlaceTower && _placer.ModeTower == i;
                _cardCost[i].color = allowed && afford ? CostOk : CostBad;
                if (_cardIcon[i] != null)
                {
                    // arte pintada: escurece se não dá para comprar; o escolhido ganha halo amarelo e cresce
                    _cardIcon[i].color = active || (allowed && afford) ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                    _cardBg[i].color = active ? new Color(CardHot.r, CardHot.g, CardHot.b, 0.85f) : Color.clear;
                    float k = active ? 1.08f : 1f;
                    _cardIcon[i].rectTransform.localScale = new Vector3(k, k, 1f);
                    continue;
                }
                _cardBg[i].color = active ? CardHot : allowed ? CardColor : CardOff;
            }
        }

        void UpdateHero()
        {
            if (_heroCard == null)
            {
                if (_skillButton.activeSelf) _skillButton.SetActive(false);
                return;
            }
            int unit = _heroLocked ? -1 : _sim.HeroUnit[0];
            int state = _heroLocked ? 0 : unit >= 0 ? 2 : 1;
            if (state != _heroCardState)
            {
                _heroCardState = state;
                if (state == 0) _heroCardCost.SetText("Fase 10");
                else if (state == 1) _heroCardCost.SetText("{0}", _sim.Loadout[0].SummonCost);
                else _heroCardCost.SetText("Em campo");
            }
            bool placing = _placer.CurrentMode == TowerPlacer.Mode.PlaceHero;
            _heroCardBg.color = placing ? CardHot : !_heroLocked && unit < 0 ? CardColor : CardOff;
            _heroCardCost.color = _heroLocked || unit >= 0 || _sim.Gold >= _sim.Loadout[0].SummonCost ? CostOk : CostBad;

            bool showSkill = unit >= 0;
            if (_skillButton.activeSelf != showSkill) _skillButton.SetActive(showSkill);
            if (!showSkill) return;
            float max = _sim.HeroSkillCooldownMax(0);
            float cd = _sim.HeroCooldown[0];
            _skillCooldown.fillAmount = max > 0f ? cd / max : 0f;
            bool aiming = _placer.CurrentMode == TowerPlacer.Mode.AimSkill;
            if (_skillArt == null) { _skillBg.color = aiming ? Color.white : Yellow; return; }

            // Arte: escurecida na recarga com os segundos; pronta = brilho pulsando; mirando = brilho cheio.
            bool ready = cd <= 0f;
            _skillArt.color = ready ? Color.white : new Color(0.55f, 0.5f, 0.45f, 1f);
            int secs = ready ? 0 : Mathf.CeilToInt(cd);
            if (secs != _skillCdShown)
            {
                _skillCdShown = secs;
                if (secs > 0) _skillCdText.SetText("{0}s", secs);
                else _skillCdText.SetText("");
            }
            if (_skillGlow != null)
            {
                if (_skillFlash > 0f) _skillFlash = Mathf.Max(0f, _skillFlash - Time.unscaledDeltaTime * 2f);
                float pulse = ready ? 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
                float a = Mathf.Max(aiming ? 1f : pulse, _skillFlash);
                _skillGlow.color = new Color(1f, 1f, 1f, a);
                float k = 1f + 0.12f * _skillFlash;
                _skillGlow.rectTransform.localScale = new Vector3(k, k, 1f);
            }
        }

        /// <summary>Faixa de dica embaixo do mapa enquanto o jogador posiciona ou mira.</summary>
        void UpdateHint()
        {
            int code;
            switch (_placer.CurrentMode)
            {
                case TowerPlacer.Mode.PlaceTower:
                case TowerPlacer.Mode.PlaceHero: code = 1; break;
                case TowerPlacer.Mode.AimSkill: code = 2; break;
                default: code = 0; break;
            }
            if (code == _shownHint) return;
            _shownHint = code;
            _hintGo.SetActive(code != 0);
            if (code == 1) _hint.SetText("Toque no chão fora do caminho · toque de novo para confirmar");
            else if (code == 2) _hint.SetText("Caça de Fogo: toque onde quer o fogo · toque de novo para lançar");
        }

        void OnHeroCard()
        {
            if (_heroLocked || !_sim.IsPlaying) { Toast("Krog é resgatado na fase 10", 1.6f); return; }
            if (_placer.CurrentMode == TowerPlacer.Mode.PlaceHero) { _placer.CancelMode(); return; }
            if (_sim.HeroUnit[0] >= 0) { _placer.Select(_sim.Units[_sim.HeroUnit[0]].Pedestal); return; }
            if (_sim.Gold >= _sim.Loadout[0].SummonCost) _placer.BeginHeroPlacement(0);
        }

        void OnSkillButton()
        {
            if (_placer.CurrentMode == TowerPlacer.Mode.AimSkill) { _placer.CancelMode(); return; }
            if (_sim.CanCastHeroSkill(0)) _placer.BeginSkillAim(0);
        }

        void OnPlayButton()
        {
            bool canCall = (_sim.Phase == SimPhase.Building || _sim.Phase == SimPhase.Intermission) && _sim.WaveNumber < _sim.WaveCount;
            if (canCall) { _sim.StartNextWave(true); return; }
            _game.SetSpeed(_game.Speed >= FastSpeed ? 1 : FastSpeed);
        }

        // ================================================================== painel de melhorias
        public void ShowContext(int pedestal)
        {
            _contextPedestal = pedestal;
            int unit = pedestal >= 0 ? _sim.PedestalUnit[pedestal] : -1;
            _unitPanel.SetActive(unit >= 0);
            if (unit < 0) return;

            TowerState t = _sim.Units[unit];
            bool isHero = t.Kind == UnitKind.Hero;
            for (int p = 0; p < 3; p++) _pathRows[p].SetActive(!isHero);
            _sellButton.SetActive(!isHero);
            _priorityLabel.SetText(PriorityNames[t.Priority]);
            if (isHero)
            {
                SetPhoto(_unitPhoto, HeroPhoto(), 120);
                _unitTitle.SetText(HeroName);
                return;
            }
            int arch = t.Archetype;
            SetPhoto(_unitPhoto, TowerPhoto(arch), 120);
            _unitTitle.SetText(ChampionNames[arch]);
            for (int p = 0; p < 3; p++) _pathName[p].SetText(PathNames[arch, p]);
            _sellLabel.SetText("Vender  {0}", _sim.SellValue(unit));
            UpdateContextAffordability(true);
        }

        void UpdateContextAffordability(bool force = false)
        {
            if (_contextPedestal < 0) return;
            int unit = _sim.PedestalUnit[_contextPedestal];
            if (unit < 0 || _sim.Units[unit].Kind != UnitKind.Tower) return;
            UpgradeState u = _sim.Upgrades[unit];
            for (int p = 0; p < 3; p++)
            {
                bool locked = _sim.IsPathLocked(unit, p);
                bool ok = _sim.CanUpgrade(unit, p);
                _pathButtons[p].interactable = ok;
                _pathBg[p].color = ok ? Green : CardOff;
                if (!force && Time.frameCount % 10 != 0) continue;   // texto: 6×/s basta
                int tier = u.Get(p);
                if (tier >= UpgradeState.MaxTier) _pathInfo[p].SetText("Nível 5 (máx.)");
                else if (locked) _pathInfo[p].SetText(tier + 1 > _sim.MaxTier ? "Nível {0} · trava: máx. {1}" : "Nível {0} · trava 5-2-0", (float)tier, (float)_sim.MaxTier);
                else _pathInfo[p].SetText("Nível {0} → {1} ouro", (float)(tier + 1), (float)_sim.UpgradeCost(unit, p));
                _pathInfo[p].color = locked ? CostBad : ok ? CostOk : CostBad;
            }
        }

        // ================================================================== eventos
        public void OnGameEvent(in GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.WaveStarted:
                    if (_sim.Level.Boss != null && e.A == _sim.WaveCount) Toast(_bossToast);
                    break;
                case GameEventType.PedestalTelegraph:
                    Toast("Pisão Glacial!", 1.2f);
                    break;
                case GameEventType.SkillCast:
                    _skillFlash = 1f;   // estouro do brilho no botão
                    break;
                case GameEventType.HeroRescued:
                    Toast("Krog foi libertado e se juntou a você!", 4f);
                    break;
                case GameEventType.Victory:
                case GameEventType.Defeat:
                    ShowEnd(e.Type == GameEventType.Victory, (int)e.Value);
                    break;
                case GameEventType.TowerSold:
                case GameEventType.TowerUpgraded:
                    if (_contextPedestal >= 0) ShowContext(_contextPedestal);
                    break;
            }
        }

        void TogglePause()
        {
            _game.TogglePause();
            _pausePanel.SetActive(_game.Paused);
        }

        static void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);   // mesma fase

        static void GoToMenu()
        {
            if (Application.CanStreamedLevelBeLoaded(GameSession.MenuScene)) SceneManager.LoadScene(GameSession.MenuScene);
            else Debug.LogWarning("[HUD] Cena MainMenu fora do Build Settings: rode Tools ▸ Chrono ▸ Criar menu inicial.");
        }

        void ShowEnd(bool won, int stars)
        {
            if (won) GameSession.SaveStars(_sim.Level, stars);
            _pausePanel.SetActive(false);
            _endPanel.SetActive(true);
            _endTitle.SetText(won ? "VITÓRIA!" : "DERROTA");
            _endTitle.color = won ? Yellow : ArtLibrary.BossRed;
            if (won) _endStars.SetText("Estrelas: {0}/3", stars);
            else _endStars.SetText("O Núcleo Cronológico caiu");
            _unitPanel.SetActive(false);
        }

        void Toast(string text, float seconds = 2.5f)
        {
            _toast.SetText(text);
            _toast.gameObject.SetActive(true);
            _toastTimer = seconds;
        }

        // ================================================================== helpers de layout
        enum Anchor { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }

        static Vector2 AnchorPoint(Anchor a)
        {
            switch (a)
            {
                case Anchor.TopLeft: return new Vector2(0f, 1f);
                case Anchor.Top: return new Vector2(0.5f, 1f);
                case Anchor.TopRight: return new Vector2(1f, 1f);
                case Anchor.Left: return new Vector2(0f, 0.5f);
                case Anchor.Right: return new Vector2(1f, 0.5f);
                case Anchor.BottomLeft: return new Vector2(0f, 0f);
                case Anchor.Bottom: return new Vector2(0.5f, 0f);
                case Anchor.BottomRight: return new Vector2(1f, 0f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;
            _lastSafeArea = sa;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            _safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        }

        static RectTransform NewRect(string name, Transform parent)
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

        static GameObject Fill(RectTransform parent)
        {
            var r = NewRect("Text", parent);
            Stretch(r);
            return r.gameObject;
        }

        /// <summary>
        /// Retângulo ancorado em <paramref name="a"/>; <paramref name="pos"/> é o canto superior esquerdo do
        /// elemento relativo ao ponto da âncora (pivô no canto superior esquerdo).
        /// </summary>
        static RectTransform Place(RectTransform r, Anchor a, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = AnchorPoint(a);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        RectTransform Panel(string name, RectTransform parent, Anchor a, Vector2 pos, Vector2 size, Color color)
        {
            var r = Place(NewRect(name, parent), a, pos, size);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = _art.Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            return r;
        }

        Image Box(string name, RectTransform parent, Anchor a, Vector2 pos, Vector2 size, Color color)
        {
            var r = NewRect(name, parent);
            r.anchorMin = r.anchorMax = AnchorPoint(a);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = _art.Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        /// <summary>Ícone quadrado; para Left/Center/Right o <paramref name="pos"/>.y é o centro vertical.</summary>
        Image Icon(RectTransform parent, Sprite sprite, Anchor a, Vector2 pos, float size)
        {
            var r = NewRect("Icon", parent);
            r.anchorMin = r.anchorMax = AnchorPoint(a);
            bool midRow = a == Anchor.Left || a == Anchor.Center || a == Anchor.Right;
            r.pivot = new Vector2(0f, midRow ? 0.5f : 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(size, size);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        TMP_Text Label(RectTransform parent, string text, float size, TextAlignmentOptions align, Anchor a, Vector2 pos, Vector2 box)
        {
            var r = NewRect("Label", parent);
            r.anchorMin = r.anchorMax = AnchorPoint(a);
            bool midRow = a == Anchor.Left || a == Anchor.Center || a == Anchor.Right;
            r.pivot = new Vector2(0f, midRow ? 0.5f : a == Anchor.Bottom || a == Anchor.BottomLeft || a == Anchor.BottomRight ? 0f : 1f);
            if (align == TextAlignmentOptions.Right && (a == Anchor.TopRight || a == Anchor.Right || a == Anchor.BottomRight))
                r.pivot = new Vector2(1f, r.pivot.y);
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            return AddText(r.gameObject, text, size, align, Color.white);
        }

        /// <summary>Texto branco com contorno grosso escuro (leitura do BTD6 sobre qualquer fundo).</summary>
        TMP_Text AddText(GameObject go, string text, float size, TextAlignmentOptions align, Color color)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.32f;
            t.outlineColor = Ink;
            t.SetText(text);
            return t;
        }

        Button PillButton(RectTransform parent, string name, Anchor a, Vector2 pos, Vector2 size, Color color, UnityAction onClick, out Image bg)
        {
            var r = NewRect(name, parent);
            r.anchorMin = r.anchorMax = AnchorPoint(a);
            bool midRow = a == Anchor.Left || a == Anchor.Center || a == Anchor.Right;
            r.pivot = new Vector2(0f, midRow ? 0.5f : a == Anchor.Bottom || a == Anchor.BottomLeft || a == Anchor.BottomRight ? 0f : 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            bg = r.gameObject.AddComponent<Image>();
            bg.sprite = _art.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = color;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(3, -3);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(onClick);   // delegates criados no carregamento, nunca no loop
            return b;
        }

        /// <summary>Botão redondo (pos = canto superior esquerdo relativo à âncora).</summary>
        Button RoundButton(RectTransform parent, string name, Anchor a, Vector2 pos, float size, Color color, UnityAction onClick, out Image bg)
        {
            var r = Place(NewRect(name, parent), a, pos, new Vector2(size, size));
            bg = r.gameObject.AddComponent<Image>();
            bg.sprite = _art.Circle;
            bg.color = color;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(4, -4);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(onClick);
            return b;
        }

        // ---- fotos: retrato 3D (LevelRenderer.BuildPortraits) ou, sem modelo, o desenho provisório
        Texture TowerPhoto(int archetype)
        {
            Texture[] p = _game != null && _game.Renderer != null ? _game.Renderer.TowerPortraits : null;
            return p != null && archetype < p.Length && p[archetype] != null ? p[archetype] : _art.TowerIcons[archetype].texture;
        }

        Texture HeroPhoto()
        {
            Texture[] p = _game != null && _game.Renderer != null ? _game.Renderer.HeroPortraits : null;
            return p != null && p.Length > 0 && p[0] != null ? p[0] : _art.Portrait.texture;
        }

        /// <summary>Foto quadrada com moldura escura; desenhos provisórios mantêm a proporção.</summary>
        RawImage Photo(RectTransform parent, Texture tex, Anchor a, Vector2 pos, float size)
        {
            var frame = NewRect("PhotoFrame", parent);
            frame.anchorMin = frame.anchorMax = AnchorPoint(a);
            bool midRow = a == Anchor.Left || a == Anchor.Center || a == Anchor.Right;
            frame.pivot = new Vector2(0f, midRow ? 0.5f : 1f);
            frame.anchoredPosition = pos;
            frame.sizeDelta = new Vector2(size, size);
            var bg = frame.gameObject.AddComponent<Image>();
            bg.sprite = _art.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = Ink;
            bg.raycastTarget = false;
            var r = NewRect("Photo", frame);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            var raw = r.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            SetPhoto(raw, tex, size);
            return raw;
        }

        static void SetPhoto(RawImage raw, Texture tex, float size)
        {
            raw.texture = tex;
            raw.enabled = tex != null;
            if (tex == null) return;
            float inner = size - 8f;
            float aspect = (float)tex.width / Mathf.Max(1, tex.height);
            raw.rectTransform.sizeDelta = aspect >= 1f ? new Vector2(inner, inner / aspect) : new Vector2(inner * aspect, inner);
        }

        TMP_Text ButtonText(Button b, string text, float size)
        {
            return AddText(Fill((RectTransform)b.transform), text, size, TextAlignmentOptions.Center, Color.white);
        }
    }

    /// <summary>Dispara uma ação ao pressionar (antes do clique).</summary>
    public sealed class PointerDownRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerEnterHandler
    {
        public System.Action OnDown;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) => OnDown?.Invoke();
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => OnDown?.Invoke();
    }
}
