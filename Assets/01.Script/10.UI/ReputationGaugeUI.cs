using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현상금 아이콘(2026-10-07 — 예전 우호도 게이지를 바꿈, 사용자 지시 "관군 현상금 아이콘, 일본 현상금 아이콘으로 기존 게이지를 교체").
/// 체력바 바로 아래(예전 게이지 자리)에 왼쪽 "관군" 투구 5개, 오른쪽 "일본" 투구 5개. 단계만큼 아이콘이 진한 색으로 차고,
/// 아이콘 줄 아래 가는 막대가 다음 단계까지 얼마나 찼는지 보여 준다. 1단계 이상이면 그 진영이 공격한다 — 아래 글자로 상태 표시.
/// 단계가 오르면 그 아이콘이 잠깐 커졌다 돌아온다.
/// 아이콘 그림은 코드로 그린다(관군 = 둥근 투구 + 챙 + 꼭지, 일본 = 가부토 — 둥근 머리 + 넓게 퍼진 목가리개 + V자 뿔). 따로 에셋 없음.
/// 색은 UIKit 과 같이 픽셀에 .linear 로 구워 넣는다(HDRP 에서 Image.color 가 인스펙터 값과 다르게 보여서).
/// 클래스 이름은 씬 참조(스크립트 GUID)를 지키려고 그대로 둠.
/// </summary>
public class ReputationGaugeUI : MonoBehaviour
{
    public ReputationSystem source;
    [Tooltip("화면 아래에서 띄울 거리(px, 1920 기준). 체력바는 46 에서 시작.")] public float bottomMargin = 4f;
    [Tooltip("아이콘 한 칸 크기(px).")] public float iconSize = 24f;
    [Tooltip("아이콘 사이 간격(px).")] public float iconGap = 3f;
    [Tooltip("관군·일본 두 묶음 사이 간격(px).")] public float groupGap = 70f;
    public Color navyColor = new Color(0.36f, 0.58f, 1.00f);
    public Color japanColor = new Color(0.95f, 0.30f, 0.24f);
    [Tooltip("빈 칸 색 — 어두운 바다 위에서도 보이게 밝은 회색 반투명(처음 0.2 회색은 검은 테두리와 겹쳐 안 보였음).")]
    public Color emptyColor = new Color(0.62f, 0.64f, 0.68f, 0.55f);

    Image[] _navy, _japan;
    Image _navyBar, _japanBar;
    Text _status;
    int _shownNavy = -1, _shownJapan = -1;
    float _pulseNavy, _pulseJapan;
    Sprite _navyFull, _navyEmpty, _japanFull, _japanEmpty;

    void Start()
    {
        if (source == null) source = ReputationSystem.Instance != null ? ReputationSystem.Instance : FindFirstObjectByType<ReputationSystem>();
        if (source == null) { enabled = false; return; }
        _navyFull = IconSprite(true, navyColor); _navyEmpty = IconSprite(true, emptyColor);
        _japanFull = IconSprite(false, japanColor); _japanEmpty = IconSprite(false, emptyColor);
        Build();
    }

    void Build()
    {
        Canvas canvas = UIKit.NewCanvas("BountyHUD", 100);
        canvas.transform.SetParent(transform, false);

        float groupW = ReputationSystem.MaxLevel * iconSize + (ReputationSystem.MaxLevel - 1) * iconGap;
        RectTransform frame = UIKit.NewRect("BountyFrame", canvas.transform);
        UIKit.Place(frame, new Vector2(0.5f, 0f), new Vector2(0f, bottomMargin), new Vector2(groupW * 2f + groupGap + 120f, iconSize + 22f));

        float cx = groupGap * 0.5f + groupW * 0.5f;
        _navy = Group(frame, -cx, _navyEmpty, out _navyBar, navyColor);
        _japan = Group(frame, cx, _japanEmpty, out _japanBar, japanColor);
        Label(frame, "관군", new Vector2(-cx - groupW * 0.5f - 26f, 4f), TextAnchor.MiddleRight, navyColor);
        Label(frame, "일본", new Vector2(cx + groupW * 0.5f + 26f, 4f), TextAnchor.MiddleLeft, japanColor);
        _status = Label(frame, "", new Vector2(0f, 4f), TextAnchor.MiddleCenter, new Color(0.92f, 0.92f, 0.9f, 0.9f));
        _status.rectTransform.sizeDelta = new Vector2(groupGap + 10f, 16f);
        _status.fontSize = 11;
    }

