using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using DigitalRuby.LightningBolt;

/// <summary>
/// 폭풍우에서 번개를 친다.
///
/// 세 가지가 따로 동작하므로, 외부 번개 에셋이 없어도 섬광과 천둥은 바로 쓸 수 있다.
///   1) 섬광  - 전용 라이트를 순간적으로 밝혔다 끈다 (여러 번 깜빡임)
///   2) 번개 줄기 - Bolt Prefab 을 연결하면 하늘에 실제 번개를 만든다 (에셋 임포트 후 연결)
///   3) 천둥  - 거리에 비례해 늦게 들린다
///
/// 바람 세기가 기준 이상일 때만 발생하므로 WeatherSystem 의 날씨를 그대로 따라간다.
/// </summary>
[DefaultExecutionOrder(145)]
public class LightningStorm : MonoBehaviour
{
    [Header("참조 (비우면 자동 탐색)")]
    public WaterSurface ocean;
    [Tooltip("번개가 칠 기준 위치. 비우면 Player 태그 오브젝트, 없으면 메인 카메라.")]
    public Transform followTarget;

    [Header("발생 조건")]
    [Tooltip("바다 바람이 이 값 이상일 때만 번개가 친다. 폭풍우 프리셋의 windSpeed 를 참고해 정한다.")]
    public float minWindSpeed = 90f;
    [Tooltip("번개 사이 간격(초) 범위. 바람이 셀수록 짧아진다.")]
    public Vector2 intervalRange = new Vector2(3.5f, 14f);

    [Header("섬광")]
    public bool enableFlash = true;
    [Tooltip("비우면 자동으로 만든다.")]
    public Light flashLight;
    [Tooltip("섬광 밝기(Lux).")]
    public float flashIntensityLux = 130000f;
    [Tooltip("한 번의 깜빡임 길이(초).")]
    public float flashDuration = 0.14f;
    [Tooltip("연속으로 깜빡이는 최대 횟수.")]
    public int maxSubFlashes = 3;

    [Header("번개 줄기 (에셋 임포트 후 연결)")]
    [Tooltip("Digital Ruby 의 Lightning Bolt 프리팹 등을 넣으면 실제 번개가 생긴다.")]
    public GameObject boltPrefab;
    public float boltDistanceMin = 180f;
    public float boltDistanceMax = 650f;
    [Tooltip("번개가 시작되는 구름 높이(m). 여기서 바다로 떨어진다.")]
    public float boltHeight = 320f;
    [Tooltip("번개 굵기/복잡도. LightningBoltScript 의 Generations.")]
    [Range(1, 8)]
    public int boltGenerations = 6;
    [Tooltip("번개가 흔들리는 정도. LightningBoltScript 의 ChaosFactor.")]
    [Range(0f, 1f)]
    public float boltChaos = 0.25f;
    public float boltLifetime = 1.5f;

    [Header("천둥")]
    public AudioSource thunderSource;
    public AudioClip[] thunderClips;
    [Tooltip("소리 속도(m/s). 거리에 따라 천둥이 늦게 들린다.")]
    public float soundSpeed = 340f;
    [Range(0f, 1f)] public float thunderVolume = 0.9f;

    [Header("상태 (읽기 전용)")]
    public float nextStrikeIn;
    public int strikeCount;

    private float _timer;
    private float _flashTimer;
    private int _subFlashLeft;
    private float _baseFlashIntensity;
    private HDAdditionalLightData _flashHD;

    void Start()
    {
        if (ocean == null) ocean = Object.FindFirstObjectByType<WaterSurface>();
        if (followTarget == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) followTarget = player.transform;
            else if (Camera.main != null) followTarget = Camera.main.transform;
        }

        if (enableFlash && flashLight == null)
        {
            var go = new GameObject("LightningFlashLight");
            go.transform.SetParent(transform, false);
            flashLight = go.AddComponent<Light>();
            flashLight.type = LightType.Directional;
            flashLight.color = new Color(0.85f, 0.92f, 1f);
            flashLight.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(35f, 200f, 0f);
        }
        if (flashLight != null)
        {
            _flashHD = flashLight.GetComponent<HDAdditionalLightData>();
            if (_flashHD == null) _flashHD = flashLight.gameObject.AddComponent<HDAdditionalLightData>();
            SetFlash(0f);
        }

