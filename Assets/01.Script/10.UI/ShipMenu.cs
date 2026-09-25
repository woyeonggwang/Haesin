using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 함선 메뉴 (Y). 전체화면. 열리면 GameMenuPause 로 게임이 멈춘다(지도와 동일).
///
/// 2026-09-19 개편(어싸신크리드 오디세이 참고 화면처럼):
///   - 배경: Background.png 가 화면 전체를 비율 유지로 채우고, 그 위에 실제 판옥선 3D 모델이 올라간다.
///     둘 다 ShowcaseViewer 가 한 장의 RenderTexture 에 그린다(HDRP 가 RT 알파를 1 로 채워 UI 배경이 비치지 않기 때문).
///   - 왼쪽: 2열 바둑판형 정사각 이미지 버튼 5개(함포/선원/외관/화살/선원 체력), 아래에 이름, 선택은 얇은 금색 테두리.
///   - 오른쪽: 선택 항목의 큰 이미지 + 설명·레벨·비용·강화(영입) 버튼. 외관 탭은 이미지 아래에 깃발 UI.
///   - 빈 여섯번째 칸은 만들지 않는다(새 기능 없음).
/// UI 는 전부 코드로 만든다(MapSystem 과 같은 방식).
/// </summary>
public class ShipMenu : MonoBehaviour
{
    public bool isOpen;
    public Color panelColor = new Color(0.07f, 0.09f, 0.14f, 0.94f);
    public Color buttonColor = new Color(0.16f, 0.19f, 0.26f, 1f);
    public Color buttonSelected = new Color(0.55f, 0.42f, 0.18f, 1f);
    public Color accent = new Color(0.95f, 0.80f, 0.45f, 1f);
    [Tooltip("선택되지 않은 항목 테두리 색.")]
    public Color tileBorder = new Color(0.28f, 0.31f, 0.40f, 0.95f);
    [Tooltip("항목 이미지 뒤 바탕색.")]
    public Color tileBack = new Color(0.08f, 0.10f, 0.15f, 0.96f);

    enum Tab { Cannon, Crew, Appearance, Arrow, CrewHealth }
    Tab _tab = Tab.Cannon;

    Canvas _canvas; RectTransform _root;
    ShowcaseViewer _viewer;
    Text _titleText, _descText, _levelText, _costText, _statsText, _msgText;
    ResourceBar _resBar, _costBar;
    Button _actionBtn; Text _actionLabel;
    Image[] _tileBorders;
    Text[] _tileLabels;
    RawImage _detailImage;
    RectTransform _detailFrame;
    RectTransform _flagPanel;
    Texture2D[] _art;
    float _msgUntil;

