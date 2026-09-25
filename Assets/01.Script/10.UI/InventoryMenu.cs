using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 메뉴 (I). 전체화면, 열리면 게임이 멈춘다(GameMenuPause).
/// 왼쪽: 플레이어 캐릭터 3D 뷰어(Haesin_Player 모델의 현재 모습을 복사해 보여 준다. 드래그 회전).
/// 오른쪽: 머리스타일 / 상의 / 하의 / 신발 / 무기 탭과 장비 슬롯 격자(아이템 시스템은 아직 없어 빈 슬롯).
/// 위: 자원(목재·철·금화).
/// </summary>
public class InventoryMenu : MonoBehaviour
{
    public bool isOpen;
    public Color panelColor = new Color(0.10f, 0.11f, 0.15f, 0.96f);
    public Color buttonColor = new Color(0.20f, 0.22f, 0.30f, 1f);
    public Color buttonSelected = new Color(0.55f, 0.42f, 0.18f, 1f);
    public Color accent = new Color(0.95f, 0.80f, 0.45f, 1f);
    public int slotColumns = 5, slotRows = 4;
    [Tooltip("빈 장비 칸 테두리(얇은 금색).")]
    public Color slotBorder = new Color(0.46f, 0.37f, 0.20f, 0.85f);
    [Tooltip("빈 장비 칸 바탕(네이비).")]
    public Color slotFill = new Color(0.09f, 0.12f, 0.20f, 1f);
    [Tooltip("빈 장비 칸을 버튼보다 얼마나 가라앉히게 보일지(1 이면 버튼과 똑같음).")]
    public Color slotDim = new Color(0.62f, 0.66f, 0.74f, 1f);

    static readonly string[] TabNames = { "머리스타일", "상의", "하의", "신발", "무기" };
    int _tab;

    Canvas _canvas; RectTransform _root;
    ShowcaseViewer _viewer;
    Text _tabTitle, _emptyText, _charText;
    Image[] _tabImages;
    Text[] _tabLabels;
    ResourceBar _resBar;

    void Start()
    {
        _viewer = gameObject.AddComponent<ShowcaseViewer>();
        _viewer.stagePosition = new Vector3(20300f, 600f, 20000f); // 함선 뷰어와 겹치지 않게
        _viewer.cameraElevation = 6f;
        _viewer.distanceScale = 1.6f;
        _viewer.stageLights = true; // 어두운 옷(남색·흑색)이 햇빛만으로는 거의 검게 나와 전용 조명 사용
        BuildUI();
        _root.gameObject.SetActive(false);
        if (PlayerResources.Instance != null) PlayerResources.Instance.OnChanged += Refresh;
    }

    void OnDestroy() { if (PlayerResources.Instance != null) PlayerResources.Instance.OnChanged -= Refresh; }

    void Update()
    {
        GameMenuPause.Tick();
        var kb = Keyboard.current; if (kb == null) return;
        if (!isOpen) { if (kb.iKey.wasPressedThisFrame) SetOpen(true); }
        else if (kb.iKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        if (open == isOpen) return;
        if (open)
        {
            if (!GameMenuPause.Acquire(this)) return;
            isOpen = true;
            _root.gameObject.SetActive(true);
            var pc = FindFirstObjectByType<PlayerCharacter>();
            if (pc != null) _viewer.Show(pc.gameObject);
            SelectTab(_tab);
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

    void BuildUI()
    {
        _canvas = UIKit.NewCanvas("InventoryCanvas", 161);
        _canvas.transform.SetParent(transform, false);
        _root = UIKit.NewRect("Root", _canvas.transform); UIKit.Stretch(_root, 0f);
        var bg = UIKit.NewImage("BG", _root, new Color(0.04f, 0.05f, 0.08f, 1f)); UIKit.Stretch(bg.rectTransform, 0f);

        var top = UIKit.NewImage("TopBar", _root, panelColor);
        top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1); top.rectTransform.pivot = new Vector2(0.5f, 1);
        top.rectTransform.anchoredPosition = Vector2.zero; top.rectTransform.sizeDelta = new Vector2(0, 70);
        var title = UIKit.NewText("Title", top.transform, "인벤토리", 34, TextAnchor.MiddleLeft, accent);
        UIKit.Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(300, 60));
        // 자원: 아이콘 + 숫자 (함선 메뉴와 같은 이미지·순서·크기)
        _resBar = ResourceBar.Create(top.transform, "Resources", 34f, 24, Color.white, true, null);
        _resBar.Root.anchorMin = _resBar.Root.anchorMax = _resBar.Root.pivot = new Vector2(1f, 0.5f);
        _resBar.Root.anchoredPosition = new Vector2(-30f, 0f);
        var hint = UIKit.NewText("Hint", top.transform, "I / ESC : 닫기   Y : 함선   드래그 : 회전", 18, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f));
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(600, 60));

