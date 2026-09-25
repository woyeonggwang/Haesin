using UnityEngine;

/// <summary>
/// 베이스캠프 안전지대. 기준점은 플레이어 리스폰 지점(선착장)이며 반경 안에서는
///   - 적 함대가 스폰되지 않고 (EnemyFleetSpawner.PickFleetCenter)
///   - AI 배가 안으로 들어오지 않으며 (EnemyShipBase / NavyShipBase.SailTowards 가 목적지를 경계 밖으로 밀어냄)
///   - 안에 있는 배는 표적이 되지 않는다 (ShipFaction.IsTargetable).
/// 씬에 하나만 두고, 없으면 RespawnPoint.Instance 위치 + 기본 반경을 쓴다.
/// </summary>
public class BaseSafeZone : MonoBehaviour
{
    public static BaseSafeZone Instance { get; private set; }

    [Tooltip("안전지대 반경(m). 기준점에서 이 거리 안은 적이 스폰되지도 들어오지도 않는다. 섬(동굴 포함)의 가장 먼 끝이 리스폰 지점에서 약 255 m 라 200 이면 섬 남쪽 끝 바깥에 적이 생기고 좌초했다 → 350.")]
    public float radius = 350f;
    [Tooltip("기준점. 비워두면 이 오브젝트 위치(리스폰 지점에 붙여 두면 자동으로 같이 움직인다).")]
    public Transform center;

    const float DefaultRadius = 350f;

    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    public static Vector3 Center
    {
        get
        {
            if (Instance != null) return Instance.center != null ? Instance.center.position : Instance.transform.position;
            if (RespawnPoint.Instance != null) return RespawnPoint.Instance.transform.position;
            return new Vector3(0f, 0f, 1e6f); // 아무것도 없으면 사실상 무효
        }
    }

    public static float Radius { get { return Instance != null ? Instance.radius : DefaultRadius; } }

    /// <summary>수평 거리 기준으로 안전지대 안인지.</summary>
    public static bool Contains(Vector3 worldPos)
    {
        Vector3 d = worldPos - Center; d.y = 0f;
        float r = Radius;
        return d.sqrMagnitude < r * r;
    }

    /// <summary>수평 거리 기준으로 (반경 + extra) 안인지. 함대처럼 퍼져 있는 대상을 검사할 때.</summary>
    public static bool Contains(Vector3 worldPos, float extra)
    {
        Vector3 d = worldPos - Center; d.y = 0f;
        float r = Radius + extra;
        return d.sqrMagnitude < r * r;
    }

    /// <summary>
    /// 목적지가 안전지대 안이면 경계 밖(margin 만큼 여유)으로 밀어낸 점을 돌려준다. 밖이면 그대로.
    /// </summary>
    public static Vector3 PushOutside(Vector3 target, float margin)
    {
        Vector3 c = Center;
        Vector3 d = target - c; d.y = 0f;
        float r = Radius + margin;
        if (d.sqrMagnitude >= r * r) return target;
        if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
        Vector3 outp = c + d.normalized * r;
        outp.y = target.y;
        return outp;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        Vector3 c = center != null ? center.position : transform.position;
        int seg = 64; Vector3 prev = c + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; Vector3 n = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius); Gizmos.DrawLine(prev, n); prev = n; }
    }
}
