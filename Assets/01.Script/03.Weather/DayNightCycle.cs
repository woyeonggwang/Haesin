using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 밤낮(2026-10-09, 사용자 지시 "밤낮 시스템 넣어주세요, 낮인데 해가 없더라고요 해도 보이게"). 씬 루트 `DayNightCycle`.
///   - 시각 timeOfDay(0~24 시)가 흐름: 낮(06~18 시)은 dayMinutes 분, 밤(18~06 시)은 nightMinutes 분 동안(실제 시간).
///   - 해(Directional Light): 6 시 동쪽에서 떠 12 시 남쪽 maxSunElevation 도, 18 시 서쪽으로 짐(북 = 월드 +Z, 동 = +X).
///     하늘은 Physically Based Sky 라 해 원반이 보이고, 해 높이에 따라 하늘색·노을이 저절로 바뀜.
///   - 밤에는 달(두 번째 Directional Light, 해 반대편)이 그림자를 맡고, 해는 지평선 아래에서 끔(방향광 그림자 하나만).
///   - 해 밝기는 WeatherSystem 이 날씨마다 정한 Lux × SunScale(해 높이 −2° → 0, 12° → 1).
///   - 밤 밝기: 전역 볼륨(런타임 사본)에 Exposure·IndirectLightingController 덮어쓰기 — 자동 노출 하한(limitMin)을 밤에 올려
///     밤이 낮처럼 밝아지지 않게 하고, 베이크된 APV 간접광(낮 해 기준)을 밤에 줄임. 동굴 볼륨(CaveInteriorVolume)도 같은 비율로.
///   - 시험용: skipKey(기본 = 키) 누르면 1 시간 건너뜀. None 이면 끔.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }
    /// <summary>해 밝기 배율(0~1) — WeatherSystem 이 날씨 Lux 에 곱함.</summary>
    public static float SunScale = 1f;

    [Header("시각")]
    [Range(0f, 24f)] public float timeOfDay = 9f;
    [Tooltip("06~18 시가 흐르는 실제 시간(분).")] public float dayMinutes = 16f;
    [Tooltip("18~06 시가 흐르는 실제 시간(분).")] public float nightMinutes = 6f;
    public bool paused;

    [Header("해·달")]
    public Light sun;
    public Light moon;
    [Tooltip("정오의 해 높이(도).")] public float maxSunElevation = 60f;
    [Tooltip("해가 뜨고 지는 방향을 돌림(도). 0 = 동쪽(+X)에서 뜸.")] public float azimuthOffset = 0f;
    [Tooltip("달 밝기(Lux, 가장 높을 때).")] public float moonLux = 2.5f;
    public Color moonColor = new Color(0.72f, 0.80f, 1f);

    [Header("노출·간접광 (밤 ↔ 낮)")]
    public Volume globalVolume;
    public Volume caveVolume;
    [Tooltip("낮 자동 노출 하한(EV100).")] public float dayExposureMin = -1f;
    [Tooltip("밤 자동 노출 하한(EV100) — 높을수록 밤이 어두움.")] public float nightExposureMin = 1f;
    public float exposureMax = 14f;
    [Tooltip("밤 간접광 배율 — 베이크된 APV 는 낮 해로 구운 값이라 밤 노출에서 그대로 두면 바위가 하얗게 빛남(2026-10-09 캡처). 낮 ↔ 밤을 로그로 보간.")] [Range(0f, 1f)] public float nightIndirect = 0.002f;
    [Tooltip("밤 반사 프로브 배율(동굴 리플렉션 프로브도 낮에 구운 것).")] [Range(0f, 1f)] public float nightReflectionProbes = 0.002f;
    [Tooltip("동굴 볼륨 밤 노출 하한(낮은 씬 값 그대로).")] public float caveNightExposureMin = 3f;

    [Header("시험")]
    public KeyCode skipKey = KeyCode.Equals;

    [Header("읽기 전용")]
    public float sunElevation;
    [Range(0f, 1f)] public float daylight;

    HDAdditionalLightData _sunHD, _moonHD;
    Exposure _exp; IndirectLightingController _ind;
    Exposure _caveExp; IndirectLightingController _caveInd;
    float _caveDayMin, _caveDayMax, _caveDayIndDiff = 1f, _caveDayIndProbe = 1f;
    bool _caveInit;

    void Awake() { if (Instance == null) Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; SunScale = 1f; } }

    void Start()
    {
        if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l != moon && l.name.Contains("Directional")) { sun = l; break; }
        if (sun != null) _sunHD = sun.GetComponent<HDAdditionalLightData>();
        if (moon != null) _moonHD = moon.GetComponent<HDAdditionalLightData>();
        if (globalVolume == null) globalVolume = WeatherSystem.FindSkyVolume();
        if (caveVolume == null) { var g = GameObject.Find("CaveInteriorVolume"); if (g != null) caveVolume = g.GetComponent<Volume>(); }
        if (globalVolume != null)
        {
            var p = globalVolume.profile; // 런타임 사본(WeatherSystem 과 같은 사본)
            if (!p.TryGet(out _exp)) _exp = p.Add<Exposure>(false);
            _exp.active = true;
            _exp.mode.Override(ExposureMode.Automatic);
            if (!p.TryGet(out _ind)) _ind = p.Add<IndirectLightingController>(false);
            _ind.active = true;
        }
        if (caveVolume != null)
        {
            var p = caveVolume.profile;
            if (p.TryGet(out _caveExp)) { _caveDayMin = _caveExp.limitMin.value; _caveDayMax = _caveExp.limitMax.value; }
            if (p.TryGet(out _caveInd)) { _caveDayIndDiff = _caveInd.indirectDiffuseLightingMultiplier.value; _caveDayIndProbe = _caveInd.reflectionProbeIntensityMultiplier.value; }
            _caveInit = true;
        }
        Apply();
    }

    void Update()
    {
        if (!paused)
        {
            bool day = timeOfDay >= 6f && timeOfDay < 18f;
            float minutes = Mathf.Max(0.1f, day ? dayMinutes : nightMinutes);
            timeOfDay += Time.deltaTime * 12f / (minutes * 60f);
        }
        if (skipKey != KeyCode.None && Input.GetKeyDown(skipKey)) timeOfDay += 1f;
        if (timeOfDay >= 24f) timeOfDay -= 24f;
        Apply();
    }

    /// <summary>해 방향(빛이 나아가는 방향)과 높이(도).</summary>
    public static Vector3 SunForward(float hour, float maxElev, float azOffset, out float elevation)
    {
        float a = (hour - 6f) / 12f * Mathf.PI;               // 6 시 0, 12 시 π/2, 18 시 π
        elevation = Mathf.Asin(Mathf.Sin(a) * Mathf.Sin(maxElev * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
        // 해가 있는 쪽(하늘 방향): 동(+X) → 남(−Z) → 서(−X)
        float az = a + azOffset * Mathf.Deg2Rad;
        Vector3 horiz = new Vector3(Mathf.Cos(az), 0f, -Mathf.Sin(az) * Mathf.Cos(maxElev * Mathf.Deg2Rad));
        if (horiz.sqrMagnitude < 1e-6f) horiz = Vector3.right;
        horiz.Normalize();
        float e = elevation * Mathf.Deg2Rad;
        Vector3 toSun = horiz * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
        return -toSun;
    }

    void Apply()
    {
        float elev;
        Vector3 fwd = SunForward(timeOfDay, maxSunElevation, azimuthOffset, out elev);
        sunElevation = elev;
        SunScale = Smooth(-2f, 12f, elev);
        float moonK = Smooth(-2f, 12f, -elev);
        daylight = Smooth(-8f, 8f, elev);

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            bool on = elev > -4f;
            if (sun.enabled != on) sun.enabled = on;
        }
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            bool on = elev < 2f;
            if (moon.enabled != on) moon.enabled = on;
            moon.color = moonColor;
            float lux = moonLux * moonK;
            if (_moonHD != null) _moonHD.SetIntensity(lux, LightUnit.Lux); else moon.intensity = lux;
        }

        if (_exp != null)
        {
            _exp.limitMin.Override(Mathf.Lerp(nightExposureMin, dayExposureMin, daylight));
            _exp.limitMax.Override(exposureMax);
        }
        float kd = LogLerp(nightIndirect, 1f, daylight), kr = LogLerp(nightReflectionProbes, 1f, daylight);
        if (_ind != null)
        {
            _ind.indirectDiffuseLightingMultiplier.Override(kd);
            _ind.reflectionProbeIntensityMultiplier.Override(kr);
        }
        if (_caveInit)
        {
            if (_caveExp != null) _caveExp.limitMin.Override(Mathf.Lerp(caveNightExposureMin, _caveDayMin, daylight));
            if (_caveInd != null)
            {
                _caveInd.indirectDiffuseLightingMultiplier.Override(_caveDayIndDiff * kd);
                _caveInd.reflectionProbeIntensityMultiplier.Override(_caveDayIndProbe * kr);
            }
        }
    }

    static float LogLerp(float a, float b, float t) { a = Mathf.Max(a, 1e-5f); b = Mathf.Max(b, 1e-5f); return Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t)); }

    static float Smooth(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3f - 2f * t); }

    /// <summary>"오후 3:20" 같은 시각 글.</summary>
    public string ClockText()
    {
        int h = Mathf.FloorToInt(timeOfDay) % 24; int m = Mathf.FloorToInt((timeOfDay - Mathf.Floor(timeOfDay)) * 60f);
        return (h < 12 ? "오전 " : "오후 ") + (h % 12 == 0 ? 12 : h % 12) + ":" + m.ToString("00");
    }
}
