using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진영의 땅(섬) 영역. 섬 가운데에 두는 원 두 개로 정한다.
///   - innerRadius(육지 + 해안): 돌아다니는 배는 스폰도 진입도 하지 않는다. 배는 선착장 정박선(HarborMooring)만 보인다.
///   - outerRadius(영해): 이 진영의 배만 스폰된다. 육지 쪽으로 갈수록 스폰 확률이 줄어든다(inner→outer 로 0→1).
///     다른 진영의 배는 들어오지 않고, 이 안에 있는 배를 다른 진영 배가 표적으로 삼지 않는다(안전지대).
///     플레이어는 이 진영과 적대이면 이 진영 배에게는 공격받는다.
/// 플레이어 베이스는 따로 BaseSafeZone.
/// </summary>
public class FactionTerritory : MonoBehaviour
{
    public Faction faction = Faction.Navy;
    [Tooltip("육지 + 해안 반경(m). 돌아다니는 배 금지.")] public float innerRadius = 500f;
    [Tooltip("영해 반경(m). 다른 진영 배 금지, 이 진영 배만 스폰.")] public float outerRadius = 1200f;

    public static readonly List<FactionTerritory> All = new List<FactionTerritory>();

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    float FlatDist(Vector3 p) { Vector3 d = p - transform.position; d.y = 0f; return d.magnitude; }

    /// <summary>이 진영 배가 p 에 스폰될 확률 가중치(0~1). 육지 가까이·남의 영해 = 0, 바깥 바다 = 1.</summary>
    public static float SpawnWeight(Faction f, Vector3 p)
    {
        float w = 1f;
        for (int i = 0; i < All.Count; i++)
        {
            var t = All[i]; if (t == null) continue;
            float d = t.FlatDist(p);
            if (d < t.innerRadius) return 0f;
            if (d < t.outerRadius)
            {
                if (t.faction != f) return 0f;
                float k = Mathf.InverseLerp(t.innerRadius, t.outerRadius, d);
                w = Mathf.Min(w, k * k * (3f - 2f * k));
            }
        }
        return w;
    }

    /// <summary>f 진영 배가 들어가면 안 되는 반경(자기 섬 = inner, 남의 섬 = outer).</summary>
    float BlockRadius(Faction f) { return f == faction ? innerRadius : outerRadius; }

    /// <summary>목적지가 금지 구역 안이면 경계 밖(margin 여유)으로 밀어낸 점. 플레이어(해적)는 제한 없음.</summary>
    public static Vector3 PushOut(Faction f, Vector3 target, float margin)
    {
        if (f == Faction.Pirate) return target;
        for (int i = 0; i < All.Count; i++)
        {
            var t = All[i]; if (t == null) continue;
            float r = t.BlockRadius(f) + margin;
            Vector3 c = t.transform.position;
            Vector3 d = target - c; d.y = 0f;
            if (d.sqrMagnitude >= r * r) continue;
            if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
            float y = target.y;
            target = c + d.normalized * r; target.y = y;
        }
        return target;
    }

    /// <summary>배가 지금 금지 구역 안이면 true 와 빠져나갈 목적지.</summary>
    public static bool InsideBlocked(Faction f, Vector3 pos, out Vector3 exit)
    {
        exit = pos;
        if (f == Faction.Pirate) return false;
        for (int i = 0; i < All.Count; i++)
        {
            var t = All[i]; if (t == null) continue;
            float r = t.BlockRadius(f);
            if (t.FlatDist(pos) >= r) continue;
            Vector3 d = pos - t.transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
            exit = t.transform.position + d.normalized * (r + 60f); exit.y = pos.y;
            return true;
        }
        return false;
    }

    /// <summary>p 에 있는 배가 attacker 진영에게서 보호받는가(다른 진영의 영해 안).</summary>
    public static bool ProtectsFrom(Faction attacker, Vector3 p)
    {
        if (attacker == Faction.Pirate) return false;
        for (int i = 0; i < All.Count; i++)
        {
            var t = All[i]; if (t == null) continue;
            if (t.faction != attacker && t.FlatDist(p) < t.outerRadius) return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = faction == Faction.Japanese ? new Color(1f, 0.3f, 0.2f, 0.8f) : faction == Faction.Wokou ? new Color(0.65f, 0.3f, 0.85f, 0.8f) : new Color(0.3f, 0.6f, 1f, 0.8f);
        DrawCircle(innerRadius); DrawCircle(outerRadius);
    }

    void DrawCircle(float r)
    {
        Vector3 c = transform.position; Vector3 prev = c + new Vector3(r, 0f, 0f);
        for (int i = 1; i <= 64; i++) { float a = i / 64f * Mathf.PI * 2f; Vector3 n = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); Gizmos.DrawLine(prev, n); prev = n; }
    }
}
