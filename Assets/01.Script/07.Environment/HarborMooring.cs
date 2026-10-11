using UnityEngine;

/// <summary>
/// 섬 선착장 앞바다의 배. 시작할 때 씬의 원본 배(스포너 템플릿: panok_navy, anteck, seki, merchant)를 복제해 세운다.
/// 예전(2026-10-04): 선착장 바로 옆에 kinematic 으로 세워 둠 → 멈춰 있기만 하고 하차를 막았으며 부딪혀도 안 밀림(사용자 지적, 2026-10-05).
/// 지금: 선착장 앞 뱃길 옆의 깊은 물에 원(하차 지점에서 minLandingClearance 밖, 뱃길에서 laneClearance 밖)을 찾아 HarborPatrol 로 천천히 돌게 함.
/// 물리(힘)로 움직여 부딪히면 밀려남. AI 는 끄고, 대포는 HarborPatrol 이 고른 적대 배를 쏨(2026-10-05 — 예전엔 포격도 끔), 선원은 싸우지 않음.
/// 진영(ShipFaction)·체력(ShipHealth)은 그대로라 우호도 규칙대로 적대가 되면 쏜다. 알맞은 원을 못 찾으면 예전처럼 정박(kinematic)·무장 해제하고 경고.
/// 섬 가까이(FactionTerritory inner)에는 돌아다니는 배가 없으므로 섬에 다가가면 이 배들만 보인다.
/// </summary>
public class HarborMooring : MonoBehaviour
{
    [System.Serializable]
    public struct Spot
    {
        [Tooltip("씬의 원본 배 이름(panok_navy / anteck / seki / merchant).")] public string templateName;
        [Tooltip("원을 못 찾았을 때 정박할 자리 — 이 오브젝트 기준 위치(m, 로컬).")] public Vector3 localPosition;
        [Tooltip("원을 못 찾았을 때 정박 방향(°, 로컬 Y).")] public float localYaw;
    }

    public Spot[] spots;

    [Header("도는 원 찾기 (로컬 +x = 바다 쪽)")]
    [Tooltip("원 가운데를 찾을 바다 쪽 거리 범위(m).")] public float searchMin = 80f, searchMax = 260f, searchStep = 20f;
    [Tooltip("시도할 반지름(m, 큰 것부터).")] public float[] radii = { 45f, 35f };
    [Tooltip("원과 하차 지점(LandingPoint) 사이 최소 거리(m).")] public float minLandingClearance = 60f;
    [Tooltip("선착장에서 바다 쪽으로 곧게 나가는 뱃길(로컬 z 0)과 원 사이 최소 거리(m) — 플레이어가 들어오는 길을 비움.")] public float laneClearance = 25f;
    [Tooltip("먼저 찾아볼 옆(로컬 z 부호). 정박선이 있던 +z 쪽 = 1(플레이어 배 자리는 −z 쪽).")] public float preferSide = 1f;
    [Tooltip("원 양옆으로 이만큼(m)까지 물이어야 함.")] public float waterMargin = 14f;
    [Tooltip("이 높이(m)보다 높은 바닥이 맞으면 얕은 물·땅으로 봄.")] public float shallowY = -5f;
    [Tooltip("위에서 보아 시계 방향으로 돌지.")] public bool clockwise = false;

    [Header("결과 (읽기 전용)")]
    public bool patrolFound;
    public Vector3 patrolCenter;
    public float patrolRadius;
    [Tooltip("생성된 배(읽기 전용).")] public GameObject[] spawned;

