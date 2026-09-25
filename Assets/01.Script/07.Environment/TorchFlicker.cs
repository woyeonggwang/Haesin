using UnityEngine;

/// <summary>
/// 횃불 조명 깜빡임. Point Light 의 세기와 위치를 Perlin 노이즈로 흔든다.
/// HDRP(Unity 6) 에서는 Light.intensity 가 곧 물리 단위(Lumen 등) 값이므로 그대로 곱한다.
/// 베이스캠프(BaseGroup/Light) 의 횃불용. 에디터에서는 동작하지 않고 플레이 중에만 흔들린다.
/// </summary>
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Tooltip("세기 흔들림 폭 (0.15 = ±15%)")]
    [Range(0f, 0.6f)] public float intensityJitter = 0.18f;
    [Tooltip("위치 흔들림 폭 (m)")]
    public float positionJitter = 0.06f;
    [Tooltip("흔들림 속도")]
    public float speed = 9f;

    Light _light;
    float _baseIntensity;
    Vector3 _basePos;
    float _seed;

    void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _basePos = transform.localPosition;
        _seed = Random.value * 100f; // 횃불마다 위상을 다르게
    }

    void OnDisable()
    {
        if (_light != null) _light.intensity = _baseIntensity;
        transform.localPosition = _basePos;
    }

    void Update()
    {
        float t = Time.time * speed + _seed;
        // 두 주파수를 섞어 규칙적으로 보이지 않게
        float n = Mathf.PerlinNoise(t, _seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, _seed + 7f) * 0.3f; // 0~1
        _light.intensity = _baseIntensity * (1f + (n - 0.5f) * 2f * intensityJitter);
        float nx = Mathf.PerlinNoise(t * 0.8f, _seed + 20f) - 0.5f;
        float nz = Mathf.PerlinNoise(t * 0.8f, _seed + 40f) - 0.5f;
        transform.localPosition = _basePos + new Vector3(nx, 0f, nz) * (positionJitter * 2f);
    }
}
