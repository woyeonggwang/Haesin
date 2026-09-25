using UnityEngine;

/// <summary>
/// AI 포격용 탄도 계산 (정적 유틸리티).
///
/// 예전에는 EnemyCannonControl / NavyCannonControl 이 표적의 "현재 위치"로 포물선을 쐈다.
/// 포탄이 90m 를 날아가는 동안(약 3~4초) 배가 이미 10~20m 움직여 있으므로,
/// 직진만 하면 거의 맞지 않아 적함 잡기가 너무 쉬웠다 (2026-09-17 사용자 지적).
/// → 표적 Rigidbody 속도로 비행시간 동안의 이동량을 예측해 그 지점을 겨눈다.
///
/// 비행시간은 조준점에 따라 달라지므로 고정점 반복(3회)으로 수렴시킨다.
/// 발사 앙각이 고정이므로 수평 거리 d 에서 비행시간 t = d / (v·cosθ).
/// </summary>
public static class BallisticAim
{
    /// <summary>
    /// 고정 앙각 포물선의 발사 속도. 해가 없으면 직사 폴백. (플레이어 FireTarget 과 같은 공식)
    /// </summary>
    public static Vector3 LaunchVelocity(Vector3 from, Vector3 aim, float firingAngleDeg, out float flightTime)
    {
        Vector3 dir = aim - from;
        Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z);
        float d = dirXZ.magnitude;
        float h = dir.y;
        float g = Mathf.Abs(Physics.gravity.y);
        float a = firingAngleDeg * Mathf.Deg2Rad;

        float denom = 2f * (d * Mathf.Tan(a) - h) * Mathf.Cos(a) * Mathf.Cos(a);
        if (denom <= 0.001f || d < 0.01f)
        {
            Vector3 v0 = (dirXZ.normalized + Vector3.up * 0.25f).normalized * 30f;
            flightTime = d / 30f;
            return v0;
        }

        float speed = Mathf.Sqrt(g * d * d / denom);
        Vector3 forward = dirXZ / d;
        Vector3 launchDir = Quaternion.LookRotation(forward) * Quaternion.Euler(-firingAngleDeg, 0f, 0f) * Vector3.forward;
        flightTime = d / Mathf.Max(speed * Mathf.Cos(a), 0.01f);
        return launchDir * speed;
    }

    /// <summary>
    /// 표적 속도를 반영한 예측 조준점.
    /// leadFactor 1 = 완전 예측, 0 = 현재 위치. maxLeadTime 으로 과도한 예측을 자른다.
    /// </summary>
    public static Vector3 PredictAim(Vector3 from, Vector3 targetPos, Vector3 targetVel, float firingAngleDeg,
                                     float leadFactor, float maxLeadTime)
    {
        targetVel.y = 0f;
        Vector3 aim = targetPos;
        for (int i = 0; i < 3; i++)
        {
            float t;
            LaunchVelocity(from, aim, firingAngleDeg, out t);
            t = Mathf.Min(t, maxLeadTime);
            aim = targetPos + targetVel * (t * leadFactor);
        }
        return aim;
    }

    /// <summary>표적(배)의 수평 속도. Rigidbody 가 없으면 0.</summary>
    public static Vector3 VelocityOf(Transform t)
    {
        if (t == null) return Vector3.zero;
        Rigidbody rb = t.GetComponentInParent<Rigidbody>();
        if (rb == null) return Vector3.zero;
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        return v;
    }
}
