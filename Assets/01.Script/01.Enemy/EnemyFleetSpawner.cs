using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 함대를 무작위 위치에 스폰한다.
///
/// 함대 하나 = 안택선 1~3척 + 세키부네 2~4척이 한 지점 주변에 흩어져 등장.
/// 플레이어 주변 minSpawnDistance 안에는 절대 스폰하지 않아서,
/// 먼 바다로 항해하다 보면 적 함대와 마주치는 흐름이 된다.
///
/// 씬의 anteck / seki 를 원본(템플릿)으로 쓴다. 시작할 때 템플릿은 비활성화되고
/// 이후 복제본만 활동한다. 너무 멀어진 함대는 정리해 수를 유지한다.
/// </summary>
public class EnemyFleetSpawner : MonoBehaviour
{
    [Header("템플릿 (씬의 원본 배)")]
    public GameObject anteckTemplate;
    public GameObject sekiTemplate;

    [Header("함대 구성")]
    [Tooltip("함대당 안택선 수 (최소~최대).")]
    public Vector2Int anteckCount = new Vector2Int(1, 3);
    [Tooltip("함대당 세키부네 수 (최소~최대).")]
    public Vector2Int sekiCount = new Vector2Int(2, 4);
    [Tooltip("함대 중심에서 배들이 흩어지는 반경(m).")]
    public float fleetSpread = 60f;
    [Header("관군 함대")]
    [Tooltip("관군 판옥선 원본(템플릿). 비워두면 관군은 등장하지 않는다.")]
    public GameObject navyPanokTemplate;
    [Tooltip("관군 함대당 판옥선 수 (최소~최대). 판옥선은 주력함이라 여럿씩 다니지 않는다.")]
    public Vector2Int navyCount = new Vector2Int(1, 2);
    [Tooltip("관군 함대 중심에서 배들이 흩어지는 반경(m).")]
    public float navyFleetSpread = 45f;
    [Tooltip("동시에 존재할 수 있는 관군 함대 수.")]
    public int maxNavyFleets = 1;
    [Tooltip("시작하자마자 관군 함대를 하나 스폰할지.")]
    public bool spawnNavyOnStart = true;


    [Header("스폰 규칙")]
    [Tooltip("동시에 존재할 수 있는 함대 수.")]
    public int maxFleets = 2;
    [Tooltip("플레이어에게서 최소 이만큼 떨어진 곳에만 스폰(m).")]
    public float minSpawnDistance = 250f;
    [Tooltip("스폰 가능한 최대 거리(m).")]
    public float maxSpawnDistance = 500f;
    [Tooltip("이 거리보다 멀어진 함대는 정리한다(m). 0이면 정리하지 않음.")]
    public float despawnDistance = 900f;
    [Tooltip("스폰 조건을 확인하는 간격(초).")]
    public float checkInterval = 8f;
    [Tooltip("시작하자마자 함대를 하나 스폰할지.")]
    public bool spawnOnStart = true;

    [Header("상태 (읽기 전용)")]
    [Tooltip("활동 중인 왜군 함대 수.")]
    public int activeFleetCount;
    [Tooltip("활동 중인 관군 함대 수.")]
    public int activeNavyFleetCount;

    private Transform _player;
    private float _nextCheckTime;
    private readonly List<List<GameObject>> _fleets = new List<List<GameObject>>();
    private readonly List<List<GameObject>> _navyFleets = new List<List<GameObject>>();

    void Start()
    {
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null) _player = target.transform;

        // 원본은 템플릿으로만 쓴다
        if (anteckTemplate != null) anteckTemplate.SetActive(false);
        if (sekiTemplate != null) sekiTemplate.SetActive(false);
        if (navyPanokTemplate != null) navyPanokTemplate.SetActive(false);

        if (spawnOnStart) TrySpawnFleet();
        if (spawnNavyOnStart) TrySpawnNavyFleet();
    }


    void Update()
    {
        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + checkInterval;

        CleanupFleets(_fleets);
        CleanupFleets(_navyFleets);

        if (_fleets.Count < maxFleets) TrySpawnFleet();
        if (_navyFleets.Count < maxNavyFleets) TrySpawnNavyFleet();

        activeFleetCount = _fleets.Count;
        activeNavyFleetCount = _navyFleets.Count;
    }


    void CleanupFleets(List<List<GameObject>> fleets)
    {
        for (int i = fleets.Count - 1; i >= 0; i--)
        {
            List<GameObject> fleet = fleets[i];
            fleet.RemoveAll(s => s == null);

            if (fleet.Count == 0) { fleets.RemoveAt(i); continue; }

            if (despawnDistance > 0f && _player != null)
            {
                bool allFar = true;
                for (int j = 0; j < fleet.Count; j++)
                {
                    if (Vector3.Distance(fleet[j].transform.position, _player.position) < despawnDistance)
                    {
                        allFar = false;
                        break;
                    }
                }
                if (allFar)
                {
                    for (int j = 0; j < fleet.Count; j++) Destroy(fleet[j]);
                    fleets.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>플레이어 기준 min~max 거리의 무작위 방향에 함대 중심을 잡는다.</summary>
    Vector3 PickFleetCenter()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 center = _player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
        center.y = 0f;
        return center;
    }

    /// <summary>
    /// 관군 함대를 스폰한다. 왜군 함대와 같은 규칙(플레이어에서 멀리)을 따르지만
    /// 목록을 따로 가져가서 왜군과 독립적으로 수를 유지한다.
    /// 관군은 등장한 뒤 진영 규칙에 따라 플레이어도 왜군도 알아서 상대한다.
    /// </summary>
    void TrySpawnNavyFleet()
    {
        if (_player == null || navyPanokTemplate == null) return;

        Vector3 center = PickFleetCenter();
        var fleet = new List<GameObject>();

        int n = Random.Range(navyCount.x, navyCount.y + 1);
        for (int i = 0; i < n; i++)
            fleet.Add(SpawnShip(navyPanokTemplate, center, navyFleetSpread));

        _navyFleets.Add(fleet);
        activeNavyFleetCount = _navyFleets.Count;
    }


    void TrySpawnFleet()
    {
        if (_player == null || anteckTemplate == null || sekiTemplate == null) return;

        Vector3 center = PickFleetCenter();
        var fleet = new List<GameObject>();

        int nAnteck = Random.Range(anteckCount.x, anteckCount.y + 1);
        int nSeki = Random.Range(sekiCount.x, sekiCount.y + 1);

        for (int i = 0; i < nAnteck; i++)
            fleet.Add(SpawnShip(anteckTemplate, center, fleetSpread * 0.6f));
        for (int i = 0; i < nSeki; i++)
            fleet.Add(SpawnShip(sekiTemplate, center, fleetSpread));

        _fleets.Add(fleet);
        activeFleetCount = _fleets.Count;
    }


    GameObject SpawnShip(GameObject template, Vector3 center, float spread)
    {
        Vector2 r = Random.insideUnitCircle * spread;
        Vector3 pos = center + new Vector3(r.x, 0f, r.y);

        GameObject ship = Instantiate(template, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        ship.SetActive(true);
        return ship;
    }

    void OnDrawGizmosSelected()
    {
        Transform p = _player;
        if (p == null)
        {
            GameObject t = GameObject.FindGameObjectWithTag("Player");
            if (t != null) p = t.transform;
        }
        if (p == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(p.position, minSpawnDistance);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(p.position, maxSpawnDistance);
    }
}
