using UnityEngine;

/// <summary>
/// 바다 환경음. 2D 루프로 항상 재생하되, 카메라가 동굴(베이스) 안에 들어가면 볼륨을 줄인다
/// (WeatherSystem.rainCovered01 — 머리 위가 가려진 비율 — 을 그대로 빌려 쓴다).
/// 음원: Assets/02.Resources/01.Asset/Audio/Sea/ocean_waves_s0698_cc0.ogg (BigSoundBank CC0).
/// </summary>
public class SeaAmbientAudio : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.2f;
    [Tooltip("동굴 안(가려짐 1.0)일 때 볼륨 배율.")]
    [Range(0f, 1f)] public float coveredMultiplier = 0.25f; // 0.2 × 0.25 = 0.05 (동굴 안)
    public float fadeTime = 1.5f;

    AudioSource _src;
    WeatherSystem _weather;
    float _vel;

    void Awake()
    {
        _src = gameObject.GetComponent<AudioSource>();
        if (_src == null) _src = gameObject.AddComponent<AudioSource>();
        _src.clip = clip;
        _src.loop = true;
        _src.spatialBlend = 0f;
        _src.playOnAwake = false;
        _src.volume = 0f; // 0 에서 페이드인 — 처음에 크게 들렸다가 줄어드는 느낌 방지 (가림 판정이 잡히기 전 한순간 최대 볼륨이었음)
        if (clip != null) _src.Play();
        _weather = UnityEngine.Object.FindFirstObjectByType<WeatherSystem>();
    }

    void Update()
    {
        float covered = _weather != null ? _weather.rainCovered01 : 0f;
        float target = volume * Mathf.Lerp(0.35f, coveredMultiplier, covered);
        _src.volume = Mathf.SmoothDamp(_src.volume, target, ref _vel, fadeTime);
    }
}
