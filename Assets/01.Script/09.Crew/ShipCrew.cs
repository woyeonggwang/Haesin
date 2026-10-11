using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배 한 척의 선원 목록이자 선원 전투의 지휘. 갑판 위 자식 오브젝트 'Crew' 아래의 CrewMember 들을 모은다.
/// 선원 수는 배 크기에 비례해 에디터에서 미리 세워 둔다(판옥선 10, 관군 판옥선 10, 안택선 12, 세키부네 6).
/// 스포너가 배를 복제하면 선원도 자식이라 함께 복제된다.
///
/// 전투: scanInterval 마다 두 배 충돌체 가장자리 사이 빈틈이 engageGap(30 m) 안인 가장 가까운 적대 배를 표적으로 잡고,
/// 선원들은 그 배의 살아 있는 선원을 쏜다(CrewMember). (사용자 지정 2026-10-05: 10 m → 30 m — 예전엔 배 중심끼리 110 m.)
/// 멀리서 끄기: 카메라에서 배(원점)까지 crewHideDistance(90 m)를 넘으면 선원 오브젝트(Crew_N)를 SetActive(false)
/// (사용자 지시 "90 m 에서 선원 오브젝트를 아예 꺼버림"). 꺼진 선원은 쏘지 않고, 맞으면 쓰러진 것으로만 기록 → 다시 켜질 때 쓰러진 자세. 선원이 모두 쓰러지면 이 배의 대포는 쏘지 못한다
/// (CanFireCannons — CannonControl·EnemyCannonControl·NavyCannonControl 이 확인, 사용자 지시).
/// 배 체력은 선원 사격으로 깎이지 않는다.
/// </summary>
public class ShipCrew : MonoBehaviour
{
    [Tooltip("이 배의 선원들. Awake 에서 자식의 CrewMember 를 자동으로 모은다.")]
    public List<CrewMember> members = new List<CrewMember>();