    Image[] Group(RectTransform parent, float centerX, Sprite empty, out Image bar, Color c)
    {
        var icons = new Image[ReputationSystem.MaxLevel];
        float groupW = ReputationSystem.MaxLevel * iconSize + (ReputationSystem.MaxLevel - 1) * iconGap;
        float x0 = centerX - groupW * 0.5f + iconSize * 0.5f;
        for (int i = 0; i < icons.Length; i++)
        {
            var rt = UIKit.NewRect("Icon" + i, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = empty; img.color = Color.white; img.raycastTarget = false; img.preserveAspect = true;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            rt.anchoredPosition = new Vector2(x0 + i * (iconSize + iconGap), 4f);
            icons[i] = img;
        }
        var back = UIKit.NewImage("BarBack", parent, new Color(0.05f, 0.06f, 0.08f, 0.8f));
        back.raycastTarget = false;
        var brt = back.rectTransform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(groupW, 3f); brt.anchoredPosition = new Vector2(centerX, -iconSize * 0.5f - 2f);
        bar = UIKit.NewImage("Bar", back.transform, new Color(c.r, c.g, c.b, 0.9f));
        bar.raycastTarget = false;
        var frt = bar.rectTransform; frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f); frt.pivot = new Vector2(0f, 0.5f);
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero; frt.sizeDelta = new Vector2(0f, 0f);
        return icons;
    }

    Text Label(RectTransform parent, string s, Vector2 pos, TextAnchor align, Color c)
    {
        var txt = UIKit.NewText("Label", parent, s, 14, align, c);
        var rt = txt.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(44f, 18f);
        rt.anchoredPosition = pos;
        txt.raycastTarget = false;
        txt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return txt;
    }

    void Update()
    {
        if (source == null || _navy == null) return;
        int n = source.navyLevel, j = source.japanLevel;
        if (n > _shownNavy && _shownNavy >= 0) _pulseNavy = 1f;
        if (j > _shownJapan && _shownJapan >= 0) _pulseJapan = 1f;
        if (n != _shownNavy) { for (int i = 0; i < _navy.Length; i++) _navy[i].sprite = i < n ? _navyFull : _navyEmpty; _shownNavy = n; }
        if (j != _shownJapan) { for (int i = 0; i < _japan.Length; i++) _japan[i].sprite = i < j ? _japanFull : _japanEmpty; _shownJapan = j; }
        _pulseNavy = Pulse(_navy, n, _pulseNavy);
        _pulseJapan = Pulse(_japan, j, _pulseJapan);

        float groupW = ReputationSystem.MaxLevel * iconSize + (ReputationSystem.MaxLevel - 1) * iconGap;
        _navyBar.rectTransform.sizeDelta = new Vector2(groupW * BarFill(Faction.Navy, n), 0f);
        _japanBar.rectTransform.sizeDelta = new Vector2(groupW * BarFill(Faction.Japanese, j), 0f);

        string st = source.navyHostile && source.japaneseHostile ? "둘 다 적대" : source.navyHostile ? "◀ 관군 적대" : source.japaneseHostile ? "일본 적대 ▶" : "중립";
        if (_status.text != st) _status.text = st;
    }

    /// <summary>막대 = 전체 5단계 중 어디까지 왔는지(단계 + 다음 단계까지 비율) / 5.</summary>
    float BarFill(Faction f, int lv)
    {
        if (lv >= ReputationSystem.MaxLevel) return 1f;
        return (lv + source.ProgressToNext(f)) / ReputationSystem.MaxLevel;
    }

    float Pulse(Image[] icons, int lv, float t)
    {
        if (t <= 0f) return 0f;
        t = Mathf.Max(0f, t - Time.unscaledDeltaTime * 1.6f);
        int idx = Mathf.Clamp(lv - 1, 0, icons.Length - 1);
        float s = 1f + 0.45f * Mathf.Sin(t * Mathf.PI);
        icons[idx].rectTransform.localScale = new Vector3(s, s, 1f);
        if (t <= 0f) icons[idx].rectTransform.localScale = Vector3.one;
        return t;
    }

    // ================= 아이콘 그림 =================

