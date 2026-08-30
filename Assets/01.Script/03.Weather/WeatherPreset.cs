using UnityEngine;

/// <summary>
/// 날씨 하나의 목표값 묶음. WeatherSystem 이 이 값들 사이를 서서히 보간한다.
/// 값을 쓰지 않는 항목은 그 기능이 씬에 없으면 자동으로 무시된다.
/// </summary>
[System.Serializable]
public class WeatherPreset
{
    [Tooltip("표시용 이름.")]
    public string name = "맑음";

    [Tooltip("이 날씨가 뽑힐 상대 확률. 0이면 자동 선택에서 제외된다.")]
    [Range(0f, 10f)]
    public float weight = 1f;

    [Header("유지 시간 (초)")]
    [Tooltip("이 날씨가 유지되는 최소 시간.")]
    public float minHoldSeconds = 60f;
    [Tooltip("이 날씨가 유지되는 최대 시간.")]
    public float maxHoldSeconds = 150f;

    [Header("바다")]
    [Tooltip("파고를 결정하는 바람 세기(0~250).")]
    public float windSpeed = 30f;
    [Tooltip("파도가 여러 방향으로 흩어지는 정도(0~1).")]
    [Range(0f, 1f)]
    public float chaos = 0.8f;
    [Tooltip("잔물결 바람(0~15).")]
    [Range(0f, 15f)]
    public float ripplesWindSpeed = 10f;
    [Tooltip("바다 전체에 생기는 흰 파도의 양(0~1).")]
    [Range(0f, 1f)]
    public float foamAmount = 0.4f;

    [Header("구름")]
    [Tooltip("구름 짙기. CloudLayer 는 opacity, 볼류메트릭 구름은 밀도로 들어간다.")]
    [Range(0f, 1f)]
    public float cloudOpacity = 0.6f;
    [Tooltip("구름 색. 흐리거나 비 올 때는 회색으로.")]
    public Color cloudTint = Color.white;
    [Tooltip("구름 밝기 보정(EV). 낮출수록 어두운 구름.")]
    public float cloudExposure = 0f;
    [Tooltip("구름이 흘러가는 속도. 강풍일 때 올린다.")]
    public float cloudScrollSpeed = 1f;
    [Tooltip("구름 양. 0이면 한 종류만, 1이면 여러 겹이 쌓여 하늘을 덮는다. 이게 '구름이 많아지는' 실제 조절값이다.")]
    [Range(0f, 1f)]
    public float cloudCoverage = 0.15f;
    [Tooltip("구름 두께. 두꺼울수록 무겁고 어둡게 보인다.")]
    [Range(0f, 1f)]
    public float cloudThickness = 0.5f;

    [Header("하늘 / 밝기")]
    [Tooltip("하늘 밝기(EV). 낮출수록 어두워진다.")]
    public float skyExposure = 14f;
    [Tooltip("태양 밝기(Lux).")]
    public float sunLux = 67000f;
    [Tooltip("태양 색.")]
    public Color sunColor = new Color(1f, 0.985f, 0.95f, 1f);

    [Header("안개 / 시야")]
    [Tooltip("안개가 옅어지는 거리(m). 작을수록 뿌옇다.")]
    public float fogMeanFreePath = 400f;
    [Tooltip("안개 색조.")]
    public Color fogTint = Color.white;

    [Header("비")]
    [Tooltip("0이면 비 없음, 1이면 최대 강수.")]
    [Range(0f, 1f)]
    public float rainIntensity = 0f;    [Tooltip("빗방울 낙하 속도 배수. RainPrefab 기본 속도의 몇 배로 떨어질지. 1 = 원래 속도.")]
    [Range(0.2f, 8f)]
    public float rainFallSpeedMultiplier = 1.6f;


    /// <summary>두 프리셋 사이를 보간한 값을 만든다.</summary>
    public static void Lerp(WeatherPreset a, WeatherPreset b, float t, WeatherPreset result)
    {
        result.windSpeed        = Mathf.Lerp(a.windSpeed, b.windSpeed, t);
        result.chaos            = Mathf.Lerp(a.chaos, b.chaos, t);
        result.ripplesWindSpeed = Mathf.Lerp(a.ripplesWindSpeed, b.ripplesWindSpeed, t);
        result.foamAmount       = Mathf.Lerp(a.foamAmount, b.foamAmount, t);

        result.cloudOpacity     = Mathf.Lerp(a.cloudOpacity, b.cloudOpacity, t);
        result.cloudTint        = Color.Lerp(a.cloudTint, b.cloudTint, t);
        result.cloudExposure    = Mathf.Lerp(a.cloudExposure, b.cloudExposure, t);
        result.cloudScrollSpeed = Mathf.Lerp(a.cloudScrollSpeed, b.cloudScrollSpeed, t);
        result.cloudCoverage    = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t);
        result.cloudThickness   = Mathf.Lerp(a.cloudThickness, b.cloudThickness, t);

        result.skyExposure      = Mathf.Lerp(a.skyExposure, b.skyExposure, t);
        result.sunLux           = Mathf.Lerp(a.sunLux, b.sunLux, t);
        result.sunColor         = Color.Lerp(a.sunColor, b.sunColor, t);

        result.fogMeanFreePath  = Mathf.Lerp(a.fogMeanFreePath, b.fogMeanFreePath, t);
        result.fogTint          = Color.Lerp(a.fogTint, b.fogTint, t);

        result.rainIntensity    = Mathf.Lerp(a.rainIntensity, b.rainIntensity, t);        result.rainFallSpeedMultiplier = Mathf.Lerp(a.rainFallSpeedMultiplier, b.rainFallSpeedMultiplier, t);

    }
}
