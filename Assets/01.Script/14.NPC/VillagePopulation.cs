using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 마을 하나의 돌아다니는 사람들. 카메라가 마을 구역(상자)에서 spawnDistance 안으로 들어오면 count 명을 조금씩 만들고,
/// despawnDistance 밖으로 나가면 모두 끈다(풀에 남겨 두고 다시 들어오면 새 자리에서 켬).
/// 사람은 NavMesh(이 오브젝트의 NavMeshSurface 로 미리 구움) 위를 Villager 가 돌아다니게 한다.
/// 자리는 구역 상자 안 무작위 점을 NavMesh 에 붙이고, anchor(길 위 한 점)까지 길이 이어지는 곳만 쓴다
/// (담 안 마당·지붕 위에 생긴 NavMesh 조각, 물 밑을 거름).
/// 사람 한 명씩: 카메라에서 hideDistance(60 m) 밖이면 오브젝트를 끄고, hideDistance − hideHysteresis 안이면 다시 켬
/// (사용자 지시 2026-10-05). 꺼진 동안은 제자리에 멈춰 있음.
/// </summary>
public class VillagePopulation : MonoBehaviour
{
    [Header("사람")]
    [Tooltip("섞어 쓸 사람 프리팹(시민 모델). 무작위로 고름.")]
    public GameObject[] prefabs;
    [Tooltip("사람 Animator 에 넣을 컨트롤러(Idle/Walk 블렌드, float Speed).")]
    public RuntimeAnimatorController controller;
    [Tooltip("동시에 돌아다니는 사람 수.")]
    public int count = 20;

    [Header("구역 (이 오브젝트 기준 상자)")]
    public Vector3 areaCenter = Vector3.zero;
    public Vector3 areaSize = new Vector3(200f, 100f, 200f);
    [Tooltip("길이 이어져 있는지 보는 기준점(길 위, 월드). 여기서 길이 이어지지 않는 자리는 안 씀.")]
    public Vector3 anchor;
    [Tooltip("이 높이(월드 y) 아래 자리는 안 씀(물가).")]
    public float minHeight = 1.2f;

    [Header("생성 거리 (카메라 ↔ 구역 상자)")]
    public float spawnDistance = 450f;
    public float despawnDistance = 600f;
    [Tooltip("한 프레임에 만드는 최대 사람 수(멈칫 줄이기).")]
    public int spawnPerFrame = 2;

    [Header("한 명씩 끄기 (카메라 ↔ 사람)")]
    [Tooltip("이 거리(m) 밖의 사람 오브젝트를 끔(사용자 지정 60 m).")]
    public float hideDistance = 60f;
    [Tooltip("다시 켤 때는 hideDistance − 이 값 안으로 들어와야 함(경계 깜빡임 방지).")]
    public float hideHysteresis = 5f;

    [Header("상태 (읽기 전용)")]
    public bool populated;
    public int activeCount;
    [Tooltip("쓰는 중인 사람 중 카메라에서 멀어 꺼진 수.")]
    public int hiddenCount;

    readonly List<Villager> _pool = new List<Villager>();
    float _nextCheck;
    int _toSpawn;
    NavMeshPath _path;

    void OnDisable() { Despawn(); }

    void Update()
    {
        if (Time.time >= _nextCheck)
        {
            _nextCheck = Time.time + 0.5f;
            Camera cam = Camera.main;
            if (cam == null) return;
            float d = DistanceToArea(cam.transform.position);
            if (!populated && d < spawnDistance) { populated = true; _toSpawn = count; }
            else if (populated && d > despawnDistance) Despawn();
        }
        if (populated && _toSpawn > 0)
        {
            for (int i = 0; i < spawnPerFrame && _toSpawn > 0; i++) { SpawnOne(); _toSpawn--; }
        }
        if (populated) UpdateHidden();
    }

