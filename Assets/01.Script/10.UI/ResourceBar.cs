using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자원 아이콘 + 수량 숫자 한 줄. 인벤토리·함선 메뉴 상단과 강화 비용 표시에 같은 모양으로 쓴다.
/// 순서는 항상 목재 / 철 / 금화 (MenuArt.ResourceNames).
///
/// 레이아웃은 HorizontalLayoutGroup + ContentSizeFitter 로 잡는다. 숫자가 커져도 Text 가
/// 스스로 필요한 폭을 알려 주므로 아이콘과 숫자가 겹치지 않고, 화면이 작아 UI 가 줄어도 비율만 달라진다.
/// 아이콘은 preserveAspect 로 비율을 유지한다.
/// </summary>
public class ResourceBar
{
    public RectTransform Root { get; private set; }

    readonly RectTransform[] _entries = new RectTransform[3];
    readonly Text[] _counts = new Text[3];
    Text _emptyLabel;

    /// <summary>
    /// 자원 줄을 만든다. tooltips 가 true 면 아이콘에 자원 이름 툴팁을 붙인다.
    /// emptyText 는 표시할 자원이 하나도 없을 때 대신 보여줄 글자(비용 줄에서 "무료" 등). 비우면 아무것도 안 나온다.
    /// </summary>
    public static ResourceBar Create(Transform parent, string name, float iconSize, int fontSize, Color textColor, bool tooltips, string emptyText)
    {
        var bar = new ResourceBar();
        var root = UIKit.NewRect(name, parent);
        var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = iconSize * 0.75f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bar.Root = root;

        for (int i = 0; i < 3; i++)
        {
            var entry = UIKit.NewRect("Res_" + MenuArt.ResourceNames[i], root);
            var entryLayout = entry.gameObject.AddComponent<HorizontalLayoutGroup>();
            entryLayout.spacing = iconSize * 0.28f;
            entryLayout.childAlignment = TextAnchor.MiddleLeft;
            entryLayout.childControlWidth = true; entryLayout.childControlHeight = true;
            entryLayout.childForceExpandWidth = false; entryLayout.childForceExpandHeight = false;
            var entryFitter = entry.gameObject.AddComponent<ContentSizeFitter>();
            entryFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            entryFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconRect = UIKit.NewRect("Icon", entry);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = MenuArt.ResourceIcon(i);
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = tooltips;
            var iconSizeElement = iconRect.gameObject.AddComponent<LayoutElement>();
            iconSizeElement.preferredWidth = iconSize; iconSizeElement.preferredHeight = iconSize;
            iconSizeElement.minWidth = iconSize; iconSizeElement.minHeight = iconSize;
            if (tooltips) UITooltip.Attach(iconRect.gameObject, MenuArt.ResourceNames[i]);

            var count = UIKit.NewText("Count", entry, "0", fontSize, TextAnchor.MiddleLeft, textColor);
            count.horizontalOverflow = HorizontalWrapMode.Overflow;
            var countElement = count.gameObject.AddComponent<LayoutElement>();
            countElement.minHeight = iconSize;

            bar._entries[i] = entry;
            bar._counts[i] = count;
        }

        if (!string.IsNullOrEmpty(emptyText))
        {
            bar._emptyLabel = UIKit.NewText("Empty", root, emptyText, fontSize, TextAnchor.MiddleLeft, textColor);
            bar._emptyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            bar._emptyLabel.gameObject.SetActive(false);
        }

        return bar;
    }

    public GameObject GameObject { get { return Root != null ? Root.gameObject : null; } }

    public void SetActive(bool on) { if (Root != null) Root.gameObject.SetActive(on); }

    /// <summary>보유 자원 표시(0 이어도 보여준다).</summary>
    public void SetValues(int wood, int iron, int gold)
    {
        Show(0, true, wood); Show(1, true, iron); Show(2, true, gold);
        if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(false);
        Rebuild();
    }

    /// <summary>비용 표시(0 인 자원은 숨긴다). 전부 0 이면 emptyText 를 보여준다.</summary>
    public void SetCost(Vector3Int cost)
    {
        Show(0, cost.x > 0, cost.x); Show(1, cost.y > 0, cost.y); Show(2, cost.z > 0, cost.z);
        bool none = cost.x <= 0 && cost.y <= 0 && cost.z <= 0;
        if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(none);
        Rebuild();
    }

    void Show(int index, bool visible, int value)
    {
        if (_entries[index] == null) return;
        if (_entries[index].gameObject.activeSelf != visible) _entries[index].gameObject.SetActive(visible);
        if (visible) _counts[index].text = value.ToString();
    }

    void Rebuild()
    {
        if (Root != null) LayoutRebuilder.MarkLayoutForRebuild(Root);
    }
}