        // 왼쪽: 캐릭터 뷰어
        var charPanel = UIKit.NewImage("CharPanel", _root, panelColor);
        UIKit.Place(charPanel.rectTransform, new Vector2(0, 1), new Vector2(40, -100), new Vector2(760, 880));
        var view = UIKit.NewRawImage("CharView", charPanel.transform, _viewer.Texture);
        UIKit.Place(view.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(720, 720));
        _viewer.view = view;
        _charText = UIKit.NewText("CharInfo", charPanel.transform, "플레이어", 20, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.88f));
        UIKit.Place(_charText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(720, 60));

        // 오른쪽: 탭 + 슬롯
        var right = UIKit.NewImage("RightPanel", _root, panelColor);
        UIKit.Place(right.rectTransform, new Vector2(1, 1), new Vector2(-40, -100), new Vector2(1020, 880));
        _tabImages = new Image[TabNames.Length];
        _tabLabels = new Text[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            int idx = i;
            var b = UIKit.NewButton("Tab_" + i, right.transform, TabNames[i], 22, buttonColor, MenuArt.LabelColor(false), () => SelectTab(idx));
            UIKit.Place(b.image.rectTransform, new Vector2(0, 1), new Vector2(20 + i * 196, -20), new Vector2(186, 60));
            MenuArt.SkinButton(b, false);   // 네이비 바탕 + 금색 테두리 아트(9-slice)
            _tabImages[i] = b.image;
            _tabLabels[i] = b.GetComponentInChildren<Text>();
        }
        _tabTitle = UIKit.NewText("TabTitle", right.transform, "", 28, TextAnchor.MiddleLeft, accent);
        UIKit.Place(_tabTitle.rectTransform, new Vector2(0, 1), new Vector2(24, -100), new Vector2(600, 44));

        var grid = UIKit.NewRect("Slots", right.transform);
        UIKit.Place(grid, new Vector2(0, 1), new Vector2(20, -160), new Vector2(980, 640));
        float cell = 150f, gap = 14f;
        for (int r = 0; r < slotRows; r++)
            for (int c = 0; c < slotColumns; c++)
            {
                // 빈 장비 칸: 탭 버튼과 같은 네이비 바탕 + 얇은 금색 테두리 아트(9-slice). 가짜 아이템은 넣지 않는다.
                var slot = UIKit.NewImage("Slot_" + r + "_" + c, grid, slotBorder);
                UIKit.Place(slot.rectTransform, new Vector2(0, 1), new Vector2(c * (cell + gap), -r * (cell + gap)), new Vector2(cell, cell));
                slot.raycastTarget = false;
                if (MenuArt.ButtonNormal != null)
                {
                    MenuArt.SetSelected(slot, false);
                    slot.color = slotDim;   // 버튼보다 한 단계 가라앉히기 — 장비 칸이 버튼처럼 보이지 않게
                }
                else
                {
                    // 아트가 없을 때의 대체: 테두리 색 위에 바탕을 한 겹 깔아 둔다.
                    var inner = UIKit.NewImage("Inner", slot.transform, slotFill);
                    UIKit.Stretch(inner.rectTransform, 2f);
                    inner.raycastTarget = false;
                }
            }
        _emptyText = UIKit.NewText("Empty", right.transform, "보유한 장비가 없습니다. (아이템 시스템은 추후 추가)", 20, TextAnchor.MiddleCenter, new Color(0.65f, 0.65f, 0.7f));
        UIKit.Place(_emptyText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(900, 40));
    }

    void SelectTab(int t)
    {
        _tab = t;
        for (int i = 0; i < _tabImages.Length; i++)
        {
            bool on = i == t;
            MenuArt.SetSelected(_tabImages[i], on);
            if (_tabLabels[i] != null) _tabLabels[i].color = MenuArt.LabelColor(on);
        }
        _tabTitle.text = TabNames[t];
    }

    void Refresh()
    {
        if (_root == null || !isOpen) return;
        var r = PlayerResources.Instance;
        if (_resBar != null) _resBar.SetValues(r != null ? r.wood : 0, r != null ? r.iron : 0, r != null ? r.gold : 0);
    }
}
