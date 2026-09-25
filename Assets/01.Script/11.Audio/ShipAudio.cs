using UnityEngine;

/// <summary>
/// 플레이어 배 소리. 두 개의 루프 AudioSource 를 자식으로 만들어 속도에 따라 볼륨을 조절한다.
///   - 선체가 물을 가르는 소리(hull): 속도가 minSpeed 이상일 때만, 속도에 비례해 커진다.
///   - 노 젓는 소리(oar): OarAnimator 의 currentStrokeRate 가 idle 보다 높을 때(실제로 젓고 있을 때) 들린다.
/// 둘 다 서 있으면 볼륨 0 으로 부드럽게 꺼진다(재생은 계속 — 다시 움직일 때 끊김 없이).
/// 음원: Assets/02.Resources/01.Asset/Audio/Sea (BigSoundBank CC0, LICENSE_CC0.txt 참고).
/// 침몰 중(ShipHealth.isSinking)에는 둘 다 꺼진다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShipAudio : MonoBehaviour
{
    [Header("선체 물 가르는 소리")]
    public AudioClip hullClip;
    [Tooltip("이 속도(m/s) 이상부터 들린다.")]
    public float hullMinSpeed = 0.8f;
    [Tooltip("이 속도(m/s)에서 최대 볼륨.")]
    public float hullFullSpeed = 12f;
    [Range(0f, 1f)] public float hullMaxVolume = 0.55f;
    [Tooltip("속도에 따른 피치 범위 (느릴 때 → 빠를 때).")]
    public Vector2 hullPitchRange = new Vector2(0.75f, 1.05f);

    [Header("노 젓는 소리")]
    public AudioClip oarClip;
    [Range(0f, 1f)] public float oarMaxVolume = 0.35f;
    [Tooltip("OarAnimator 의 idleStrokeRate 보다 이만큼 높아야 젓는 것으로 본다.")]
    public float oarRateMargin = 0.05f;

    [Header("공통")]
    [Tooltip("볼륨이 목표값을 따라가는 속도(초).")]
    public float fadeTime = 0.6f;
    [Tooltip("3D 감쇠 최대 거리(m).")]
    public float maxDistance = 120f;

    Rigidbody _rb;
    OarAnimator _oars;
    ShipHealth _health;
    AudioSource _hull, _oar;
    float _hullVel, _oarVel;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _oars = GetComponent<OarAnimator>();
        _health = GetComponent<ShipHealth>();
        _hull = MakeSource("HullWaterAudio", hullClip);
        _oar = MakeSource("OarAudio", oarClip);
    }

    AudioSource MakeSource(string name, AudioClip clip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        // 배 루트 스케일(66.79) 영향을 받지 않게 로컬 스케일을 보정할 필요는 없다 — AudioSource 는 위치만 쓴다.
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 8f;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f;
        src.volume = 0f;
        if (clip != null) { src.time = Random.Range(0f, clip.length * 0.9f); src.Play(); }
        return src;
    }

    void Update()
    {
        bool sinking = _health != null && (_health.isSinking || _health.isDead);
        Vector3 v = _rb.linearVelocity; v.y = 0f;
        float speed = v.magnitude;

        // 선체
        float hullT = sinking ? 0f : Mathf.InverseLerp(hullMinSpeed, hullFullSpeed, speed);
        float hullTarget = hullMaxVolume * Mathf.SmoothStep(0f, 1f, hullT);
        _hull.volume = Mathf.SmoothDamp(_hull.volume, hullTarget, ref _hullVel, fadeTime);
        _hull.pitch = Mathf.Lerp(hullPitchRange.x, hullPitchRange.y, hullT);

        // 노
        float oarTarget = 0f;
        if (!sinking && _oars != null && _oars.enabled)
        {
            float rate = _oars.currentStrokeRate;
            float t = Mathf.InverseLerp(_oars.idleStrokeRate + oarRateMargin, _oars.maxStrokeRate, rate);
            oarTarget = oarMaxVolume * t;
        }
        _oar.volume = Mathf.SmoothDamp(_oar.volume, oarTarget, ref _oarVel, fadeTime);
    }
}