    void UpdateHidden()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        float off2 = hideDistance * hideDistance;
        float onD = Mathf.Max(0f, hideDistance - hideHysteresis), on2 = onD * onD;
        int hidden = 0;
        for (int i = 0; i < _pool.Count; i++)
        {
            Villager v = _pool[i];
            if (v == null || !v.inUse) continue;
            float d2 = (v.transform.position - c).sqrMagnitude;
            bool act = v.gameObject.activeSelf;
            if (act && d2 > off2) { v.gameObject.SetActive(false); act = false; }
            else if (!act && d2 < on2) { v.gameObject.SetActive(true); act = true; }
            if (!act) hidden++;
        }
        hiddenCount = hidden;
    }

    float DistanceToArea(Vector3 p)
    {
        Vector3 local = transform.InverseTransformPoint(p) - areaCenter;
        Vector3 h = areaSize * 0.5f;
        Vector3 q = new Vector3(Mathf.Max(Mathf.Abs(local.x) - h.x, 0f), 0f, Mathf.Max(Mathf.Abs(local.z) - h.z, 0f));
        return q.magnitude;
    }

    void SpawnOne()
    {
        Vector3 pos;
        if (!TryRandomPoint(out pos, 30)) return;
        Villager v = null;
        for (int i = 0; i < _pool.Count; i++) if (_pool[i] != null && !_pool[i].inUse) { v = _pool[i]; break; }
        if (v == null)
        {
            if (prefabs == null || prefabs.Length == 0) return;
            var pf = prefabs[Random.Range(0, prefabs.Length)];
            if (pf == null) return;
            var go = Instantiate(pf, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            go.SetActive(false);
            go.name = pf.name + "_" + _pool.Count;
            var an = go.GetComponent<Animator>();
            if (an != null) { an.runtimeAnimatorController = controller; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f; agent.height = 1.7f; agent.baseOffset = 0f;
            agent.angularSpeed = 300f; agent.acceleration = 3f; agent.stoppingDistance = 0.6f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);
            agent.autoTraverseOffMeshLink = true;
            v = go.AddComponent<Villager>();
            v.population = this;
            _pool.Add(v);
        }
        v.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        v.inUse = true;
        v.gameObject.SetActive(true);
        v.Begin(pos);
        RecountActive();
    }

    void Despawn()
    {
        populated = false;
        _toSpawn = 0;
        for (int i = 0; i < _pool.Count; i++) if (_pool[i] != null) { _pool[i].inUse = false; _pool[i].gameObject.SetActive(false); }
        hiddenCount = 0;
        RecountActive();
    }

    void RecountActive()
    {
        int n = 0;
        for (int i = 0; i < _pool.Count; i++) if (_pool[i] != null && _pool[i].inUse) n++;
        activeCount = n;
    }

    /// <summary>구역 안 무작위 자리(NavMesh 위, 길이 이어진 곳). tries 번 안에 못 찾으면 false.</summary>
    public bool TryRandomPoint(out Vector3 result, int tries)
    {
        return TryRandomPointNear(Vector3.zero, -1f, out result, tries);
    }

    /// <summary>from 에서 minDist~maxDist 떨어진 자리를 우선 찾는다(maxDist ≤ 0 이면 구역 전체).</summary>
    public bool TryRandomPointNear(Vector3 from, float maxDist, out Vector3 result, int tries)
    {
        if (_path == null) _path = new NavMeshPath();
        Vector3 h = areaSize * 0.5f;
        for (int k = 0; k < tries; k++)
        {
            Vector3 world;
            if (maxDist > 0f)
            {
                Vector2 r = Random.insideUnitCircle * maxDist;
                world = from + new Vector3(r.x, 0f, r.y);
                Vector3 l = transform.InverseTransformPoint(world) - areaCenter;
                if (Mathf.Abs(l.x) > h.x || Mathf.Abs(l.z) > h.z) continue;
            }
            else
            {
                Vector3 l = areaCenter + new Vector3(Random.Range(-h.x, h.x), 0f, Random.Range(-h.z, h.z));
                world = transform.TransformPoint(l);
            }
            world.y = GroundHeight(world) + 0.3f;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(world, out hit, 3f, NavMesh.AllAreas)) continue;
            if (hit.position.y < minHeight) continue;
            if (Mathf.Abs(hit.position.y - world.y) > 2.5f) continue;   // 지붕 위 등
            if (!NavMesh.CalculatePath(hit.position, anchor, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;
            result = hit.position;
            return true;
        }
        result = Vector3.zero;
        return false;
    }

    static float GroundHeight(Vector3 p)
    {
        RaycastHit hit;
        // 터레인만 보고 싶지만 레이어가 없으니 위에서 쏴 가장 낮은 맞음을 땅으로 봄(지붕을 피해서)
        var hits = Physics.RaycastAll(new Vector3(p.x, 400f, p.z), Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        bool any = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider is TerrainCollider) return hits[i].point.y;
            any = true; best = Mathf.Max(best, hits[i].point.y);
        }
        if (Physics.Raycast(new Vector3(p.x, 400f, p.z), Vector3.down, out hit, 500f)) return hit.point.y;
        return any ? best : 0f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(areaCenter, areaSize);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(anchor, 3f);
    }
}
