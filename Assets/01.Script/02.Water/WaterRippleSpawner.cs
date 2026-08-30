using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 파문/물보라를 한 곳에서 관리하는 풀. 씬에 하나만 두면 된다.
/// 어디서든 WaterRippleSpawner.Instance.SpawnFromImpact(...) 으로 충돌 파문을 만든다.
///
/// 풀을 충돌용과 항적용으로 나눠 둔다. 항적은 계속 나오기 때문에
/// 하나의 풀을 같이 쓰면 정작 중요한 충돌 파문이 자리를 못 잡는 일이 생긴다.
/// </summary>
public class WaterRippleSpawner : MonoBehaviour
{
    public static WaterRippleSpawner Instance { get; private set; }

    [Header("프리팹")]
    [Tooltip("WaterDeformer + WaterRipple 이 붙은 원형 파문 프리팹.")]
    public GameObject ripplePrefab;
    [Tooltip("물보라 VFX 프리팹. 없으면 생략된다.")]
    public GameObject splashPrefab;

    [Header("풀 크기")]
    [Tooltip("충돌 전용 파문 수. 항적에 밀리지 않도록 따로 확보한다.")]
    public int impactPoolSize = 8;
    [Tooltip("항적 전용 파문 수.")]
    public int wakePoolSize = 10;
    public int splashPoolSize = 8;

    [Header("충돌 -> 파문 변환")]
    [Tooltip("이 속도(m/s) 미만의 충돌은 무시한다.")]
    public float minImpactSpeed = 1.5f;
    [Tooltip("이 속도에서 최대 세기가 된다.")]
    public float maxImpactSpeed = 14f;
    [Tooltip("최소 충돌에서의 솟아오름(m).")]
    public float minAmplitude = 0.2f;
    [Tooltip("최대 충돌에서의 솟아오름(m).")]
    public float maxAmplitude = 1.1f;
    [Tooltip("최소 충돌에서 퍼지는 반지름(m).")]
    public float minRadius = 10f;
    [Tooltip("최대 충돌에서 퍼지는 반지름(m).")]
    public float maxRadius = 38f;
    [Range(0f, 1f)]
    [Tooltip("충돌 파문의 거품 세기.")]
    public float impactFoam = 0.5f;
    [Range(0f, 1f)]
    [Tooltip("항적 파문의 거품 세기.")]
    public float wakeFoam = 0.28f;

    [Header("파문 모양")]
    [Tooltip("충돌 파문의 세로:가로 비율. 1이면 원형, 2면 진행 방향으로 두 배 길쭉.")]
    public float impactAspect = 1.3f;
    [Tooltip("항적 파문의 세로:가로 비율. 배처럼 길쭉하게 하려면 1.5~2.5.")]
    public float wakeAspect = 2f;

    [Header("영역 컬링")]
    [Tooltip("수면의 Water Decal 영역 밖에서는 파문을 만들지 않는다. 어차피 화면에 나오지 않는다.")]
    public bool cullOutsideDecalRegion = true;
    public WaterSurface targetSurface;