    static readonly string[] TabNames = { "함포", "선원", "외관", "화살", "선원 체력" };
    static readonly string[] ArtNames = { "Cannon", "Crew", "Appearance", "Arrow", "CrewHealth" };
    static readonly Color[] FlagColors = { Color.white, new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.30f, 0.85f), new Color(0.95f, 0.80f, 0.20f), new Color(0.15f, 0.60f, 0.25f), new Color(0.55f, 0.20f, 0.65f), new Color(0.10f, 0.10f, 0.12f), new Color(0.90f, 0.55f, 0.20f) };

    // 레이아웃 상수 (1920x1080 기준, CanvasScaler 가 비율에 맞게 늘린다)
    const float TopBar = 70f, BottomBar = 90f;
    const float TileSize = 170f, TileGap = 18f, GridLeft = 46f, GridTop = -104f, TileLabelH = 28f;
    const float PanelW = 500f, PanelH = 910f, PanelRight = -40f, PanelTop = -76f, Pad = 22f;
    const float DetailH = 280f;

    void Start()
    {
        _art = new Texture2D[ArtNames.Length];
        for (int i = 0; i < ArtNames.Length; i++) _art[i] = Resources.Load<Texture2D>("ShipMenuArt/" + ArtNames[i]);

        _viewer = gameObject.AddComponent<ShowcaseViewer>();
        _viewer.stagePosition = new Vector3(20000f, 600f, 20000f);
        _viewer.fullScreen = true;
        _viewer.backdrop = Resources.Load<Texture2D>("ShipMenuArt/Background");
        // 왼쪽 그리드(~404px)와 오른쪽 패널(1380px~) 사이 빈 폭에 배가 들어가도록 한다.
        _viewer.horizontalFreeFraction = 0.50f;
        _viewer.verticalFreeFraction = 0.80f;
        _viewer.distanceScale = 1.05f;
        // 빈 영역(그리드 오른쪽~패널 왼쪽)의 가운데 = (404+1380)/2 ≈ 892 → 1920 기준 0.465
        _viewer.viewportCenterX = 0.465f;

        BuildUI();
        _root.gameObject.SetActive(false);
        if (PlayerResources.Instance != null) PlayerResources.Instance.OnChanged += Refresh;
        if (ShipUpgrades.Instance != null) ShipUpgrades.Instance.OnChanged += OnUpgradesChanged;
    }

    void OnDestroy()
    {
        if (PlayerResources.Instance != null) PlayerResources.Instance.OnChanged -= Refresh;
        if (ShipUpgrades.Instance != null) ShipUpgrades.Instance.OnChanged -= OnUpgradesChanged;
    }

    void Update()
    {
        GameMenuPause.Tick();
        var kb = Keyboard.current; if (kb == null) return;
        if (!isOpen)
        {
            if (kb.yKey.wasPressedThisFrame) SetOpen(true);
        }
        else
        {
            if (kb.yKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { SetOpen(false); return; }
            if (_msgText != null && Time.unscaledTime > _msgUntil && _msgText.text.Length > 0) _msgText.text = "";
            UpdateViewerFraming();
        }
    }

    public void SetOpen(bool open)
    {
        if (open == isOpen) return;
        if (open)
        {
            if (!GameMenuPause.Acquire(this)) return;
            isOpen = true;
            _root.gameObject.SetActive(true);
            var ship = ShipUpgrades.PlayerShip();
            if (ship != null) { _viewer.Show(ship); if (ShipUpgrades.Instance != null) ShipUpgrades.Instance.ApplyFlagTo(_viewer.Subject); }
            SelectTab(_tab);
            UpdateViewerFraming();
            Refresh();
        }
        else
        {
            isOpen = false;
            _root.gameObject.SetActive(false);
            _viewer.Hide();
            GameMenuPause.Release(this);
        }
    }

    /// <summary>
    /// 화면 비율에 따라 UI 가 차지하는 폭이 달라진다(CanvasScaler ScaleWithScreenSize, match 0.5).
    /// 실제 캔버스(Canvas) 크기로 빈 영역(그리드 오른쪽 ~ 패널 왼쪽)을 계산해 뷰어에 넘긴다.
    /// 4:3 처럼 좌우가 좁은 비율에서도 배가 패널을 침범하지 않게 한다.
    /// </summary>
    void UpdateViewerFraming()
    {
        if (_viewer == null || _canvas == null) return;
        float sf = _canvas.scaleFactor;
        if (sf <= 0.0001f) return;
        float cw = Screen.width / sf, ch = Screen.height / sf;   // 캔버스(Canvas) 단위 화면 크기

        float gridRight = GridLeft + TileSize * 2f + TileGap;            // 왼쪽 그리드 끝
        float panelLeft = cw - (-PanelRight + PanelW);                   // 오른쪽 패널 시작
        float margin = 24f;
        float left = gridRight + margin, right = panelLeft - margin;
        if (right <= left) { left = cw * 0.35f; right = cw * 0.65f; }

        float hFree = (right - left) / cw;
        float cx = ((left + right) * 0.5f) / cw;
        float vFree = Mathf.Max(0.2f, (ch - TopBar - BottomBar - 40f) / ch);
        _viewer.SetFraming(hFree, vFree, cx);
    }


    // ================= UI =================

    void BuildUI()
    {
        _canvas = UIKit.NewCanvas("ShipMenuCanvas", 160);
        _canvas.transform.SetParent(transform, false);
        _root = UIKit.NewRect("Root", _canvas.transform); UIKit.Stretch(_root, 0f);

        // 배경: 뷰어 RT 가 못 나올 때를 위한 받침색 + 그 위에 전체 화면 뷰어(배경 그림 + 판옥선)
        var bg = UIKit.NewImage("BG", _root, new Color(0.04f, 0.05f, 0.08f, 1f)); UIKit.Stretch(bg.rectTransform, 0f);
        var view = UIKit.NewRawImage("ShipView", _root, _viewer.Texture);
        UIKit.Stretch(view.rectTransform, 0f);
        view.raycastTarget = true;   // 드래그 회전용 (위에 다른 UI 가 있으면 ShowcaseViewer 가 걸러낸다)
        _viewer.view = view;

        // 상단 바
        var top = UIKit.NewImage("TopBar", _root, panelColor);
        top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1); top.rectTransform.pivot = new Vector2(0.5f, 1);
        top.rectTransform.anchoredPosition = Vector2.zero; top.rectTransform.sizeDelta = new Vector2(0, TopBar);
        var title = UIKit.NewText("Title", top.transform, "함선", 34, TextAnchor.MiddleLeft, accent);
        UIKit.Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(300, 60));
        // 자원: 아이콘 + 숫자 (인벤토리와 같은 이미지·순서·크기)
        _resBar = ResourceBar.Create(top.transform, "Resources", 34f, 24, Color.white, true, null);
        _resBar.Root.anchorMin = _resBar.Root.anchorMax = _resBar.Root.pivot = new Vector2(1f, 0.5f);
        _resBar.Root.anchoredPosition = new Vector2(-30f, 0f);
        var hint = UIKit.NewText("Hint", top.transform, "Y / ESC : 닫기    드래그 : 함선 회전", 18, TextAnchor.MiddleCenter, new Color(0.72f, 0.74f, 0.80f));
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(600, 60));

        BuildTileGrid();
        BuildRightPanel();

        // 하단 수치
        var bottom = UIKit.NewImage("Bottom", _root, panelColor);
        bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0); bottom.rectTransform.pivot = new Vector2(0.5f, 0);
        bottom.rectTransform.anchoredPosition = Vector2.zero; bottom.rectTransform.sizeDelta = new Vector2(0, BottomBar);
        _statsText = UIKit.NewText("Stats", bottom.transform, "", 24, TextAnchor.MiddleCenter, Color.white);
        UIKit.Stretch(_statsText.rectTransform, 10f);
    }

    /// <summary>왼쪽 2열 정사각 이미지 버튼. 선택된 칸은 얇은 금색 테두리로 구분한다.</summary>
    void BuildTileGrid()
    {
        _tileBorders = new Image[TabNames.Length];
        _tileLabels = new Text[TabNames.Length];

        for (int i = 0; i < TabNames.Length; i++)
        {
            int idx = i;
            int col = i % 2, row = i / 2;
            Vector2 pos = new Vector2(GridLeft + col * (TileSize + TileGap), GridTop - row * (TileSize + TileLabelH + TileGap));

            var cell = UIKit.NewRect("Tile_" + i, _root);
            UIKit.Place(cell, new Vector2(0, 1), pos, new Vector2(TileSize, TileSize + TileLabelH));

            // 테두리(= 버튼) → 바탕 → 그림 순서로 겹친다. 테두리 두께는 3px.
            var border = UIKit.NewImage("Border", cell, tileBorder);
            UIKit.Place(border.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(TileSize, TileSize));
            var btn = border.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(() => SelectTab((Tab)idx));

            var back = UIKit.NewImage("Back", border.transform, tileBack);
            UIKit.Stretch(back.rectTransform, 3f); back.raycastTarget = false;

            if (_art[i] != null)
            {
                var thumb = UIKit.NewRawImage("Thumb", border.transform, _art[i]);
                UIKit.Stretch(thumb.rectTransform, 5f); thumb.raycastTarget = false;
            }

            var label = UIKit.NewText("Label", cell, TabNames[i], 22, TextAnchor.MiddleCenter, new Color(0.88f, 0.89f, 0.93f));
            UIKit.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, -(TileSize + 2f)), new Vector2(TileSize, TileLabelH));
            _tileBorders[i] = border;
            _tileLabels[i] = label;
        }
    }

    void BuildRightPanel()
    {
        var panel = UIKit.NewImage("Panel", _root, panelColor);
        UIKit.Place(panel.rectTransform, new Vector2(1, 1), new Vector2(PanelRight, PanelTop), new Vector2(PanelW, PanelH));
        float inner = PanelW - Pad * 2f;

        // 상세 이미지 (비율 유지해 상자 안에 맞춘다)
        // 상자는 그림 비율에 맞춰 가로 가운데 정렬한다(정사각 그림이면 양옆 검은 여백이 생기지 않게).
        var frame = UIKit.NewImage("DetailFrame", panel.transform, tileBack);
        UIKit.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -Pad), new Vector2(inner, DetailH));
        _detailFrame = frame.rectTransform;
        var edge = UIKit.NewImage("DetailEdge", frame.transform, tileBorder);
        UIKit.Stretch(edge.rectTransform, 0f); edge.raycastTarget = false;
        var fill = UIKit.NewImage("DetailFill", edge.transform, tileBack);
        UIKit.Stretch(fill.rectTransform, 2f); fill.raycastTarget = false;
        _detailImage = UIKit.NewRawImage("DetailImage", fill.transform, null);
        UIKit.Place(_detailImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DetailH, DetailH));
        _detailImage.raycastTarget = false;

        _titleText = UIKit.NewText("PTitle", panel.transform, "", 30, TextAnchor.MiddleLeft, accent);
        UIKit.Place(_titleText.rectTransform, new Vector2(0, 1), new Vector2(Pad, -312), new Vector2(inner, 42));
        _descText = UIKit.NewText("PDesc", panel.transform, "", 20, TextAnchor.UpperLeft, new Color(0.86f, 0.87f, 0.90f));
        UIKit.Place(_descText.rectTransform, new Vector2(0, 1), new Vector2(Pad, -358), new Vector2(inner, 120));
        _levelText = UIKit.NewText("PLevel", panel.transform, "", 24, TextAnchor.MiddleLeft, Color.white);
        UIKit.Place(_levelText.rectTransform, new Vector2(0, 1), new Vector2(Pad, -488), new Vector2(inner, 38));
        // 비용은 "이름 + 숫자" 대신 자원 아이콘 + 숫자로 표시한다(상단 자원 표시와 동일).
        _costText = UIKit.NewText("PCost", panel.transform, "", 22, TextAnchor.MiddleLeft, new Color(0.95f, 0.85f, 0.55f));
        UIKit.Place(_costText.rectTransform, new Vector2(0, 1), new Vector2(Pad, -530), new Vector2(210, 38));
        _costBar = ResourceBar.Create(panel.transform, "PCostBar", 30f, 22, new Color(0.95f, 0.88f, 0.62f), true, "무료");
        _costBar.Root.anchorMin = _costBar.Root.anchorMax = new Vector2(0f, 1f);
        _costBar.Root.pivot = new Vector2(0f, 0.5f);
        _costBar.Root.anchoredPosition = new Vector2(Pad + 214f, -549f);
        _actionBtn = UIKit.NewButton("Action", panel.transform, "강화", 26, buttonSelected, Color.white, OnAction);
        UIKit.Place(_actionBtn.image.rectTransform, new Vector2(0, 1), new Vector2(Pad, -576), new Vector2(inner, 64));
        _actionLabel = _actionBtn.GetComponentInChildren<Text>();
        _msgText = UIKit.NewText("PMsg", panel.transform, "", 20, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.45f));
        UIKit.Place(_msgText.rectTransform, new Vector2(0, 1), new Vector2(Pad, -648), new Vector2(inner, 34));

        BuildFlagPanel(panel.transform, inner);
    }

    /// <summary>외관 탭: 깃발 색 견본 + 이미지 목록. 상세 이미지·설명 아래에 놓여 겹치지 않는다.</summary>
    void BuildFlagPanel(Transform panel, float inner)
    {
        _flagPanel = UIKit.NewRect("FlagPanel", panel);
        UIKit.Place(_flagPanel, new Vector2(0, 1), new Vector2(Pad, -488), new Vector2(inner, 420));

        var fl = UIKit.NewText("FlagLabel", _flagPanel, "깃발 색", 22, TextAnchor.MiddleLeft, Color.white);
        UIKit.Place(fl.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(inner, 30));

        for (int i = 0; i < FlagColors.Length; i++)
        {
            Color c = FlagColors[i];
            var sw = UIKit.NewButton("Swatch_" + i, _flagPanel, "", 10, c, Color.clear, () => { if (ShipUpgrades.Instance != null) { ShipUpgrades.Instance.SetFlagColor(c); ShipUpgrades.Instance.ApplyFlagTo(_viewer.Subject); } });
            UIKit.Place(sw.image.rectTransform, new Vector2(0, 1), new Vector2((i % 4) * 84f, -36f - (i / 4) * 84f), new Vector2(76, 76));
        }

        var il = UIKit.NewText("ImgLabel", _flagPanel, "깃발 이미지 (StreamingAssets/Flags/*.png)", 18, TextAnchor.MiddleLeft, new Color(0.80f, 0.82f, 0.88f));
        UIKit.Place(il.rectTransform, new Vector2(0, 1), new Vector2(0, -206), new Vector2(inner, 26));
        var none = UIKit.NewButton("ImgNone", _flagPanel, "원래 그림", 20, buttonColor, Color.white, () => { if (ShipUpgrades.Instance != null) { ShipUpgrades.Instance.SetFlagImage(""); ShipUpgrades.Instance.ApplyFlagTo(_viewer.Subject); } });
        UIKit.Place(none.image.rectTransform, new Vector2(0, 1), new Vector2(0, -238), new Vector2(200, 42));

        var files = ShipUpgrades.AvailableFlagImages();
        for (int i = 0; i < files.Count && i < 3; i++)
        {
            string f = files[i];
            var fb = UIKit.NewButton("Img_" + i, _flagPanel, f, 18, buttonColor, Color.white, () => { if (ShipUpgrades.Instance != null) { ShipUpgrades.Instance.SetFlagImage(f); ShipUpgrades.Instance.ApplyFlagTo(_viewer.Subject); } });
            UIKit.Place(fb.image.rectTransform, new Vector2(0, 1), new Vector2(0, -286f - i * 46f), new Vector2(inner, 42));
        }
    }

    void SelectTab(Tab t)
    {
        _tab = t;
        for (int i = 0; i < _tileBorders.Length; i++)
        {
            bool on = (int)t == i;
            UIKit.SetColor(_tileBorders[i], on ? accent : tileBorder);
            _tileLabels[i].color = on ? accent : new Color(0.88f, 0.89f, 0.93f);
        }

        // 상세 이미지: 비율을 유지한 채 상자 안에 맞춘다.
        Texture2D art = _art[(int)t];
        _detailImage.texture = art;
        _detailImage.enabled = art != null;
        if (art != null)
        {
            float boxW = PanelW - Pad * 2f, boxH = DetailH;
            float k = Mathf.Min(boxW / art.width, boxH / art.height);
            Vector2 fit = new Vector2(art.width * k, art.height * k);
            _detailFrame.sizeDelta = fit;                       // 상자를 그림 크기에 맞춤
            _detailImage.rectTransform.sizeDelta = fit - new Vector2(4f, 4f);   // 테두리 2px 안쪽
        }

        _flagPanel.gameObject.SetActive(t == Tab.Appearance);
        bool showUpgrade = t != Tab.Appearance;
        _levelText.gameObject.SetActive(showUpgrade); _costText.gameObject.SetActive(showUpgrade); _actionBtn.gameObject.SetActive(showUpgrade);
        if (_costBar != null) _costBar.SetActive(showUpgrade);
        Refresh();
    }

    void OnUpgradesChanged() { Refresh(); }

    void Refresh()
    {
        if (_root == null || !isOpen) return;
        var r = PlayerResources.Instance; var u = ShipUpgrades.Instance;
        if (_resBar != null) _resBar.SetValues(r != null ? r.wood : 0, r != null ? r.iron : 0, r != null ? r.gold : 0);

        var ship = ShipUpgrades.PlayerShip();
        var hp = ship != null ? ship.GetComponent<ShipHealth>() : null;
        var crew = ship != null ? ship.GetComponent<ShipCrew>() : null;
        string hpS = hp != null ? Mathf.CeilToInt(hp.currentHealth) + " / " + Mathf.CeilToInt(hp.maxHealth) : "-";
        string crewS = crew != null ? crew.Count + " / " + crew.maxCrew : "-";
        if (u != null)
            _statsText.text = "함선 체력  " + hpS + "        선원  " + crewS + "        함포 공격력  x" + u.CannonDamageMultiplier.ToString("F2") + "        화살 공격력  x" + u.ArrowDamageMultiplier.ToString("F2") + "        선원 체력  " + Mathf.RoundToInt(u.CrewHealth);

        if (u == null) return;
        switch (_tab)
        {
            case Tab.Cannon:
                _titleText.text = "함포"; _descText.text = "함포의 공격력을 강화합니다. 포탄 한 발의 피해가 레벨당 15% 늘어납니다.";
                SetLevel(u.cannonLevel, u.maxLevel, u.CostCannon(u.cannonLevel), "강화"); break;
            case Tab.Crew:
                _titleText.text = "선원"; _descText.text = "선원을 영입합니다. 영입한 선원은 갑판에 섭니다. (전투·화살 발사는 추후)";
                _levelText.text = "선원  " + crewS;
                if (crew != null && crew.HasFreeSlot)
                {
                    _costText.text = "비용";
                    if (_costBar != null) { _costBar.SetActive(true); _costBar.SetCost(u.CostRecruit(crew.Count)); }
                    _actionLabel.text = "영입"; _actionBtn.interactable = true;
                }
                else
                {
                    _costText.text = "자리가 없습니다";
                    if (_costBar != null) _costBar.SetActive(false);
                    _actionLabel.text = "영입 불가"; _actionBtn.interactable = false;
                }
                break;
            case Tab.Appearance:
                _titleText.text = "외관"; _descText.text = "깃발 색을 고르거나, 게임 폴더 StreamingAssets/Flags 에 넣은 이미지를 깃발에 입힙니다. (화면의 함선에서 바로 확인)"; break;
            case Tab.Arrow:
                _titleText.text = "화살"; _descText.text = "선원들이 쏘는 화살의 공격력을 강화합니다. 레벨당 15%. (화살 발사 시스템은 추후 — 지금은 수치만 올라갑니다)";
                SetLevel(u.arrowLevel, u.maxLevel, u.CostArrow(u.arrowLevel), "강화"); break;
            case Tab.CrewHealth:
                _titleText.text = "선원 체력"; _descText.text = "적의 화살·총알을 버티는 선원의 체력을 강화합니다. 레벨당 20%. (피격 시스템은 추후)";
                SetLevel(u.crewHealthLevel, u.maxLevel, u.CostCrewHealth(u.crewHealthLevel), "강화"); break;
        }
    }

    void SetLevel(int level, int max, Vector3Int cost, string action)
    {
        _levelText.text = "레벨  " + level + " / " + max;
        bool can = level < max;
        _costText.text = can ? "다음 레벨 비용" : "최대 레벨";
        if (_costBar != null)
        {
            _costBar.SetActive(can);
            if (can) _costBar.SetCost(cost);
        }
        _actionLabel.text = can ? action : "최대";
        _actionBtn.interactable = can;
    }

    void OnAction()
    {
        var u = ShipUpgrades.Instance; if (u == null) return;
        bool ok = false;
        switch (_tab)
        {
            case Tab.Cannon: ok = u.UpgradeCannon(); break;
            case Tab.Crew: ok = u.Recruit(); if (ok) { _viewer.Rebuild(ShipUpgrades.PlayerShip()); u.ApplyFlagTo(_viewer.Subject); } break;
            case Tab.Arrow: ok = u.UpgradeArrow(); break;
            case Tab.CrewHealth: ok = u.UpgradeCrewHealth(); break;
        }
        if (!ok) Message("자원이 부족하거나 더 올릴 수 없습니다.");
        Refresh();
    }

    void Message(string m) { _msgText.text = m; _msgUntil = Time.unscaledTime + 2.5f; }
}
