using UnityEngine;

/// <summary>
/// 포탄 소리. 포탄 템플릿(Sphere)에 붙는다 — 플레이어·적·관군 포탄이 모두 같은 템플릿을 복제하므로 한 곳에서 처리.
///   - 발사음: 활성화되는 순간(= 포구 위치) 3D 원샷. maxDistance 가 커서 먼 적함의 포성이 거리에 따라 작게 들린다.
///   - 비행음(휘이익): 포탄 자체에 붙은 루프 소스. 3D 감쇠라 포탄이 다가올수록 커진다. 발사 직후 잠깐은 포성에 묻히게 지연.
///   - 명중음: Bullet.OnCollisionEnter 가 OnImpact() 를 호출한다.
/// 음원: Assets/02.Resources/01.Asset/Audio/Cannon (LICENSE.txt).
/// </summary>
public class BulletAudio : MonoBehaviour
{
    [Header("발사")]
    public AudioClip fireClip;
    [Range(0f, 1f)] public float fireVolume = 0.5f;
    public float fireMinDistance = 25f;
    [Tooltip("이 거리(m)에서 소리가 0 이 된다. 먼 적의 포성도 들리게 크게.")]
    public float fireMaxDistance = 900f;
    public Vector2 firePitchRange = new Vector2(0.92f, 1.08f);

    [Header("비행음")]
    public AudioClip whistleClip;
    [Range(0f, 1f)] public float whistleVolume = 0.5f;
    public float whistleMinDistance = 5f;
    public float whistleMaxDistance = 140f;
    [Tooltip("발사 후 이 시간(초) 뒤부터 휘이익 소리가 커지기 시작한다.")]
    public float whistleDelay = 0.35f;
    public float whistleFadeIn = 0.5f;

    [Header("명중")]
    public AudioClip hitClip;
    [Range(0f, 1f)] public float hitVolume = 0.5f;
    public float hitMinDistance = 15f;
    public float hitMaxDistance = 600f;

    AudioSource _whistle;
    Rigidbody _rb;
    float _spawnTime;

    void OnEnable()
    {
        _spawnTime = Time.time;
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        OneShotAudio.Play(fireClip, transform.position, fireVolume, fireMinDistance, fireMaxDistance, Random.Range(firePitchRange.x, firePitchRange.y));

        if (whistleClip != null)
        {
            if (_whistle == null)
            {
                _whistle = gameObject.AddComponent<AudioSource>();
                _whistle.clip = whistleClip;
                _whistle.loop = true;
                _whistle.spatialBlend = 1f;
                _whistle.rolloffMode = AudioRolloffMode.Linear;
                _whistle.minDistance = whistleMinDistance;
                _whistle.maxDistance = whistleMaxDistance;
                _whistle.dopplerLevel = 0.6f; // 스쳐 지나갈 때 음이 꺾이는 느낌
                _whistle.playOnAwake = false;
            }
            _whistle.volume = 0f;
            _whistle.pitch = Random.Range(0.9f, 1.1f);
            _whistle.time = Random.Range(0f, whistleClip.length * 0.8f);
            _whistle.Play();
        }
    }

    void Update()
    {
        if (_whistle == null) return;
        float t = Mathf.Clamp01((Time.time - _spawnTime - whistleDelay) / Mathf.Max(0.01f, whistleFadeIn));
        // 땅에 떨어져 구르거나 멈춘 포탄(터지지 않은 것)은 휘이익 소리를 내지 않는다: 속도 8 m/s 아래에서 줄어 3 m/s 에서 0.
        float speedT = _rb != null ? Mathf.InverseLerp(3f, 8f, _rb.linearVelocity.magnitude) : 1f;
        _whistle.volume = whistleVolume * t * speedT;
    }

    /// <summary>Bullet 이 터질 때 부른다. 포탄은 곧 파괴되므로 명중음은 별도 오브젝트로 낸다.</summary>
    public void OnImpact()
    {
        if (_whistle != null) _whistle.Stop();
        OneShotAudio.Play(hitClip, transform.position, hitVolume, hitMinDistance, hitMaxDistance, Random.Range(0.95f, 1.05f));
    }

    void OnDisable()
    {
        if (_whistle != null) _whistle.Stop();
    }
}
