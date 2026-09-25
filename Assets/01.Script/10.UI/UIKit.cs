using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코드로 uGUI 를 만들 때 쓰는 공용 도우미 (MapSystem 의 NewRect/NewImage/NewText 와 같은 방식).
/// 이 프로젝트(HDRP, Linear)에서는 Image.color 가 인스펙터 값과 다르게 보이므로(§17 시행착오)
/// 색은 UIColor() 를 거쳐 넣는다.
/// </summary>
public static class UIKit
{
    static Font _font;
    static Sprite _white;

    public static Font Font
    {
        get { if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); return _font; }
    }

    public static Sprite White
    {
        get
        {
            if (_white == null)
            {
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                t.SetPixels32(px); t.Apply();
                _white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
            }
            return _white;
        }
    }

    static readonly System.Collections.Generic.Dictionary<string, Sprite> _solid = new System.Collections.Generic.Dictionary<string, Sprite>();

    /// <summary>
    /// 색을 텍스처에 구운 단색 스프라이트(색별 캐시). 이 프로젝트(HDRP, Linear)에서는 Image.color(정점색)가
    /// 인스펙터 값과 다르게 보여서(§17 시행착오) MapSystem.Tint 와 같은 방식으로 픽셀에 .linear 로 넣고 Image.color 는 흰색으로 둔다.
    /// </summary>
    public static Sprite SolidSprite(Color c)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c);
        Sprite s;
        if (_solid.TryGetValue(key, out s) && s != null) return s;
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color lc = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
        lc.a = c.a;
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = lc;
        t.SetPixels(px); t.Apply();
        s = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        _solid[key] = s;
        return s;
    }

    /// <summary>Image 의 색을 바꾼다(스프라이트 교체 방식).</summary>
    public static void SetColor(Image img, Color c) { img.sprite = SolidSprite(c); img.color = Color.white; }

    /// <summary>텍스트 등 정점색만 쓰는 곳용. (텍스트는 Text.color 그대로 보임)</summary>
    public static Color UIColor(Color c) { return c; }

    /// <summary>
    /// 장면(scene)에 EventSystem 이 없으면 만든다. 이것이 없으면 uGUI 버튼이 클릭을 전혀 받지 못한다
    /// (2026-09-19 확인: 장면에 EventSystem 이 없어 메뉴 버튼이 동작하지 않았음).
    /// 입력은 Input System 모듈을 쓴다(프로젝트 Active Input Handling = Both).
    /// </summary>
    public static UnityEngine.EventSystems.EventSystem EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) return es;
        var go = new GameObject("EventSystem");
        es = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        return es;
    }

    public static Canvas NewCanvas(string name, int sortingOrder)
    {
        EnsureEventSystem();
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>앵커 (ax,ay) 기준 위치·크기 지정.</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.type = Image.Type.Simple;
        SetColor(img, color);
        return img;
    }

    public static RawImage NewRawImage(string name, Transform parent, Texture tex)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex; img.color = Color.white;
        return img;
    }

    public static Text NewText(string name, Transform parent, string text, int size, TextAnchor align, Color color)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font; t.fontSize = size; t.text = text; t.alignment = align; t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>배경 이미지 + 라벨 텍스트를 가진 버튼.</summary>
    public static Button NewButton(string name, Transform parent, string label, int fontSize, Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage(name, parent, bg);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); colors.selectedColor = Color.white; colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        btn.colors = colors;
        var txt = NewText("Label", img.transform, label, fontSize, TextAnchor.MiddleCenter, fg);
        Stretch(txt.rectTransform, 4f);
        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    public static string Cost(Vector3Int c)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (c.x > 0) parts.Add("목재 " + c.x);
        if (c.y > 0) parts.Add("철 " + c.y);
        if (c.z > 0) parts.Add("금화 " + c.z);
        return parts.Count == 0 ? "무료" : string.Join("  ", parts.ToArray());
    }
}
