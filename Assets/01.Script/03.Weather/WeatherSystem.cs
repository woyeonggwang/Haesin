using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using DigitalRuby.RainMaker;

/// <summary>
/// 자동 날씨 시스템.
///
/// 프리셋을 가중치로 뽑아 일정 시간 유지하고, 다음 날씨로 서서히 넘어간다.
/// 바다(파고/거품), 구름(짙기/색/속도), 하늘과 태양 밝기, 안개, 비를 한꺼번에 움직인다.
///
/// 씬에 없는 기능은 자동으로 건너뛴다.
/// 구름은 CloudLayer 와 Volumetric Clouds 둘 다,
/// 하늘은 GradientSky 와 Physically Based Sky 둘 다 지원한다.
///
/// 볼륨 프로파일은 런타임 사본(volume.profile)만 건드리므로
/// 에셋 파일의 원래 값은 바뀌지 않는다.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    [Header("대상 (비우면 자동 탐색)")]
    public Volume skyVolume;
    public WaterSurface ocean;
    public Light sun;
    [Tooltip("비가 따라다닐 대상. 배를 넣으면 배 위에서 비가 내린다. 비우면 메인 카메라.")]
    public Transform rainFollowTarget;
    [Tooltip("대상의 회전은 무시하고 위치만 따라간다. 배가 출렁여도 비가 같이 기울지 않는다.")]
    public bool rainIgnoreTargetRotation = true;

    [Header("날씨 목록")]
    public List<WeatherPreset> presets = new List<WeatherPreset>();

    [Header("진행")]
    [Tooltip("켜면 시간이 지나면서 날씨가 저절로 바뀐다.")]
    public bool autoChange = true;
    [Tooltip("날씨가 바뀌는 데 걸리는 시간(초).")]
    public float transitionSeconds = 25f;
    [Tooltip("시작할 때 사용할 프리셋 번호.")]
    public int startIndex = 0;
    [Tooltip("같은 날씨가 연속으로 뽑히지 않게 한다.")]
    public bool avoidRepeat = true;

    [Header("비 파티클")]
    [Tooltip("RainMaker 의 RainPrefab. 넣으면 이걸 쓰고, 아래 자체 제작 파티클은 무시된다.")]
    public GameObject rainPrefab;
    [Tooltip("RainMaker 는 사람 크기 기준이라 판옥선(38m) 옆에서는 좁다. 방출 영역만 이 배수로 넓힌다.")]
    public float rainAreaScale = 5f;
    [Tooltip("빗방울 크기. 영역을 넓히면 그만큼 키워야 눈에 보인다. 0이면 프리팹 기본값 유지.")]
    public float rainFallDropSize = 0.45f;
    [Tooltip("빗방울 수명(초). 바다까지 닿게 하려면 길게. 0이면 프리팹 기본값 유지.")]
    public float rainFallLifetime = 3f;

    [Tooltip("rainPrefab 이 없을 때 쓰는 자체 파티클용 머티리얼.")]
    public Material rainMaterial;
    [Tooltip("빗방울 양. RainMaker 를 쓸 때는 동시에 살아있는 빗방울 개수(maxParticles)로 들어간다. " +
             "RainMaker 는 방출량을 maxParticles / 수명 으로 스스로 계산하기 때문에, 이 값을 올려야 비가 진해진다. " +
             "rainPrefab 이 없을 때는 자체 파티클의 초당 방출 수로 쓰인다.")]
    public float maxRainEmission = 3500f;
    [Tooltip("빗방울 색과 불투명도. 알파를 낮추면 투명해진다. RainPrefab 기본값은 (0.66, 0.66, 0.66, 0.078).")]
    public Color rainDropColor = new Color(0.66f, 0.66f, 0.66f, 0.05f);
    [Tooltip("비가 뿌려지는 영역의 한 변(m).")]
    public float rainAreaSize = 70f;
    [Tooltip("카메라 위 어느 높이에서 떨어뜨릴지(m).")]
    public float rainHeight = 30f;
    [Tooltip("빗방울 낙하 속도(m/s).")]
    public float rainFallSpeed = 38f;
    [Tooltip("카메라 아래로 몇 m 까지 빗방울이 살아 있을지. 짧으면 비가 공중에서 끊겨 보인다.")]
    public float rainDepthBelow = 70f;
    [Tooltip("빗방울 크기.")]
    public float rainDropSize = 0.06f;
    [Tooltip("빗방울 하나가 화면에서 차지할 수 있는 최대 비율. 카메라 바로 앞을 지나는 물방울이 화면을 덮는 것을 막는다.")]
    [Range(0.002f, 0.2f)]
    public float rainMaxScreenSize = 0.02f;
    [Tooltip("빗줄기 길이 배수.")]
    public float rainStreakScale = 0.03f;
    [Range(0f, 1f)]
    [Tooltip("빗방울 불투명도.")]
    public float rainAlpha = 0.45f;
    [Header("비 - 바람 (한쪽 쏠림 방지)")]
    [Tooltip("RainMaker 자체 바람(WindZone)을 끈다. 켜두면 5~30초마다 windMain 이 50~100 으로 무작위 설정되고 방향도 무작위로 바뀌어서, 영역을 키우고 수명을 늘릴수록 비가 통째로 한쪽으로 날아간다.")]
    public bool rainDisableAssetWind = true;
    [Tooltip("빗줄기를 일정하게 기울이는 세기(m/s). 0 이면 수직으로 떨어진다. 자연스러운 정도는 3~8.")]
    public float rainWindTilt = 0f;
    [Tooltip("빗줄기가 기울어지는 방향(도, 월드 Y축 기준). 0 = +Z")]
    public float rainWindDirection = 0f;
    [Tooltip("바람 기울기를 강수량에 비례시킨다. 끄면 항상 rainWindTilt 만큼 기운다.")]
    public bool rainWindScalesWithIntensity = true;


    [Header("상태 (읽기 전용)")]
    public string currentWeather = "-";
    public string nextWeather = "-";
    [Range(0f, 1f)] public float transitionProgress = 1f;
    public float holdRemaining;

    // --- 내부 ---
    private readonly WeatherPreset _blended = new WeatherPreset();
    private WeatherPreset _from;
    private WeatherPreset _to;
    private float _transitionTimer;
    private bool _transitioning;
    private int _currentIndex = -1;

    private VolumeProfile _runtimeProfile;
    private CloudLayer _cloudLayer;
    private VolumetricClouds _volClouds;
    private GradientSky _gradientSky;
    private PhysicallyBasedSky _pbSky;
    private Fog _fog;

    private HDAdditionalLightData _sunHD;

    private RainScript _rainScript;      // RainMaker 사용 시
    private Transform _rainPrefabTr;
    private ParticleSystem _rain;        // 자체 파티클 사용 시
    private ParticleSystem.EmissionModule _rainEmission;
    private Transform _rainTr;    private ParticleSystem _rainFallPS;  // RainMaker 의 RainFallParticleSystem
    private float _appliedWindTilt = float.NaN;
    private float _appliedWindDir = float.NaN;    private float _baseFallSpeedMin;     // RainPrefab 의 velocityOverLifetime.y 원본
    private float _baseFallSpeedMax;
    private float _baseFallLifetime;
    private float _appliedSpeedMul = float.NaN;
    private Color _appliedDropColor = new Color(-1f, -1f, -1f, -1f);



    void Start()
    {
        Resolve();
        BuildRain();

        if (presets.Count == 0)
        {
            Debug.LogWarning("[WeatherSystem] 프리셋이 비어 있습니다.");
            enabled = false;
            return;
        }

        _currentIndex = Mathf.Clamp(startIndex, 0, presets.Count - 1);
        _from = presets[_currentIndex];
        _to = _from;
        _transitioning = false;
        transitionProgress = 1f;
        currentWeather = _to.name;
        nextWeather = _to.name;
        holdRemaining = Random.Range(_to.minHoldSeconds, _to.maxHoldSeconds);

        CopyInto(_to, _blended);
        Apply(_blended);
    }

    void Resolve()
    {
        if (ocean == null) ocean = Object.FindFirstObjectByType<WaterSurface>();

        if (skyVolume == null)
        {
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.isGlobal && v.sharedProfile != null) { skyVolume = v; break; }
        }

        if (skyVolume != null)
        {
            // 런타임 사본을 쓴다. 에셋 원본은 건드리지 않는다.
            _runtimeProfile = skyVolume.profile;
            _runtimeProfile.TryGet(out _cloudLayer);
            _runtimeProfile.TryGet(out _volClouds);
            _runtimeProfile.TryGet(out _gradientSky);
            _runtimeProfile.TryGet(out _pbSky);
            _runtimeProfile.TryGet(out _fog);
        }

        if (sun == null)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun != null) _sunHD = sun.GetComponent<HDAdditionalLightData>();

        if (rainFollowTarget == null)
        {
            // 카메라는 배 주위를 돌기 때문에 비가 선체 안으로 들어갈 수 있다. 배를 우선한다.
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) rainFollowTarget = player.transform;
            else if (Camera.main != null) rainFollowTarget = Camera.main.transform;
        }
    }

    void BuildRain()
    {
        // RainMaker 프리팹이 있으면 그것을 쓴다.
        if (rainPrefab != null)
        {
            GameObject inst = Instantiate(rainPrefab, transform);
            inst.name = "RainMaker";
            _rainPrefabTr = inst.transform;
            _rainScript = inst.GetComponent<RainScript>();
            if (_rainScript != null)
            {
                // 카메라를 따라가면 배 주위를 도는 카메라 때문에 비가 선체 안으로 들어간다.
                // 위치는 우리가 배 기준으로 직접 잡는다.
                _rainScript.FollowCamera = false;
                if (_rainScript.Camera == null) _rainScript.Camera = Camera.main;
                _rainScript.RainIntensity = 0f;
            }
            else Debug.LogWarning("[WeatherSystem] rainPrefab 에 RainScript 가 없습니다.");

            // 배 크기에 맞게 넓히고 빗방울을 키운다.
            // 파티클 ScalingMode 가 Shape 라 루트 스케일은 방출 영역만 키우고 입자 크기는 건드리지 않는다.
            if (rainAreaScale > 0f)
                inst.transform.localScale = new Vector3(rainAreaScale, 1f, rainAreaScale);

            foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject.name != "RainFallParticleSystem") continue;
                _rainFallPS = ps;
                var mainMod = ps.main;
                if (rainFallDropSize > 0f) mainMod.startSize = rainFallDropSize;
                if (rainFallLifetime > 0f) mainMod.startLifetime = rainFallLifetime;

                // RainMaker 는 방출량을 직접 받지 않는다.
                // BaseRainScript.RainFallEmissionRate() = maxParticles / startLifetime * RainIntensity
                // 이라서 빗방울을 늘리려면 maxParticles 를 올려야 한다. (기본 5000 에서 고정되어 있었다)
                if (maxRainEmission > 0f)
                    mainMod.maxParticles = Mathf.Clamp(Mathf.RoundToInt(maxRainEmission), 100, 200000);

                // 속도 배수를 곱할 기준값을 보관해 둔다.
                var volBase = ps.velocityOverLifetime;
                _baseFallSpeedMin = volBase.y.constantMin;
                _baseFallSpeedMax = volBase.y.constantMax;
                if (_baseFallSpeedMin == 0f && _baseFallSpeedMax == 0f)
                    _baseFallSpeedMin = _baseFallSpeedMax = volBase.y.constant;
                _baseFallLifetime = mainMod.startLifetime.constant;
            }

            // RainMaker 는 5~30초마다 WindZone 의 windMain 을 50~100 으로,
            // 방향은 완전 무작위로 다시 뽑는다. 영역(rainAreaScale)과 수명(rainFallLifetime)을
            // 키운 상태에서는 이 힘이 수명 내내 누적되어 비 전체가 한쪽으로 날아간다.
            // 그래서 에셋 바람은 끄고, 필요하면 아래에서 우리가 일정한 기울기만 준다.
            if (rainDisableAssetWind)
            {
                if (_rainScript != null)
                {
                    _rainScript.EnableWind = false;
                    if (_rainScript.WindZone != null)
                        _rainScript.WindZone.gameObject.SetActive(false);
                }
                // 씬에 다른 WindZone 이 있어도 빗방울이 끌려가지 않게 외력 자체를 끊는다.
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var ef = ps.externalForces;
                    ef.enabled = false;
                }
            }

            ApplyRainWind(0f);
            return;
        }

        if (rainMaterial == null) return;

        GameObject go = new GameObject("RainParticles");
        _rainTr = go.transform;
        _rainTr.SetParent(transform, false);

        _rain = go.AddComponent<ParticleSystem>();
        var main = _rain.main;
        main.loop = true;
        main.startLifetime = (rainHeight + rainDepthBelow) / Mathf.Max(1f, rainFallSpeed);
        main.startSpeed = rainFallSpeed;
        main.startSize = rainDropSize;
        main.startColor = new Color(0.88f, 0.93f, 0.98f, rainAlpha);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 8000;
        main.playOnAwake = false;

        var shape = _rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(rainAreaSize, 0.1f, rainAreaSize);
        shape.rotation = new Vector3(90f, 0f, 0f);   // 아래로 뿌린다

        _rainEmission = _rain.emission;
        _rainEmission.rateOverTime = 0f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = rainStreakScale;
        renderer.lengthScale = 2f;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = rainMaxScreenSize;   // 화면을 덮는 근거리 입자 방지
        renderer.material = rainMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        _rain.Play();
    }

    /// <summary>
    /// 빗줄기에 일정한 기울기를 준다. WindZone 처럼 가속도로 미는 것이 아니라
    /// 고정 속도라서 수명이 길어도 한쪽으로 무한히 눈다니지 않는다.
    /// </summary>
    /// <summary>
    /// 빗줄기에 일정한 기울기를 준다. WindZone 처럼 가속도로 미는 것이 아니라
    /// 고정 속도라서 수명이 길어도 한쪽으로 무한히 밀리지 않는다.
    /// </summary>
    void ApplyRainWind(float intensity)
    {
        if (_rainFallPS == null) return;

        float tilt = rainWindTilt * (rainWindScalesWithIntensity ? Mathf.Clamp01(intensity) : 1f);
        float dir = rainWindDirection;

        // 기울기를 쓴 적이 없고 지금도 0 이면 모듈을 아예 건드리지 않는다.
        if (tilt == 0f && float.IsNaN(_appliedWindTilt))
        {
            _appliedWindTilt = 0f;
            _appliedWindDir = dir;
            return;
        }
        if (Mathf.Approximately(tilt, _appliedWindTilt) && Mathf.Approximately(dir, _appliedWindDir)) return;
        _appliedWindTilt = tilt;
        _appliedWindDir = dir;

        var vol = _rainFallPS.velocityOverLifetime;
        if (!vol.enabled) return;

        Vector3 d = Quaternion.Euler(0f, dir, 0f) * Vector3.forward;
        float vx = d.x * tilt;
        float vz = d.z * tilt;

        // x / y / z 커브는 반드시 같은 모드여야 한다.
        // (다르면 "Particle Velocity curves must all be in the same mode" 가 뜨면서 비가 아예 안 나온다)
        // RainPrefab 기본값은 TwoConstants 이므로 y 의 모드에 맞춰서 넣는다.
        switch (vol.y.mode)
        {
            case ParticleSystemCurveMode.TwoConstants:
                vol.x = new ParticleSystem.MinMaxCurve(vx, vx);
                vol.z = new ParticleSystem.MinMaxCurve(vz, vz);
                break;

            case ParticleSystemCurveMode.Constant:
                vol.x = new ParticleSystem.MinMaxCurve(vx);
                vol.z = new ParticleSystem.MinMaxCurve(vz);
                break;

            default:
                // 커브 모드면 동일 모드로 맞추기 어려우므로 건드리지 않는다.
                Debug.LogWarning("[WeatherSystem] velocityOverLifetime.y 가 " + vol.y.mode +
                                 " 모드라 rainWindTilt 를 적용하지 않았습니다.");
                break;
        }
    }

    /// <summary>
    /// 빗방울 낙하 속도를 배수로 조절한다.
    /// 속도만 올리면 같은 수명 동안 훨씬 멀리 떨어져서 화면상 빗줄기가 오히려 엉성해진다.
    /// 그래서 수명을 같은 비율로 줄여 낙하 거리와 밀도를 그대로 유지한다.
    /// (RainMaker 의 방출량 = maxParticles / 수명 이므로 수명을 줄이면 방출량이 자동으로 늘어난다)
    /// </summary>
    void ApplyRainFallSpeed(float multiplier)
    {
        if (_rainFallPS == null) return;

        float mul = Mathf.Max(0.05f, multiplier);
        if (!float.IsNaN(_appliedSpeedMul) && Mathf.Abs(mul - _appliedSpeedMul) < 0.005f) return;
        _appliedSpeedMul = mul;

        var vol = _rainFallPS.velocityOverLifetime;
        if (vol.enabled)
        {
            switch (vol.y.mode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    vol.y = new ParticleSystem.MinMaxCurve(_baseFallSpeedMin * mul, _baseFallSpeedMax * mul);
                    break;
                case ParticleSystemCurveMode.Constant:
                    vol.y = new ParticleSystem.MinMaxCurve(_baseFallSpeedMax * mul);
                    break;
            }
        }

        var mm = _rainFallPS.main;
        mm.startLifetime = _baseFallLifetime / mul;
    }

    /// <summary>빗방울 색과 불투명도를 적용한다. 머티리얼 에셋은 건드리지 않는다.</summary>
    void ApplyRainDropColor()
    {
        if (_rainFallPS == null) return;
        if (_appliedDropColor == rainDropColor) return;
        _appliedDropColor = rainDropColor;

        var mm = _rainFallPS.main;
        // startColor 도 모드가 맞아야 한다. RainPrefab 기본은 TwoColors.
        switch (mm.startColor.mode)
        {
            case ParticleSystemGradientMode.TwoColors:
                mm.startColor = new ParticleSystem.MinMaxGradient(rainDropColor, rainDropColor);
                break;
            case ParticleSystemGradientMode.Color:
                mm.startColor = new ParticleSystem.MinMaxGradient(rainDropColor);
                break;
            default:
                mm.startColor = new ParticleSystem.MinMaxGradient(rainDropColor);
                break;
        }
    }




    void Update()
    {
        if (presets.Count == 0) return;

        // ---- 진행 ----
        if (_transitioning)
        {
            _transitionTimer += Time.deltaTime;
            float t = transitionSeconds <= 0f ? 1f : Mathf.Clamp01(_transitionTimer / transitionSeconds);
            transitionProgress = t;
            float s = Mathf.SmoothStep(0f, 1f, t);
            WeatherPreset.Lerp(_from, _to, s, _blended);

            if (t >= 1f)
            {
                _transitioning = false;
                _from = _to;
                currentWeather = _to.name;
                nextWeather = _to.name;
                holdRemaining = Random.Range(_to.minHoldSeconds, _to.maxHoldSeconds);
            }
        }
        else if (autoChange)
        {
            holdRemaining -= Time.deltaTime;
            if (holdRemaining <= 0f) BeginTransition(PickNextIndex(), transitionSeconds);
        }

        Apply(_blended);
    }

    int PickNextIndex()
    {
        float total = 0f;
        for (int i = 0; i < presets.Count; i++)
        {
            if (avoidRepeat && i == _currentIndex) continue;
            total += Mathf.Max(0f, presets[i].weight);
        }
        if (total <= 0f) return _currentIndex;

        float r = Random.value * total;
        for (int i = 0; i < presets.Count; i++)
        {
            if (avoidRepeat && i == _currentIndex) continue;
            float w = Mathf.Max(0f, presets[i].weight);
            if (r < w) return i;
            r -= w;
        }
        return _currentIndex;
    }

    /// <summary>지정한 날씨로 전환한다.</summary>
    public void SetWeather(int index, float seconds = -1f)
    {
        if (index < 0 || index >= presets.Count) return;
        BeginTransition(index, seconds < 0f ? transitionSeconds : seconds);
    }

    /// <summary>이름으로 날씨를 전환한다.</summary>
    public void SetWeather(string weatherName, float seconds = -1f)
    {
        for (int i = 0; i < presets.Count; i++)
            if (presets[i].name == weatherName) { SetWeather(i, seconds); return; }
        Debug.LogWarning("[WeatherSystem] '" + weatherName + "' 날씨를 찾지 못했습니다.");
    }

    /// <summary>지금 바로 다음 날씨로 넘긴다.</summary>
    public void ForceNext() { BeginTransition(PickNextIndex(), transitionSeconds); }

    void BeginTransition(int index, float seconds)
    {
        if (index < 0 || index >= presets.Count) return;
        CopyInto(_blended, _fromScratch);
        _from = _fromScratch;
        _to = presets[index];
        _currentIndex = index;
        _transitionTimer = 0f;
        transitionSeconds = Mathf.Max(0f, seconds);
        _transitioning = true;
        transitionProgress = 0f;
        nextWeather = _to.name;
    }

    private readonly WeatherPreset _fromScratch = new WeatherPreset();

    static void CopyInto(WeatherPreset src, WeatherPreset dst)
    {
        WeatherPreset.Lerp(src, src, 0f, dst);
        dst.name = src.name;
        dst.minHoldSeconds = src.minHoldSeconds;
        dst.maxHoldSeconds = src.maxHoldSeconds;
    }

    void Apply(WeatherPreset w)
    {
        // ---- 바다 ----
        if (ocean != null)
        {
            ocean.largeWindSpeed = w.windSpeed;
            ocean.largeChaos = Mathf.Clamp01(w.chaos);
            ocean.ripplesWindSpeed = Mathf.Clamp(w.ripplesWindSpeed, 0f, 15f);
            ocean.simulationFoamAmount = Mathf.Clamp01(w.foamAmount);
        }

        // ---- 구름 (CloudLayer) ----
        if (_cloudLayer != null)
        {
            _cloudLayer.opacity.overrideState = true;
            _cloudLayer.opacity.value = Mathf.Clamp01(w.cloudOpacity);

            _cloudLayer.layerA.tint.overrideState = true;
            _cloudLayer.layerA.tint.value = w.cloudTint;

            _cloudLayer.layerA.exposure.overrideState = true;
            _cloudLayer.layerA.exposure.value = w.cloudExposure;

            // scrollSpeed 는 단순 float 이 아니라 모드가 붙은 구조체다.
            // 구름 '양' - 클라우드맵의 채널을 차례로 켜서 겹을 늘린다.
            // opacity 는 전체 페이드일 뿐이라 이것만으로는 구름이 많아지지 않는다.
            float cov = Mathf.Clamp01(w.cloudCoverage) * 3f;
            _cloudLayer.layerA.opacityR.overrideState = true;
            _cloudLayer.layerA.opacityR.value = 1f;
            _cloudLayer.layerA.opacityG.overrideState = true;
            _cloudLayer.layerA.opacityG.value = Mathf.Clamp01(cov);
            _cloudLayer.layerA.opacityB.overrideState = true;
            _cloudLayer.layerA.opacityB.value = Mathf.Clamp01(cov - 1f);
            _cloudLayer.layerA.opacityA.overrideState = true;
            _cloudLayer.layerA.opacityA.value = Mathf.Clamp01(cov - 2f);

            _cloudLayer.layerA.thickness.overrideState = true;
            _cloudLayer.layerA.thickness.value = Mathf.Clamp01(w.cloudThickness);

            _cloudLayer.layerA.scrollSpeed.overrideState = true;
            var sv = _cloudLayer.layerA.scrollSpeed.value;
            sv.mode = WindParameter.WindOverrideMode.Custom;
            sv.customValue = w.cloudScrollSpeed;
            _cloudLayer.layerA.scrollSpeed.value = sv;
        }

        // ---- 구름 (Volumetric Clouds) ----
        if (_volClouds != null)
        {
            _volClouds.densityMultiplier.overrideState = true;
            _volClouds.densityMultiplier.value = Mathf.Clamp01(w.cloudOpacity);

            _volClouds.scatteringTint.overrideState = true;
            _volClouds.scatteringTint.value = w.cloudTint;
        }

        // ---- 하늘 밝기 ----
        if (_gradientSky != null)
        {
            _gradientSky.exposure.overrideState = true;
            _gradientSky.exposure.value = w.skyExposure;
        }
        if (_pbSky != null)
        {
            _pbSky.exposure.overrideState = true;
            _pbSky.exposure.value = w.skyExposure;
        }

        // ---- 안개 ----
        if (_fog != null)
        {
            _fog.enabled.overrideState = true;
            _fog.enabled.value = true;
            _fog.meanFreePath.overrideState = true;
            _fog.meanFreePath.value = Mathf.Max(1f, w.fogMeanFreePath);
            _fog.tint.overrideState = true;
            _fog.tint.value = w.fogTint;
        }

        // ---- 태양 ----
        if (sun != null)
        {
            sun.color = w.sunColor;
            if (_sunHD != null) _sunHD.SetIntensity(w.sunLux, LightUnit.Lux);
            else sun.intensity = w.sunLux;
        }

        // ---- 비 (RainMaker) ----
        if (_rainScript != null)
        {
            _rainScript.RainIntensity = Mathf.Clamp01(w.rainIntensity);            ApplyRainWind(w.rainIntensity);            ApplyRainFallSpeed(w.rainFallSpeedMultiplier);
            ApplyRainDropColor();


            if (rainFollowTarget != null && _rainPrefabTr != null)
            {
                _rainPrefabTr.position = rainFollowTarget.position + Vector3.up * rainHeight;
                if (rainIgnoreTargetRotation) _rainPrefabTr.rotation = Quaternion.identity;
            }
        }

        // ---- 비 (자체 파티클) ----
        if (_rain != null)
        {
            var em = _rain.emission;
            em.rateOverTime = maxRainEmission * Mathf.Clamp01(w.rainIntensity);
            if (rainFollowTarget != null)
            {
                // 위치만 따라가고 회전은 항상 월드 기준으로 고정한다.
                // 배의 자식으로 두거나 회전을 따라가면 배가 흔들릴 때 비가 통째로 돈다.
                _rainTr.position = rainFollowTarget.position + Vector3.up * rainHeight;
                if (rainIgnoreTargetRotation) _rainTr.rotation = Quaternion.identity;
            }
        }
    }
}