    void Start()
    {
        if (spots == null) return;
        patrolFound = FindCircle(out patrolCenter, out patrolRadius);
        if (!patrolFound) Debug.LogWarning("[HarborMooring] " + PathName() + ": 도는 원을 못 찾아 예전처럼 정박시킴");

        spawned = new GameObject[spots.Length];
        int n = spots.Length;
        for (int i = 0; i < n; i++)
        {
            var tpl = FindTemplate(spots[i].templateName);
            if (tpl == null) { Debug.LogWarning("[HarborMooring] 원본 배 없음: " + spots[i].templateName); continue; }

            Vector3 p; Quaternion r;
            if (patrolFound)
            {
                // 원 위에 고르게(위상 i/n), 도는 방향의 접선을 보게
                Vector3 dock = patrolCenter - transform.position; dock.y = 0f;
                float a0 = Mathf.Atan2(-dock.z, -dock.x); // 원에서 선착장 쪽 각도
                float a = a0 + Mathf.PI * 0.5f + i * Mathf.PI * 2f / n; // 선착장 쪽 바로 앞은 피함
                p = patrolCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * patrolRadius;
                Vector3 t = clockwise ? new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) : new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                r = Quaternion.LookRotation(t, Vector3.up);
            }
            else
            {
                p = transform.TransformPoint(spots[i].localPosition); p.y = 0f;
                r = Quaternion.Euler(0f, transform.eulerAngles.y + spots[i].localYaw, 0f);
            }

            // 원본이 아직 켜져 있으면(스포너 Start 전) 복제본의 AI 가 깨어나지 않게 잠깐 끈 채로 복제
            bool was = tpl.activeSelf;
            tpl.SetActive(false);
            var go = Instantiate(tpl, p, r);
            tpl.SetActive(was);
            go.name = (patrolFound ? "Harbor_" : "Moored_") + spots[i].templateName;
            Disarm(go, patrolFound);
            if (patrolFound)
            {
                var hp = go.AddComponent<HarborPatrol>();
                hp.center = patrolCenter;
                hp.radius = patrolRadius;
                hp.clockwise = clockwise;
            }
            else MakeMoored(go);
            go.SetActive(true);
            spawned[i] = go;
        }
    }

    string PathName() { return transform.parent != null ? transform.parent.name + "/" + name : name; }

    /// <summary>
    /// 둘레가 모두 깊은 물이고 하차 지점에서 떨어진 원을 찾는다. 먼저 선착장 앞 뱃길(로컬 z 0)을 비우는 옆쪽 원
    /// (preferSide 쪽부터, 선착장에 가까운 것부터), 없으면 뱃길 위의 원.
    /// </summary>
    public bool FindCircle(out Vector3 center, out float radius)
    {
        center = Vector3.zero; radius = 0f;
        Vector3 landing = NearestLanding();
        float side = preferSide >= 0f ? 1f : -1f;
        foreach (float rad in radii)
            for (float d = searchMin; d <= searchMax + 0.01f; d += searchStep)
                for (int k = 0; k < 2; k++)
                {
                    float sgn = k == 0 ? side : -side;
                    for (float off = rad + laneClearance; off <= rad + laneClearance + 60.01f; off += 15f)
                        if (TryCircle(d, sgn * off, rad, landing, ref center)) { radius = rad; return true; }
                }
        float[] sides = { 0f, 40f, -40f };
        foreach (float rad in radii)
            for (float d = searchMin; d <= searchMax + 0.01f; d += searchStep)
                foreach (float z0 in sides)
                    if (TryCircle(d, z0, rad, landing, ref center)) { radius = rad; return true; }
        return false;
    }

    bool TryCircle(float d, float z0, float rad, Vector3 landing, ref Vector3 center)
    {
        Vector3 c = transform.TransformPoint(new Vector3(d, 0f, z0)); c.y = 0f;
        Vector3 dl = landing - c; dl.y = 0f;
        if (dl.magnitude - rad < minLandingClearance) return false;
        if (!RingIsWater(c, rad)) return false;
        center = c;
        return true;
    }

    Vector3 NearestLanding()
    {
        Vector3 best = transform.position; float bd = float.MaxValue;
        foreach (var lp in FindObjectsByType<LandingPoint>(FindObjectsSortMode.None))
        {
            float dd = (lp.transform.position - transform.position).sqrMagnitude;
            if (dd < bd) { bd = dd; best = lp.transform.position; }
        }
        return best;
    }

    bool RingIsWater(Vector3 c, float rad)
    {
        for (int k = 0; k < 36; k++)
        {
            float a = k * Mathf.PI * 2f / 36f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int s = -1; s <= 1; s++)
                if (!IsDeep(c + dir * (rad + s * waterMargin))) return false;
        }
        return true;
    }

    bool IsDeep(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, 200f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.rigidbody != null) continue; // 배 등
            if (h.point.y > shallowY) return false;
        }
        return true;
    }

    static GameObject FindTemplate(string n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        foreach (var sf in Resources.FindObjectsOfTypeAll<ShipFaction>())
        {
            if (sf == null || sf.gameObject.scene.name == null || !sf.gameObject.scene.IsValid()) continue; // 에셋 제외
            if (sf.transform.parent == null && sf.gameObject.name == n) return sf.gameObject;
        }
        return null;
    }

    /// <summary>AI 를 끄고 선원은 싸우지 않게. 도는 배(patrol)는 대포·노를 켜 둠, 정박선은 둘 다 끔.</summary>
    static void Disarm(GameObject go, bool patrol)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            if (mb is EnemyShipBase || mb is NavyShipBase || mb.GetType().Name.EndsWith("AI"))
                mb.enabled = false;
            else if (!patrol && (mb is OarAnimator || mb.GetType().Name.Contains("CannonControl")))
                mb.enabled = false;
        }
        foreach (var crew in go.GetComponentsInChildren<ShipCrew>(true)) crew.combatEnabled = false;
        var b = go.GetComponent<ShipBuoyancy>();
        if (b != null && patrol) b.mode = ShipBuoyancy.Mode.Physics;
    }

    static void MakeMoored(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
        var b = go.GetComponent<ShipBuoyancy>();
        if (b != null) b.mode = ShipBuoyancy.Mode.Transform;
    }

    void OnDrawGizmosSelected()
    {
        if (patrolFound)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Vector3 prev = patrolCenter + new Vector3(patrolRadius, 0f, 0f);
            for (int i = 1; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 q = patrolCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * patrolRadius;
                Gizmos.DrawLine(prev, q); prev = q;
            }
        }
        if (spots == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        foreach (var s in spots)
        {
            Vector3 p = transform.TransformPoint(s.localPosition); p.y = 0f;
            Gizmos.DrawWireSphere(p, 4f);
            Gizmos.DrawLine(p, p + Quaternion.Euler(0f, transform.eulerAngles.y + s.localYaw, 0f) * Vector3.forward * 15f);
        }
    }
}
