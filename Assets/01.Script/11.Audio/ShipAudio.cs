using UnityEngine;

/// <summary>
/// 플레이어 배 소리. 두 개의 루프 AudioSource 를 자식으로 만들어 속도에 따라 볼륨을 조절한다.
///   - 선체가 물을 가르는 소리(hull): 속도가 minSpeed 이상일 때만, 속도에 비례해 커진다.
///   - 노 젓는 소리(oar, 2026-10-06 바꿈): 예전 물소리 루프(rowing_quickly) 대신 OarAnimator 위상에 맞춰 젓기 시작할 때마다
///     노 소리 3 개(Audio/Oar/oar_stroke_* — Kenney RPG Audio 나무 삐걱임 + rowing_quickly 녹음에서 잘라 낸 물 젓는 소리, CC0)를
///     좌·우·가운데에서 어긋나게 겹쳐 틂(볼륨 oarVolume 0.5). 정박(젓기 0)·침몰 중엔 안 남.
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

    [Header("노 젓는 소리 — 노를 한 번 저을 때마다 (2026-10-06, 사용자 지시 \"0.5, 나무가 삐걱거리는 느낌\")")]
    [Tooltip("물을 젓기 시작할 때(OarAnimator 위상이 0 을 지날 때) 노 소리 — 여러 개 중 oarVoices 개를 골라(서로 다르게) 조금씩 어긋나게 겹쳐 틂.")]
    public AudioClip[] oarDriveClips = new AudioClip[0];
    [Tooltip("노를 빼 되돌릴 때(위상이 driveFraction 을 지날 때) — 짧은 삐걱임 + 물방울.")]
    public AudioClip[] oarRecoverClips = new AudioClip[0];
    [Range(0f, 1f)] public float oarVolume = 0.5f;
    [Tooltip("되돌릴 때 소리 배율.")] [Range(0f, 1f)] public float oarRecoverScale = 0.7f;
    [Tooltip("한 번 저을 때 겹쳐 들리는 노 소리 수(좌현·우현·가운데 자리, 2026-10-06 사용자 지시 \"랜덤으로 3 개는 들리게\").")] [Range(1, 3)] public int oarVoices = 3;
    [Tooltip("겹치는 노 소리 사이 무작위 늦춤 최대(초).")] public float oarVoiceSpread = 0.35f;
    [Tooltip("노 소리 피치(재생마다 ±6 % 흔듦). 2026-10-06 사용자 지시 0.5.")] public float oarPitch = 0.5f;
    [Tooltip("가장 느리게 저을 때(제자리) 볼륨 배율 — 빠르게 저을수록 1 까지.")] [Range(0f, 1f)] public float oarIdleVolumeScale = 0.6f;
    [Tooltip("노 소리가 줄지 않고 들리는 거리(m).")] public float oarMinDistance = 25f;
    [Tooltip("좌·우현 소리 자리(배 원점에서 옆으로, m). 두 현이 조금 어긋나게 울려 여러 노 느낌.")] public float oarSideOffset = 5f;

    [Header("공통")]
    [Tooltip("볼륨이 목표값을 따라가는 속도(초).")]
    public float fadeTime = 0.6f;
    [Tooltip("3D 감쇠 최대 거리(m).")]
    public float maxDistance = 120f;

    Rigidbody _rb;
    OarAnimator _oars;
    ShipHealth _health;
    AudioSource _hull, _oarL, _oarR, _oarC;
    float _hullVel;
    float _lastPhase = -1f; int _lastDrive = -1, _lastRecover = -1;
    readonly System.Collections.Generic.List<PendingOar> _pending = new System.Collections.Generic.List<PendingOar>();
    struct PendingOar { public AudioSource src; public AudioClip clip; public float vol; public float at; }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _oars = GetComponent<OarAnimator>();
        _health = GetComponent<ShipHealth>();
        _hull = MakeSource("HullWaterAudio", hullClip);
        _oarL = MakeSource("OarAudio_L", null); _oarL.minDistance = oarMinDistance; _oarL.loop = false; _oarL.volume = 1f; // PlayOneShot 볼륨이 곱해짐
        _oarR = MakeSource("OarAudio_R", null); _oarR.minDistance = oarMinDistance; _oarR.loop = false; _oarR.volume = 1f;
        _oarC = MakeSource("OarAudio_C", null); _oarC.minDistance = oarMinDistance; _oarC.loop = false; _oarC.volume = 1f;
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

    /// <summary>노 소리 oarVoices 개(서로 다른 음원)를 좌현·우현·가운데 자리에서 0~oarVoiceSpread 초 어긋나게(첫 소리는 바로).</summary>
    void Stroke(AudioClip[] clips, ref int last, float vol)
    {
        if (clips == null || clips.Length == 0 || vol <= 0f) return;
        var srcs = new[] { _oarL, _oarR, _oarC };
        int start = Random.Range(0, 3);
        int first = Pick(clips.Length, ref last);
        for (int k = 0; k < Mathf.Clamp(oarVoices, 1, 3); k++)
        {
            int ci = (first + k) % clips.Length; // 서로 다른 음원(음원이 voices 보다 적으면 겹침)
            float delay = k == 0 ? 0f : Random.Range(0.05f, oarVoiceSpread);
            _pending.Add(new PendingOar { src = srcs[(start + k) % 3], clip = clips[ci], vol = vol * (k == 0 ? 1f : Random.Range(0.75f, 0.95f)), at = Time.time + delay });
        }
    }

    static int Pick(int n, ref int last)
    {
        int i = Random.Range(0, n);
        if (n > 1 && i == last) i = (i + 1 + Random.Range(0, n - 1)) % n;
        last = i; return i;
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

        // 노: OarAnimator 위상에 맞춰 젓는 순간마다 한 번씩
        Vector3 side = transform.right; side.y = 0f; side = side.sqrMagnitude > 1e-4f ? side.normalized * oarSideOffset : Vector3.zero;
        _oarL.transform.position = transform.position - side;
        _oarR.transform.position = transform.position + side;
        _oarC.transform.position = transform.position;
        if (sinking || _oars == null || !_oars.enabled || _oars.currentStrokeRate < 0.02f) { _lastPhase = -1f; }
        else
        {
            float ph = _oars.currentPhase, d = Mathf.Clamp(_oars.driveFraction, 0.05f, 0.95f);
            if (_lastPhase >= 0f)
            {
                float diff = ph - _lastPhase;
                bool wrapped = Mathf.Abs(diff) > 0.5f;
                bool crossDrive = wrapped;                                              // 0(앞) 을 지남 = 젓기 시작
                bool crossRecover = !wrapped && ((_lastPhase < d) != (ph < d)); // driveFraction 을 지남 = 되돌리기
                float rateT = Mathf.InverseLerp(_oars.idleStrokeRate, _oars.maxStrokeRate, _oars.currentStrokeRate);
                float vol = oarVolume * Mathf.Lerp(oarIdleVolumeScale, 1f, rateT);
                if (crossDrive) Stroke(oarDriveClips, ref _lastDrive, vol);
                if (crossRecover) Stroke(oarRecoverClips, ref _lastRecover, vol * oarRecoverScale);
            }
            _lastPhase = ph;
        }
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (Time.time < _pending[i].at) continue;
            var k = _pending[i]; _pending.RemoveAt(i);
            if (k.src != null && k.clip != null) { k.src.pitch = oarPitch * Random.Range(0.94f, 1.06f); k.src.PlayOneShot(k.clip, k.vol); }
        }
    }
}
