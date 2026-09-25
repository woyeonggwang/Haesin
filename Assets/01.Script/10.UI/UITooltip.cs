using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 마우스를 올리면 짧은 이름표를 띄우는 아주 단순한 툴팁. 자원 아이콘에 자원 이름을 붙이는 데 쓴다.
/// 게임이 멈춰 있어도(timeScale 0) 동작하도록 애니메이션 없이 즉시 보이고 사라진다.
/// 이름표는 같은 캔버스의 맨 마지막 자식으로 하나만 만들어 재사용한다.
/// </summary>
public class UITooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string text;

    static RectTransform _panel;
    static Text _label;
    static Canvas _canvas;

    /// <summary>대상에 툴팁을 붙인다. Graphic 이 있어야 마우스 판정이 되므로 raycastTarget 을 켠다.</summary>
    public static UITooltip Attach(GameObject target, string text)
    {
        if (target == null) return null;
        var graphic = target.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = true;
        var tip = target.GetComponent<UITooltip>();
        if (tip == null) tip = target.AddComponent<UITooltip>();
        tip.text = text;
        return tip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(text)) return;
        EnsurePanel(GetComponentInParent<Canvas>());
        if (_panel == null) return;
        _label.text = text;
        _panel.sizeDelta = new Vector2(Mathf.Max(84f, _label.preferredWidth + 26f), 34f);   // 글자 폭에 맞춘다
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        Place(eventData);
    }

    public void OnPointerExit(PointerEventData eventData) { Hide(); }
    void OnDisable() { Hide(); }

    static void Hide()
    {
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    void Place(PointerEventData eventData)
    {
        var canvasRect = _canvas.transform as RectTransform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, null, out local)) return;

        // 커서 오른쪽 아래에 두되 캔버스 밖으로 나가지 않게 민다.
        Vector2 size = _panel.sizeDelta;
        Rect area = canvasRect.rect;
        float x = Mathf.Clamp(local.x + 18f, area.xMin + 4f, area.xMax - size.x - 4f);
        float y = Mathf.Clamp(local.y - 16f, area.yMin + size.y + 4f, area.yMax - 4f);
        _panel.anchoredPosition = new Vector2(x, y);
    }

    static void EnsurePanel(Canvas canvas)
    {
        if (canvas == null) return;
        if (_panel != null && _canvas == canvas) return;

        _canvas = canvas;
        var root = UIKit.NewRect("Tooltip", canvas.transform);
        var bg = root.gameObject.AddComponent<Image>();
        bg.sprite = UIKit.SolidSprite(new Color(0.06f, 0.07f, 0.11f, 0.96f));
        bg.color = Color.white;
        bg.raycastTarget = false;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(150f, 34f);

        var border = UIKit.NewImage("Border", root, new Color(0.55f, 0.45f, 0.24f, 0.9f));
        UIKit.Stretch(border.rectTransform, -1f);
        border.raycastTarget = false;
        border.transform.SetAsFirstSibling();

        _label = UIKit.NewText("Label", root, "", 18, TextAnchor.MiddleCenter, new Color(0.95f, 0.93f, 0.85f));
        UIKit.Stretch(_label.rectTransform, 6f);
        _label.raycastTarget = false;

        _panel = root;
        _panel.gameObject.SetActive(false);
    }
}
