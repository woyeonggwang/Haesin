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

    [Header("왜구 함대 (2026-10-07)")]
    [Tooltip("왜구 안택선 원본(씬의 anteck_wokou). 비워두면 왜구는 등장하지 않는다.")]
    public GameObject wokouAnteckTemplate;
    [Tooltip("왜구 세키부네 원본(씬의 seki_wokou).")]
    public GameObject wokouSekiTemplate;
    [Tooltip("왜구 함대당 안택선 수 (최소~최대).")]
    public Vector2Int wokouAnteckCount = new Vector2Int(0, 1);
    [Tooltip("왜구 함대당 세키부네 수 (최소~최대).")]
    public Vector2Int wokouSekiCount = new Vector2Int(1, 2);
    [Tooltip("동시에 존재할 수 있는 왜구 함대 수.")]
    public int maxWokouFleets = 1;
    [Tooltip("시작하자마자 왜구 함대를 하나 스폰할지.")]
    public bool spawnWokouOnStart = false;

    [Header("상선 호송대")]
    [Tooltip("상선 원본(템플릿). 비워두면 상선은 등장하지 않는다.")]
    public GameObject merchantTemplate;
    [Tooltip("호송대 한 무리의 상선 수 (최소~최대).")]
    public Vector2Int merchantCount = new Vector2Int(1, 3);
    [Tooltip("호송대 배들이 서로 떨어지는 간격(m). 대열 폭이 된다.")]
    public float merchantSpacing = 45f;
    [Tooltip("동시에 존재할 수 있는 상선 호송대 수.")]
    public int maxMerchantConvoys = 2;
    [Tooltip("시작하자마자 호송대를 하나 스폰할지.")]
    public bool spawnMerchantOnStart = true;


    [Header("스폰 규칙")]
    [Tooltip("동시에 존재할 수 있는 함대 수.")]
    public int maxFleets = 2;
    [Tooltip("플레이어에게서 최소 이만큼 떨어진 곳에만 스폰(m).")]
    public float minSpawnDistance = 250f;
    [Tooltip("스폰 가능한 최대 거리(m).")]
    public float maxSpawnDistance = 500f;
    [Tooltip("거리 분포의 치우침. 1이면 min~max 에 고르게, 클수록 먼 쪽에 몰려서 생성된다.")]
    [Range(0.5f, 4f)]
    public float spawnDistanceBias = 1.8f;
    [Tooltip("이 거리보다 멀어진 함대는 정리한다(m). 0이면 정리하지 않음.")]
    public float despawnDistance = 900f;
    [Tooltip("스폰 조건을 확인하는 간격(초).")]
    public float checkInterval = 8f;
    [Tooltip("시작하자마자 함대를 하나 스폰할지.")]
    public bool spawnOnStart = true;
    [Tooltip("베이스 안전지대 경계에서 함대(흩어진 배 포함)가 더 떨어져야 하는 여유(m). 예전엔 함대 중심만 검사해서 흩어진 배가 안전지대 안·섬 바로 옆에 생겼다.")]
    public float safeZoneMargin = 60f;

    [Header("상태 (읽기 전용)")]
    [Tooltip("활동 중인 왜군 함대 수.")]
    public int activeFleetCount;
    [Tooltip("활동 중인 관군 함대 수.")]
    public int activeNavyFleetCount;
    [Tooltip("활동 중인 상선 호송대 수.")]
    public int activeMerchantConvoyCount;
    [Tooltip("활동 중인 왜구 함대 수.")]
    public int activeWokouFleetCount;

    private Transform _player;
    private float _nextCheckTime;
    private readonly List<List<GameObject>> _fleets = new List<List<GameObject>>();
    private readonly List<List<GameObject>> _navyFleets = new List<List<GameObject>>();
    private readonly List<List<GameObject>> _merchantConvoys = new List<List<GameObject>>();
    private readonly List<List<GameObject>> _wokouFleets = new List<List<GameObject>>();

    void Start()
    {
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null) _player = target.transform;

        // 원본은 템플릿으로만 쓴다
        if (anteckTemplate != null) anteckTemplate.SetActive(false);
        if (sekiTemplate != null) sekiTemplate.SetActive(false);
        if (navyPanokTemplate != null) navyPanokTemplate.SetActive(false);
        if (merchantTemplate != null) merchantTemplate.SetActive(false);
        if (wokouAnteckTemplate != null) wokouAnteckTemplate.SetActive(false);
        if (wokouSekiTemplate != null) wokouSekiTemplate.SetActive(false);

        if (spawnOnStart) TrySpawnFleet();
        if (spawnWokouOnStart) TrySpawnWokouFleet();
        if (spawnNavyOnStart) TrySpawnNavyFleet();
        if (spawnMerchantOnStart) TrySpawnMerchantConvoy();
    }


    void Update()
    {
        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + checkInterval;

        CleanupFleets(_fleets);
        CleanupFleets(_navyFleets);
        CleanupFleets(_merchantConvoys);
        CleanupFleets(_wokouFleets);

        if (_fleets.Count < maxFleets) TrySpawnFleet();
        if (_navyFleets.Count < maxNavyFleets) TrySpawnNavyFleet();
        if (_merchantConvoys.Count < maxMerchantConvoys) TrySpawnMerchantConvoy();
        if (_wokouFleets.Count < maxWokouFleets) TrySpawnWokouFleet();

        activeFleetCount = _fleets.Count;
        activeNavyFleetCount = _navyFleets.Count;
        activeMerchantConvoyCount = _merchantConvoys.Count;
        activeWokouFleetCount = _wokouFleets.Count;
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
    bool PickFleetCenter(float spread, Faction faction, out Vector3 center)
    {
        // 거리를 균등하게 뽑으면 절반이 (min+max)/2 안쪽에 몰려서 적이 코앞에 나타난 것처럼 보인다.
        // 0~1 난수에 거듭제곱을 씌워 분포를 먼 쪽으로 민다. bias 가 1이면 예전과 같은 균등 분포.
        // 베이스 안전지대 안, 섬 해안(FactionTerritory inner), 다른 진영의 영해(outer)에는 스폰하지 않는다.
        // 자기 진영 영해에서는 육지에 가까울수록 확률이 줄어든다(SpawnWeight). 12번 뽑아 모두 실패하면 이번엔 스폰하지 않는다.
        center = Vector3.zero;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float u = Random.value;
            float t = Mathf.Pow(u, 1f / Mathf.Max(0.01f, spawnDistanceBias));
            float dist = Mathf.Lerp(minSpawnDistance, maxSpawnDistance, t);
            Vector3 c = _player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
            c.y = 0f;
            if (BaseSafeZone.Contains(c, spread + safeZoneMargin)) continue;
            float w = FactionTerritory.SpawnWeight(faction, c);
            if (w <= 0f || Random.value > w) continue;
            center = c;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 관군 함대를 스폰한다. 왜군 함대와 같은 규칙(플레이어에서 멀리)을 따르지만
    /// 목록을 따로 가져가서 왜군과 독립적으로 수를 유지한다.
    /// 관군은 등장한 뒤 진영 규칙에 따라 플레이어도 왜군도 알아서 상대한다.
    /// </summary>
    /// <summary>
    /// 상선 호송대를 스폰한다. 1~3척이 한 줄로 늘어서서 같이 다닌다.
    /// 맨 앞 배가 선도선이고 나머지는 그 뒤 좌우로 붙어 따라간다.
    /// 상선은 관군과 같은 진영이므로 플레이어와 왜군 모두에게 표적이 된다.
    /// </summary>
    void TrySpawnMerchantConvoy()
    {
        if (_player == null || merchantTemplate == null) return;

        Vector3 center;
        if (!PickFleetCenter(merchantSpacing * Mathf.Max(1, merchantCount.y) * 0.75f, Faction.Navy, out center)) return;
        float heading = Random.Range(0f, 360f);
        Quaternion rot = Quaternion.Euler(0f, heading, 0f);

        int n = Random.Range(merchantCount.x, merchantCount.y + 1);
        var convoy = new List<GameObject>();
        MerchantAI leader = null;

        for (int i = 0; i < n; i++)
        {
            // 선도선은 한가운데, 나머지는 뒤쪽 좌우로 번갈아 붙인다.
            float back = i * merchantSpacing * 0.75f;
            float side = (i == 0) ? 0f : ((i % 2 == 1) ? 1f : -1f) * merchantSpacing * 0.5f;
            Vector3 pos = center + rot * new Vector3(side, 0f, -back);
            pos = BaseSafeZone.PushOutside(pos, safeZoneMargin); // 한 척이라도 안전지대에 걸치면 밖으로
            pos = FactionTerritory.PushOut(Faction.Navy, pos, 20f);
            pos.y = 0f;

            GameObject ship = Instantiate(merchantTemplate, pos, rot);
            ship.SetActive(true);
            convoy.Add(ship);

            var ai = ship.GetComponent<MerchantAI>();
            if (ai == null) continue;
            if (leader == null) { leader = ai; ai.convoyLeader = null; }
            else
            {
                ai.convoyLeader = leader;
                ai.formationOffset = new Vector2(side, back);
            }
        }

        _merchantConvoys.Add(convoy);
        activeMerchantConvoyCount = _merchantConvoys.Count;
    }

    void TrySpawnNavyFleet()
    {
        if (_player == null || navyPanokTemplate == null) return;

        Vector3 center;
        if (!PickFleetCenter(navyFleetSpread, Faction.Navy, out center)) return;
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

        Vector3 center;
        if (!PickFleetCenter(fleetSpread, Faction.Japanese, out center)) return;
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


    /// <summary>왜구 함대(세키부네 1~2 + 가끔 안택선). 다른 함대와 같은 규칙(플레이어에서 250~500 m, 남의 영해 밖).</summary>
    void TrySpawnWokouFleet()
    {
        if (_player == null || wokouSekiTemplate == null) return;
        Vector3 center;
        if (!PickFleetCenter(fleetSpread, Faction.Wokou, out center)) return;
        _wokouFleets.Add(SpawnGroup(wokouAnteckTemplate, Random.Range(wokouAnteckCount.x, wokouAnteckCount.y + 1),
                                    wokouSekiTemplate, Random.Range(wokouSekiCount.x, wokouSekiCount.y + 1), center));
        activeWokouFleetCount = _wokouFleets.Count;
    }

    /// <summary>
    /// 퀘스트용: 원본 배를 플레이어 둘레 distance(m) 링에 count 척 띄운다(진영 영해·안전지대 규칙 그대로). 스포너의 함대 목록에는 넣지 않음 —
    /// 그래서 900 m 정리 대상이 아니다(퀘스트가 스스로 정리). 템플릿이 꺼져 있어도 됨.
    /// </summary>
    public List<GameObject> SpawnForQuest(GameObject template, int count, float distance, float spread)
    {
        return SpawnForQuest(template, count, distance, spread, false, null, -1);
    }

    /// <summary>
    /// 퀘스트용 배 띄우기(2026-10-09 확장). around 가 있으면 플레이어 대신 그 자리 둘레 distance 에서,
    /// ignoreTerritory 면 섬 영해 규칙(스폰 금지·밀어내기)을 무시하고 띄운 배에도 ShipFaction.ignoreTerritory 를 켬(남의 영해 안에서도 싸움).
    /// factionOverride ≥ 0 이면 띄운 배의 진영을 바꿈(예: 상선 원본을 일본 수송선으로). 육지(터레인 −2 m 위)에는 띄우지 않음.
    /// </summary>
    public List<GameObject> SpawnForQuest(GameObject template, int count, float distance, float spread, bool ignoreTerritory, Transform around, int factionOverride)
    {
        var list = new List<GameObject>();
        if (template == null || count <= 0) return list;
        if (_player == null) { var t = GameObject.FindGameObjectWithTag("Player"); if (t != null) _player = t.transform; }
        if (_player == null && around == null) return list;
        var tf = template.GetComponent<ShipFaction>();
        Faction f = factionOverride >= 0 ? (Faction)factionOverride : (tf != null ? tf.faction : Faction.Japanese);
        Vector3 origin = around != null ? around.position : _player.position;
        Vector3 center = origin;
        bool found = false;
        for (int attempt = 0; attempt < 24 && !found; attempt++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 c = origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance; c.y = 0f;
            if (BaseSafeZone.Contains(c, spread + safeZoneMargin)) continue;
            if (!ignoreTerritory && FactionTerritory.SpawnWeight(f, c) <= 0f) continue;
            if (IsLand(c, -2f)) continue;
            center = c; found = true;
        }
        if (!found)
        {
            Vector3 fwd = _player != null ? _player.forward : Vector3.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            center = BaseSafeZone.PushOutside(origin + fwd.normalized * distance, safeZoneMargin); center.y = 0f;
        }
        for (int i = 0; i < count; i++)
        {
            var ship = ignoreTerritory || factionOverride >= 0 ? SpawnQuestShip(template, center, spread, ignoreTerritory, factionOverride) : SpawnShip(template, center, spread);
            list.Add(ship);
        }
        return list;
    }

    GameObject SpawnQuestShip(GameObject template, Vector3 center, float spread, bool ignoreTerritory, int factionOverride)
    {
        Vector3 pos = center;
        for (int k = 0; k < 8; k++)
        {
            Vector2 r = Random.insideUnitCircle * spread;
            pos = center + new Vector3(r.x, 0f, r.y);
            if (!IsLand(pos, -2f)) break;
        }
        pos = BaseSafeZone.PushOutside(pos, safeZoneMargin);
        var tf = template.GetComponent<ShipFaction>();
        if (!ignoreTerritory && tf != null) pos = FactionTerritory.PushOut(tf.faction, pos, 20f);
        pos.y = 0f;
        bool was = template.activeSelf;
        if (was) template.SetActive(false);   // 복제가 꺼진 채 생기게 — 진영을 바꾼 뒤 켬
        GameObject ship = Instantiate(template, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        if (was) template.SetActive(true);
        var sf = ship.GetComponent<ShipFaction>();
        if (sf != null) { sf.ignoreTerritory = ignoreTerritory; if (factionOverride >= 0) sf.faction = (Faction)factionOverride; }
        ship.SetActive(true);
        return ship;
    }

    /// <summary>p 의 터레인 높이가 above 보다 높으면(육지·얕은 곳) true.</summary>
    public static bool IsLand(Vector3 p, float above)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            var o = t.transform.position; var sz = t.terrainData.size;
            if (p.x < o.x || p.x > o.x + sz.x || p.z < o.z || p.z > o.z + sz.z) continue;
            if (t.SampleHeight(p) + o.y > above) return true;
        }
        return false;
    }

    List<GameObject> SpawnGroup(GameObject a, int na, GameObject b, int nb, Vector3 center)
    {
        var fleet = new List<GameObject>();
        if (a != null) for (int i = 0; i < na; i++) fleet.Add(SpawnShip(a, center, fleetSpread * 0.6f));
        if (b != null) for (int i = 0; i < nb; i++) fleet.Add(SpawnShip(b, center, fleetSpread));
        return fleet;
    }

    GameObject SpawnShip(GameObject template, Vector3 center, float spread)
    {
        Vector2 r = Random.insideUnitCircle * spread;
        Vector3 pos = center + new Vector3(r.x, 0f, r.y);
        pos = BaseSafeZone.PushOutside(pos, safeZoneMargin); // 한 척이라도 안전지대에 걸치면 밖으로
        var tf = template.GetComponent<ShipFaction>();
        if (tf != null) pos = FactionTerritory.PushOut(tf.faction, pos, 20f);
        pos.y = 0f;

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
