using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 하단의 플레이어 체력 게이지.
///
/// 씬에 캔버스를 미리 만들어 둘 필요가 없다. 없으면 시작할 때 스스로 만든다.
/// 이미 만들어 둔 것이 있으면 아래 참조 칸에 넣으면 그것을 쓴다.
///
/// 막대는 두 겹이다. 앞의 것은 현재 체력을 바로 따라가고,
/// 뒤의 붉은 것은 조금 늦게 따라와서 "방금 이만큼 깎였다" 가 눈에 보인다.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("체력을 표시할 배. 비우면 Player 태그를 가진 배를 찾는다.")]
    public ShipHealth target;

    [Header("모양")]
    [Tooltip("막대 너비(px, 1920 기준).")]
    public float barWidth = 560f;
    [Tooltip("막대 높이(px).")]
    public float barHeight = 26f;
    [Tooltip("화면 아래에서 띄울 거리(px).")]
    public float bottomMargin = 46f;
    [Tooltip("뒤쪽 붉은 막대가 따라오는 속도(초당 비율).")]
    public float delayedFollowSpeed = 0.35f;
    [Tooltip("뒤쪽 막대가 움직이기 시작하기까지의 시간(초).")]
    public float delayedHoldTime = 0.5f;

    [Header("색")]
    public Color colorHigh = new Color(0.35f, 0.82f, 0.45f, 1f);
    public Color colorMid = new Color(0.95f, 0.78f, 0.25f, 1f);
    public Color colorLow = new Color(0.90f, 0.28f, 0.22f, 1f);
    public Color colorDelayed = new Color(0.75f, 0.12f, 0.10f, 0.9f);

    [Header("직접 만든 UI 를 쓸 때 (선택)")]
    public Image fillImage;
    public Image delayedImage;
    public Text valueText;
    public Image regenMark;

    private float _shown;
    private float _delayed;
    private float _holdUntil;
    private Canvas _canvas;

    void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.GetComponent<ShipHealth>();
        }
        if (target == null)
        {
            Debug.LogWarning("[PlayerHealthUI] 표시할 ShipHealth 를 찾지 못했습니다.");
            enabled = false;
            return;
        }

        if (fillImage == null) BuildUI();

        _shown = target.HealthRatio;
        _delayed = _shown;
        target.OnDamaged += OnDamaged;
    }

    void OnDestroy()
    {
        if (target != null) target.OnDamaged -= OnDamaged;
    }

    void OnDamaged(float amount, Vector3 point)
    {
        _holdUntil = Time.time + delayedHoldTime;
    }

    void Update()
    {
        if (target == null) return;

        float r = target.HealthRatio;

        // 앞 막대는 바로 따라간다.
        _shown = Mathf.MoveTowards(_shown, r, Mathf.Max(0.6f, Mathf.Abs(_shown - r) * 8f) * Time.deltaTime);
        if (_shown < r) _shown = r;   // 회복 중에는 튀지 않게 바로 붙인다

        // 뒤 막대는 잠깐 멈췄다가 천천히 따라온다.
        if (_delayed > _shown)
        {
            if (Time.time >= _holdUntil)
                _delayed = Mathf.MoveTowards(_delayed, _shown, delayedFollowSpeed * Time.deltaTime);
        }
        else _delayed = _shown;

        if (fillImage != null)
        {
            fillImage.fillAmount = _shown;
            fillImage.color = ColorFor(r);
        }
        if (delayedImage != null) delayedImage.fillAmount = _delayed;

        if (valueText != null)
            valueText.text = Mathf.CeilToInt(target.currentHealth) + " / " + Mathf.RoundToInt(target.maxHealth);

        if (regenMark != null)
        {
            bool show = target.regenerating;
            Color c = regenMark.color;
            // 회복 중이면 부드럽게 깜빡인다.
            c.a = show ? 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 3f)) : 0f;
            regenMark.color = c;
        }
    }

    Color ColorFor(float ratio)
    {
        if (ratio > 0.5f) return Color.Lerp(colorMid, colorHigh, (ratio - 0.5f) / 0.5f);
        return Color.Lerp(colorLow, colorMid, ratio / 0.5f);
    }

    // ---------- 자동 생성 ----------

    void BuildUI()
    {
        var canvasGo = new GameObject("PlayerHUD");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 바깥 테두리
        RectTransform frame = NewRect("HealthFrame", canvasGo.transform,
            new Vector2(barWidth + 6f, barHeight + 6f), new Vector2(0f, bottomMargin));
        var frameImg = frame.gameObject.AddComponent<Image>();
        frameImg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
        frameImg.raycastTarget = false;

        // 안쪽 배경
        RectTransform bg = NewRect("Back", frame, new Vector2(barWidth, barHeight), Vector2.zero);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);
        bgImg.raycastTarget = false;

        // 뒤에서 천천히 따라오는 붉은 막대
        RectTransform del = NewRect("DelayedFill", bg, new Vector2(barWidth, barHeight), Vector2.zero);
        delayedImage = del.gameObject.AddComponent<Image>();
        delayedImage.sprite = WhiteSprite();
        delayedImage.color = colorDelayed;
        delayedImage.type = Image.Type.Filled;
        delayedImage.fillMethod = Image.FillMethod.Horizontal;
        delayedImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        delayedImage.raycastTarget = false;

        // 현재 체력 막대
        RectTransform fill = NewRect("Fill", bg, new Vector2(barWidth, barHeight), Vector2.zero);
        fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = WhiteSprite();
        fillImage.color = colorHigh;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.raycastTarget = false;

        // 회복 중 표시 (막대 위에 얇게 깔리는 밝은 띠)
        RectTransform mark = NewRect("RegenMark", bg, new Vector2(barWidth, 4f), new Vector2(0f, barHeight * 0.5f - 3f));
        regenMark = mark.gameObject.AddComponent<Image>();
        regenMark.color = new Color(0.6f, 0.95f, 1f, 0f);
        regenMark.raycastTarget = false;

        // 숫자
        RectTransform txt = NewRect("Value", frame, new Vector2(barWidth, barHeight), Vector2.zero);
        valueText = txt.gameObject.AddComponent<Text>();
        valueText.alignment = TextAnchor.MiddleCenter;
        valueText.fontSize = 15;
        valueText.color = new Color(1f, 1f, 1f, 0.92f);
        valueText.raycastTarget = false;
        valueText.font = FindFont();
        if (valueText.font == null) valueText.enabled = false;   // 폰트가 없으면 숫자만 숨긴다
    }

    private static Sprite _white;

    /// <summary>
    /// Image 의 Filled 타입은 스프라이트가 있어야 fillAmount 가 동작한다.
    /// 에셋을 따로 두지 않으려고 1x1 흰 스프라이트를 한 번만 만들어 돌려쓴다.
    /// </summary>
    static Sprite WhiteSprite()
    {
        if (_white != null) return _white;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        _white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        _white.hideFlags = HideFlags.HideAndDontSave;
        return _white;
    }

    static Font FindFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;
        string[] names = Font.GetOSInstalledFontNames();
        if (names != null && names.Length > 0) return Font.CreateDynamicFontFromOSFont(names[0], 16);
        return null;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;

        // 부모가 캔버스면 화면 하단 중앙에 붙이고, 아니면 부모 가운데에 겹친다.
        bool onCanvas = parent.GetComponent<Canvas>() != null;
        rt.anchorMin = onCanvas ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
        rt.anchorMax = rt.anchorMin;
        rt.pivot = new Vector2(0.5f, onCanvas ? 0f : 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }
}
