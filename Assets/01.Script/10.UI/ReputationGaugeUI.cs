using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 우호도 게이지(체력바 바로 아래). 왼쪽 끝 = 관군 쪽, 오른쪽 끝 = 일본 쪽, 가운데 = 중립.
/// 양 끝의 진한 구간(±hostileThreshold 밖)에 표시가 들어가면 반대 진영이 공격한다 — 가운데 글자로 상태를 보여 준다.
/// 색은 UIKit.SolidSprite 로 구움(HDRP 에서 Image.color 가 인스펙터 값과 다르게 보여서).
/// </summary>
public class ReputationGaugeUI : MonoBehaviour
{
    public ReputationSystem source;
    [Tooltip("막대 너비(px, 1920 기준). 체력바(560)와 같게.")] public float barWidth = 560f;
    public float barHeight = 10f;
    [Tooltip("화면 아래에서 띄울 거리(px). 체력바는 46 에서 시작.")] public float bottomMargin = 22f;
    public Color navyColor = new Color(0.30f, 0.52f, 0.95f);
    public Color japanColor = new Color(0.90f, 0.28f, 0.22f);
    public Color neutralColor = new Color(0.62f, 0.62f, 0.58f);

    RectTransform _marker;
    Text _status;
    float _shown;

    void Start()
    {
        if (source == null) source = ReputationSystem.Instance != null ? ReputationSystem.Instance : FindFirstObjectByType<ReputationSystem>();
        if (source == null) { enabled = false; return; }
        Build();
        _shown = source.standing;
    }

    void Build()
    {
        Canvas canvas = UIKit.NewCanvas("ReputationHUD", 100);
        canvas.transform.SetParent(transform, false);

        RectTransform frame = UIKit.NewRect("GaugeFrame", canvas.transform);
        UIKit.Place(frame, new Vector2(0.5f, 0f), new Vector2(0f, bottomMargin), new Vector2(barWidth + 6f, barHeight + 6f));
        UIKit.Stretch(UIKit.NewImage("Frame", frame, new Color(0.05f, 0.06f, 0.08f, 0.85f)).rectTransform, 0f);

        float t = source.hostileThreshold / 100f;           // 0..1 반쪽 비율
        float half = barWidth * 0.5f;
        float zone = half * (1f - t);                        // 적대 구간 폭(한쪽)
        // 왼쪽 적대 구간(관군 쪽 끝 → 일본군이 적) / 중립 / 오른쪽 적대 구간(일본 쪽 끝 → 관군이 적)
        Segment(frame, -half + zone * 0.5f, zone, Dim(navyColor, 0.85f));
        Segment(frame, 0f, barWidth - zone * 2f, Dim(neutralColor, 0.9f));
        Segment(frame, half - zone * 0.5f, zone, Dim(japanColor, 0.85f));
        Segment(frame, 0f, 2f, new Color(0.9f, 0.9f, 0.85f, 1f)); // 가운데 눈금

        _marker = UIKit.NewImage("Marker", frame, new Color(1f, 0.97f, 0.88f, 1f)).rectTransform;
        _marker.anchorMin = _marker.anchorMax = new Vector2(0.5f, 0.5f);
        _marker.sizeDelta = new Vector2(5f, barHeight + 10f);

        Label(frame, "관군", new Vector2(-half - 34f, 0f), TextAnchor.MiddleRight, navyColor);
        Label(frame, "일본", new Vector2(half + 34f, 0f), TextAnchor.MiddleLeft, japanColor);
        _status = Label(frame, "", new Vector2(0f, -barHeight - 6f), TextAnchor.MiddleCenter, new Color(0.92f, 0.92f, 0.9f, 0.9f));
        _status.rectTransform.sizeDelta = new Vector2(barWidth, 16f);
        _status.fontSize = 12;
    }

    static Color Dim(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    void Segment(RectTransform parent, float x, float w, Color c)
    {
        var img = UIKit.NewImage("Seg", parent, c);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Mathf.Max(1f, w), barHeight);
        rt.anchoredPosition = new Vector2(x, 0f);
        img.raycastTarget = false;
    }

    Text Label(RectTransform parent, string s, Vector2 pos, TextAnchor align, Color c)
    {
        var txt = UIKit.NewText("Label", parent, s, 14, align, c);
        var rt = txt.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(60f, 18f);
        rt.anchoredPosition = pos;
        txt.raycastTarget = false;
        return txt;
    }

    void Update()
    {
        if (source == null || _marker == null) return;
        _shown = Mathf.MoveTowards(_shown, source.standing, Mathf.Max(20f, Mathf.Abs(_shown - source.standing) * 6f) * Time.unscaledDeltaTime);
        _marker.anchoredPosition = new Vector2(_shown / 100f * barWidth * 0.5f, 0f);
        string st = source.navyHostile ? "관군이 적대합니다" : source.japaneseHostile ? "일본군이 적대합니다" : "중립 — 관군·일본군 모두 공격하지 않음";
        if (_status.text != st) _status.text = st;
    }
}