    private readonly List<WaterRipple> _impact = new List<WaterRipple>();
    private readonly List<WaterRipple> _wake = new List<WaterRipple>();
    private readonly List<GameObject> _splashes = new List<GameObject>();
    private int _impactCursor, _wakeCursor, _splashCursor;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (targetSurface == null) targetSurface = WaterHeightUtil.FindSurface();
        BuildPools();
    }

    void BuildPools()
    {
        if (ripplePrefab != null)
        {
            FillRipplePool(_impact, impactPoolSize, "ImpactRipple_");
            FillRipplePool(_wake, wakePoolSize, "WakeRipple_");
        }

        if (splashPrefab != null)
        {
            for (int i = 0; i < splashPoolSize; i++)
            {
                GameObject go = Instantiate(splashPrefab, transform);
                go.name = "Splash_" + i;
                go.SetActive(false);
                _splashes.Add(go);
            }
        }
    }

    void FillRipplePool(List<WaterRipple> list, int count, string prefix)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(ripplePrefab, transform);
            go.name = prefix + i;
            WaterRipple r = go.GetComponent<WaterRipple>();
            if (r == null) r = go.AddComponent<WaterRipple>();
            go.SetActive(false);
            list.Add(r);
        }
    }

    /// <summary>수면의 데칼 영역 안인지 확인한다. 영역 밖의 파문은 화면에 나오지 않는다.</summary>
    public bool IsInsideDecalRegion(Vector3 worldPos)
    {
        if (!cullOutsideDecalRegion) return true;
        if (targetSurface == null) return true;

        Vector2 size = targetSurface.decalRegionSize;
        Vector3 c = targetSurface.decalRegionAnchor != null
            ? targetSurface.decalRegionAnchor.position
            : targetSurface.transform.position;

        return Mathf.Abs(worldPos.x - c.x) <= size.x * 0.5f
            && Mathf.Abs(worldPos.z - c.z) <= size.y * 0.5f;
    }

    /// <summary>충돌 속도(m/s)를 받아 세기에 맞는 파문과 물보라를 만든다.</summary>
    public void SpawnFromImpact(Vector3 worldPos, float impactSpeed)
    {
        SpawnFromImpact(worldPos, impactSpeed, Vector3.forward, impactAspect);
    }

    /// <summary>진행 방향과 비율을 지정한 충돌 파문.</summary>
    public void SpawnFromImpact(Vector3 worldPos, float impactSpeed, Vector3 forward, float aspect)
    {
        if (impactSpeed < minImpactSpeed) return;
        if (!IsInsideDecalRegion(worldPos)) return;

        float k = Mathf.Clamp01(Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impactSpeed));
        WaterRipple r = Next(_impact, ref _impactCursor);
        if (r != null)
        {
            r.foamStrength = impactFoam;
            r.duration = Mathf.Lerp(1.6f, 2.6f, k);
            r.Play(worldPos, Mathf.Lerp(minAmplitude, maxAmplitude, k), Mathf.Lerp(minRadius, maxRadius, k), aspect, forward);
        }
        SpawnSplash(worldPos);
    }

    /// <summary>항해 중 뱃머리에서 나오는 잔잔한 항적 파문.</summary>
    public void SpawnWake(Vector3 worldPos, float amplitude, float radius)
    {
        SpawnWake(worldPos, amplitude, radius, Vector3.forward, wakeAspect);
    }

    /// <summary>선체 방향으로 길쭉한 항적 파문.</summary>
    public void SpawnWake(Vector3 worldPos, float amplitude, float radius, Vector3 forward, float aspect)
    {
        if (!IsInsideDecalRegion(worldPos)) return;
        WaterRipple r = Next(_wake, ref _wakeCursor);
        if (r == null) return;
        r.foamStrength = wakeFoam;
        r.duration = 2.4f;
        r.Play(worldPos, amplitude, radius, aspect, forward);
    }

    public void SpawnSplash(Vector3 worldPos)
    {
        if (_splashes.Count == 0) return;
        GameObject go = _splashes[_splashCursor];
        _splashCursor = (_splashCursor + 1) % _splashes.Count;
        go.SetActive(false);
        go.transform.position = worldPos;
        go.SetActive(true);
    }

    WaterRipple Next(List<WaterRipple> list, ref int cursor)
    {
        if (list.Count == 0) return null;
        for (int i = 0; i < list.Count; i++)
        {
            int idx = (cursor + i) % list.Count;
            if (!list[idx].gameObject.activeSelf)
            {
                cursor = (idx + 1) % list.Count;
                return list[idx];
            }
        }
        WaterRipple oldest = list[cursor];
        cursor = (cursor + 1) % list.Count;
        return oldest;
    }
}