    [Tooltip("영입 가능한 최대 선원 수.")]
    public int maxCrew = 16;
    [Tooltip("영입한 선원이 설 자리(배 로컬, x=우현, y=갑판 높이(0 이면 기존 선원 평균), z=선수). 순서대로 채운다.")]
    public Vector3[] recruitSpots = new Vector3[] {
        new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, 6f), new Vector3(-1.5f, 0f, -8f), new Vector3(1.5f, 0f, -8f), new Vector3(-1.5f, 0f, -3f), new Vector3(-1.5f, 0f, 3f) };
    int _recruited;

    [Header("선원 전투")]
    [Tooltip("선원이 싸울지. 정박선(HarborMooring)은 끈다.")]
    public bool combatEnabled = true;
    [Tooltip("선원이 싸우기 시작하는 배 사이 빈틈(m) — 두 배 충돌체 가장자리 사이 거리(사용자 지정 30 m).")]
    public float engageGap = 30f;
    [Tooltip("명중률 거리(m, 쏘는 선원 ↔ 맞는 선원): 20 m 에서 hit 최대 → 이 거리에서 최소. (예전엔 싸우기 시작하는 배 중심 거리였음)")]
    public float combatRange = 110f;
    [Tooltip("표적을 다시 찾는 간격(초).")]
    public float scanInterval = 0.5f;
    [Tooltip("조총 장전 시간(초, 최소~최대) — 쏜 뒤 다음 사격까지 대기.")]
    public Vector2 matchlockReload = new Vector2(5f, 9f);
    [Tooltip("활 다음 사격까지 대기(초, 최소~최대).")]
    public Vector2 bowReload = new Vector2(1.5f, 4f);
    [Tooltip("조총 명중률(가까이 20 m, 멀리 = combatRange).")]
    public Vector2 matchlockHit = new Vector2(0.22f, 0.07f);
    [Tooltip("활 명중률(가까이 20 m, 멀리 = combatRange).")]
    public Vector2 bowHit = new Vector2(0.12f, 0.04f);

    [Header("멀리서 끄기")]
    [Tooltip("카메라에서 배(원점)까지 이 거리(m)를 넘으면 선원 오브젝트를 끔(사용자 지정 90 m).")]
    public float crewHideDistance = 90f;
    [Tooltip("다시 켤 때는 crewHideDistance − 이 값 안으로 들어와야 함(경계에서 깜빡임 방지).")]
    public float crewHideHysteresis = 5f;

    [Header("상태 (읽기 전용)")]
    public bool crewShown = true;
    public float engagedGap = -1f;
    public ShipFaction target;
    public ShipCrew targetCrew;
    public int aliveCount;

    /// <summary>살아 있는 선원 수가 바뀔 때(살아 있는 수, 전체).</summary>
    public System.Action<int, int> OnCrewChanged;

    ShipFaction _faction;
    ShipHealth _health;
    float _nextScan;

    public int Count { get { return members.Count; } }
    public bool HasFreeSlot { get { return members.Count < maxCrew && _recruited < recruitSpots.Length; } }
    /// <summary>살아 있는 선원이 있는가. 선원을 두지 않은 배(members 0)는 true.</summary>
    public bool HasLivingCrew { get { return members.Count == 0 || aliveCount > 0; } }

    /// <summary>이 오브젝트가 속한 배의 대포를 쏠 수 있는가(선원이 없는 배 = ShipCrew 없음은 늘 true).</summary>
    public static bool CanFireCannons(Component c)
    {
        if (c == null) return true;
        ShipCrew sc = c.GetComponentInParent<ShipCrew>();
        return sc == null || sc.HasLivingCrew;
    }

    /// <summary>선원 한 명을 recruitSpots 의 다음 자리에 추가한다(기존 선원을 복제). 성공하면 true.</summary>
    public bool AddRecruit()
    {
        if (!HasFreeSlot || members.Count == 0) return false;
        CrewMember template = members[0];
        Transform holder = template.transform.parent;
        Vector3 spot = recruitSpots[_recruited];
        if (spot.y <= 0f) { float sum = 0f; for (int i = 0; i < members.Count; i++) sum += members[i].transform.localPosition.y; spot.y = sum / members.Count; }
        var go = Instantiate(template.gameObject, holder);
        go.SetActive(true);
        go.name = "Crew_" + (members.Count + 1);
        go.transform.localPosition = spot;
        go.transform.localRotation = template.transform.localRotation;
        go.transform.localScale = template.transform.localScale;
        _recruited++;
        Collect();
        var m = go.GetComponent<CrewMember>();
        if (m != null) m.Revive();
        go.SetActive(crewShown);
        RecountAlive();
        return true;
    }

    void Awake()
    {
        _faction = GetComponentInParent<ShipFaction>();
        _health = GetComponentInParent<ShipHealth>();
        Collect();
        _nextScan = Time.time + Random.Range(0f, scanInterval);
        CacheColliders();
    }

    // 배 충돌체(트리거 아님) — 빈틈 계산용
    readonly List<Collider> _cols = new List<Collider>();
    void CacheColliders()
    {
        _cols.Clear();
        foreach (var c in GetComponentsInChildren<Collider>(true))
            if (!c.isTrigger && (c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider && ((MeshCollider)c).convex)))
                _cols.Add(c);
    }

    static Camera _cam;

    /// <summary>선원 오브젝트를 모두 켜거나 끈다.</summary>
    public void SetCrewShown(bool show)
    {
        crewShown = show;
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null && members[i].gameObject.activeSelf != show) members[i].gameObject.SetActive(show);
    }

    void UpdateCrewVisibility()
    {
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
        if (_cam == null) return;
        float d2 = (_cam.transform.position - transform.position).sqrMagnitude;
        if (crewShown) { if (d2 > crewHideDistance * crewHideDistance) SetCrewShown(false); }
        else { float on = Mathf.Max(0f, crewHideDistance - crewHideHysteresis); if (d2 < on * on) SetCrewShown(true); }
    }

    /// <summary>두 배 충돌체 가장자리 사이 거리(m, 겹치면 0). 충돌체가 없으면 중심 거리.</summary>
    public float GapTo(ShipCrew other)
    {
        if (other == null) return float.MaxValue;
        float best = float.MaxValue; bool any = false;
        for (int i = 0; i < _cols.Count; i++)
        {
            Collider a = _cols[i]; if (a == null || !a.enabled || !a.gameObject.activeInHierarchy) continue;
            for (int j = 0; j < other._cols.Count; j++)
            {
                Collider b = other._cols[j]; if (b == null || !b.enabled || !b.gameObject.activeInHierarchy) continue;
                any = true;
                // 볼록 둘 사이 가장 가까운 점 — 번갈아 투영 몇 번(정확하진 않지만 몇 cm 안)
                Vector3 q = b.bounds.center;
                Vector3 p = a.ClosestPoint(q);
                for (int k = 0; k < 4; k++) { q = b.ClosestPoint(p); p = a.ClosestPoint(q); }
                float d = Vector3.Distance(p, q);
                if (d < best) best = d;
            }
        }
        return any ? best : Vector3.Distance(transform.position, other.transform.position);
    }

    /// <summary>빈틈이 engageGap 안인 가장 가까운 적대 배(선원이 살아 있는).</summary>
    ShipCrew FindEngageTarget(out ShipFaction faction, out float gap)
    {
        faction = null; gap = -1f;
        ShipCrew best = null; float bestGap = engageGap;
        float coarse = engageGap + 60f; // 배 길이(최대 약 37 m)를 넉넉히 더한 중심 거리 — 이 밖은 계산 안 함
        Vector3 p = transform.position;
        var all = ShipFaction.All;
        for (int i = 0; i < all.Count; i++)
        {
            ShipFaction s = all[i];
            if (s == null || !_faction.IsHostileTo(s) || !ShipFaction.IsTargetableBy(_faction, s)) continue;
            if ((s.transform.position - p).sqrMagnitude > coarse * coarse) continue;
            ShipCrew c = s.GetComponentInChildren<ShipCrew>();
            if (c == null || c.aliveCount <= 0) continue;
            float g = GapTo(c);
            if (g <= bestGap) { bestGap = g; best = c; faction = s; gap = g; }
        }
        return best;
    }

    /// <summary>자식 계층에서 CrewMember 를 다시 모은다(에디터에서 선원을 추가·삭제한 뒤 호출).</summary>
    public void Collect()
    {
        members.Clear();
        GetComponentsInChildren<CrewMember>(true, members);
        for (int i = 0; i < members.Count; i++)
        {
            members[i].ship = this;
            members[i].slotIndex = i;
        }
        RecountAlive();
    }

    void RecountAlive()
    {
        int n = 0;
        for (int i = 0; i < members.Count; i++) if (members[i] != null && members[i].alive) n++;
        aliveCount = n;
    }

    void Update()
    {
        UpdateCrewVisibility();

        if (Time.time < _nextScan) return;
        _nextScan = Time.time + scanInterval;

        target = null;
        targetCrew = null;
        engagedGap = -1f;
        if (!combatEnabled || aliveCount <= 0 || _faction == null) return;
        if (_health != null && (_health.isDead || _health.isSinking)) return;

        ShipFaction f; float g;
        ShipCrew c = FindEngageTarget(out f, out g);
        if (c == null) return;
        target = f;
        targetCrew = c;
        engagedGap = g;
    }

    /// <summary>표적 배의 살아 있는 선원 하나(무작위). 없으면 null.</summary>
    public CrewMember PickTargetMember()
    {
        if (targetCrew == null || targetCrew.aliveCount <= 0) return null;
        var list = targetCrew.members;
        int n = list.Count;
        int start = Random.Range(0, n);
        for (int k = 0; k < n; k++)
        {
            var m = list[(start + k) % n];
            if (m != null && m.alive) return m;
        }
        return null;
    }

    public float ReloadTime(CrewWeapon w)
    {
        Vector2 r = w == CrewWeapon.Matchlock ? matchlockReload : bowReload;
        return Random.Range(r.x, r.y);
    }

    public float HitChance(CrewWeapon w, float distance)
    {
        Vector2 h = w == CrewWeapon.Matchlock ? matchlockHit : bowHit;
        float t = Mathf.InverseLerp(20f, Mathf.Max(21f, combatRange), distance);
        return Mathf.Lerp(h.x, h.y, t);
    }

    /// <summary>CrewMember.Kill 이 부른다.</summary>
    public void NotifyMemberDied(CrewMember m)
    {
        RecountAlive();
        if (OnCrewChanged != null) OnCrewChanged(aliveCount, members.Count);
    }

    /// <summary>선원 모두를 되살린다(플레이어 리스폰).</summary>
    public void ReviveAll()
    {
        for (int i = 0; i < members.Count; i++) if (members[i] != null) members[i].Revive();
        RecountAlive();
        if (OnCrewChanged != null) OnCrewChanged(aliveCount, members.Count);
    }
}