        ScheduleNext();
    }

    void ScheduleNext()
    {
        float wind = ocean != null ? ocean.largeWindSpeed : 0f;
        // 바람이 셀수록 자주 친다
        float k = Mathf.Clamp01((wind - minWindSpeed) / Mathf.Max(1f, minWindSpeed));
        float interval = Mathf.Lerp(intervalRange.y, intervalRange.x, k);
        _timer = interval * Random.Range(0.7f, 1.3f);
        nextStrikeIn = _timer;
    }

    void Update()
    {
        // ---- 섬광 감쇠 ----
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            float n = Mathf.Clamp01(_flashTimer / Mathf.Max(0.01f, flashDuration));
            SetFlash(_baseFlashIntensity * n * n);
            if (_flashTimer <= 0f)
            {
                if (_subFlashLeft > 0)
                {
                    _subFlashLeft--;
                    _baseFlashIntensity = flashIntensityLux * Random.Range(0.35f, 0.8f);
                    _flashTimer = flashDuration * Random.Range(0.5f, 1f);
                }
                else SetFlash(0f);
            }
        }

        float wind = ocean != null ? ocean.largeWindSpeed : 0f;
        if (wind < minWindSpeed) { nextStrikeIn = -1f; return; }

        _timer -= Time.deltaTime;
        nextStrikeIn = _timer;
        if (_timer <= 0f) Strike();
    }

    void SetFlash(float lux)
    {
        if (flashLight == null) return;
        if (_flashHD != null) _flashHD.SetIntensity(lux, UnityEngine.Rendering.LightUnit.Lux);
        else flashLight.intensity = lux;
        flashLight.enabled = lux > 1f;
    }

    /// <summary>번개를 한 번 친다. 외부에서 직접 호출해도 된다.</summary>
    public void Strike()
    {
        strikeCount++;
        ScheduleNext();

        Vector3 origin = followTarget != null ? followTarget.position : transform.position;
        float dist = Random.Range(boltDistanceMin, boltDistanceMax);
        float ang = Random.value * Mathf.PI * 2f;
        Vector3 spot = origin + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);

        // 1) 섬광
        if (enableFlash && flashLight != null)
        {
            // 가까울수록 밝게
            float near = 1f - Mathf.Clamp01((dist - boltDistanceMin) / Mathf.Max(1f, boltDistanceMax - boltDistanceMin));
            _baseFlashIntensity = flashIntensityLux * Mathf.Lerp(0.45f, 1f, near);
            _flashTimer = flashDuration;
            _subFlashLeft = Random.Range(0, maxSubFlashes);
            // 섬광 방향을 번개 쪽으로
            Vector3 dir = (origin - (spot + Vector3.up * boltHeight)).normalized;
            flashLight.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // 2) 번개 줄기 - 구름에서 바다 표면으로 떨어진다
        if (boltPrefab != null)
        {
            // 떨어질 지점의 실제 수면 높이를 구한다.
            float seaY = spot.y;
            if (ocean != null) seaY = WaterHeightUtil.SampleHeight(ocean, spot);

            Vector3 cloudPoint = new Vector3(spot.x, seaY + boltHeight, spot.z);
            Vector3 seaPoint = new Vector3(spot.x, seaY, spot.z);

            var go = Instantiate(boltPrefab, cloudPoint, Quaternion.identity);
            go.SetActive(true);

            var bolt = go.GetComponent<LightningBoltScript>();
            if (bolt != null)
            {
                // Object 참조를 비우고 좌표로 직접 지정한다.
                bolt.StartObject = null;
                bolt.EndObject = null;
                bolt.StartPosition = cloudPoint;
                bolt.EndPosition = seaPoint;
                bolt.Generations = boltGenerations;
                bolt.ChaosFactor = boltChaos;
                bolt.Duration = boltLifetime;
                bolt.ManualMode = true;
                StartCoroutine(TriggerBoltNextFrame(bolt));
            }
            Destroy(go, boltLifetime + 0.2f);
        }

        // 3) 천둥 (거리만큼 늦게)
        if (thunderSource != null && thunderClips != null && thunderClips.Length > 0)
        {
            var clip = thunderClips[Random.Range(0, thunderClips.Length)];
            if (clip != null) StartCoroutine(PlayThunder(clip, dist / Mathf.Max(1f, soundSpeed), dist));
        }
    }

    // Instantiate 직후에는 LightningBoltScript 의 Start() 가 아직 실행되지 않아
    // 내부 LineRenderer 가 준비되지 않았다. 한 프레임 기다렸다 친다.
    System.Collections.IEnumerator TriggerBoltNextFrame(LightningBoltScript bolt)
    {
        yield return null;
        if (bolt != null) bolt.Trigger();
    }

    System.Collections.IEnumerator PlayThunder(AudioClip clip, float delay, float dist)
    {
        yield return new WaitForSeconds(delay);
        float vol = thunderVolume * Mathf.Lerp(1f, 0.35f,
            Mathf.Clamp01((dist - boltDistanceMin) / Mathf.Max(1f, boltDistanceMax - boltDistanceMin)));
        thunderSource.PlayOneShot(clip, vol);
    }
}