    /// <summary>투구 실루엣(64², 안티에일리어싱) — joseon = 둥근 투구 + 챙 + 꼭지, 아니면 가부토(목가리개 + V자 뿔). 테두리는 검정.</summary>
    static Sprite IconSprite(bool joseon, Color c)
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
        Color lc = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c; lc.a = c.a;
        Color edge = new Color(0f, 0f, 0f, Mathf.Min(1f, c.a + 0.2f));
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // 4×4 표본으로 덮인 비율(안티에일리어싱), 테두리는 바깥 1.5 px
                float cover = 0f, coverOut = 0f;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 4f) / S * 2f - 1f;   // −1..1
                        float v = (y + (sy + 0.5f) / 4f) / S * 2f - 1f;
                        float d = joseon ? Joseon(u, v) : Kabuto(u, v);
                        if (d <= 0f) cover += 1f / 16f;
                        if (d <= 0.06f) coverOut += 1f / 16f;
                    }
                Color p = Color.Lerp(edge, lc, cover);
                p.a = Mathf.Max(cover * lc.a, coverOut * edge.a);
                if (coverOut <= 0f) p = new Color(0f, 0f, 0f, 0f);
                px[y * S + x] = p;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    // 모양 함수: 음수 = 안쪽(근사 거리, 정규화 좌표 −1..1, v 위쪽 +)
    static float Circle(float u, float v, float cx, float cy, float r) { return Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r; }
    static float Box(float u, float v, float cx, float cy, float hx, float hy) { float dx = Mathf.Abs(u - cx) - hx, dy = Mathf.Abs(v - cy) - hy; return Mathf.Max(dx, dy); }
    static float Union(float a, float b) { return Mathf.Min(a, b); }

    static float Ellipse(float u, float v, float cx, float cy, float rx, float ry) { float a = (u - cx) / rx, b = (v - cy) / ry; return Mathf.Sqrt(a * a + b * b) - 1f; }

    static float Joseon(float u, float v)
    {
        // 조선 투구: 반타원 머리 + 챙 + 꼭지·술 + 아래로 조금 퍼지는 드림(귀·목 가리개), 얼굴 자리는 비움
        float dome = Mathf.Max(Ellipse(u, v, 0f, -0.12f, 0.56f, 0.62f), -(v + 0.12f));
        float brim = Box(u, v, 0f, -0.17f, 0.74f, 0.06f);
        float knob = Box(u, v, 0f, 0.55f, 0.05f, 0.10f);
        float tassel = Circle(u, v, 0f, 0.72f, 0.10f);
        float t = Mathf.Clamp01((-0.23f - v) / 0.5f);
        float halfW = 0.5f + 0.12f * t;
        float flap = Mathf.Max(Mathf.Abs(u) - halfW, Mathf.Max(v + 0.23f, -0.73f - v));
        flap = Mathf.Max(flap, -Box(u, v, 0f, -0.48f, 0.20f, 0.26f));
        return Union(Union(Union(dome, brim), Union(knob, tassel)), flap);
    }

    static float Kabuto(float u, float v)
    {
        // 가부토: 둥근 머리 + 차양 + 아래로 넓게 퍼지는 목가리개(얼굴 자리는 비움) + V 자 뿔 + 앞 장식
        float dome = Mathf.Max(Ellipse(u, v, 0f, -0.08f, 0.5f, 0.5f), -(v + 0.08f));
        float visor = Box(u, v, 0f, -0.10f, 0.60f, 0.05f);
        float t = Mathf.Clamp01((-0.14f - v) / 0.5f);
        float halfW = 0.52f + 0.32f * t;
        float shikoro = Mathf.Max(Mathf.Abs(u) - halfW, Mathf.Max(v + 0.14f, -0.64f - v));
        shikoro = Mathf.Max(shikoro, -Box(u, v, 0f, -0.42f, 0.22f, 0.30f));
        float horn = Mathf.Min(Segment(u, v, 0.06f, 0.28f, 0.48f, 0.86f), Segment(u, v, -0.06f, 0.28f, -0.48f, 0.86f)) - 0.065f;
        float crest = Circle(u, v, 0f, 0.30f, 0.10f);
        return Union(Union(Union(dome, visor), shikoro), Union(horn, crest));
    }

    static float Segment(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
        float t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy));
        float dx = wx - vx * t, dy = wy - vy * t;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
