using System.Collections;
using UnityEngine;

/// <summary>
/// 안택선 포격. EnemyAI 가 Broadside(교전) 상태이고 플레이어 쪽 현이 정렬되면
/// 그 현의 대포들을 일제사한다. 포탄은 플레이어의 Bullet(Sphere)과 같은 것을 쓰되
/// shotMode 를 Enemy 로 설정한다.
///
/// portPoints / starboardPoints 를 비워두면 자식의 CanonL / CanonR 그룹에서
/// ShotPoint 들을 자동으로 수집한다.
/// </summary>
public class EnemyCannonControl : MonoBehaviour
{
    [Header("포탄")]
    [Tooltip("발사할 포탄. 플레이어 CannonControl 과 같은 Sphere 를 쓰면 된다.")]
    public GameObject cannonBallPrefab;

    [Header("발사 지점")]
    [Tooltip("좌현 발사 지점들. 비우면 CanonL 그룹에서 자동 수집.")]
    public Transform[] portPoints;
    [Tooltip("우현 발사 지점들. 비우면 CanonR 그룹에서 자동 수집.")]
    public Transform[] starboardPoints;

    [Header("사격")]
    [Tooltip("이 거리 안에서만 포격한다(m).")]
    public float fireRange = 90f;
    [Tooltip("일제사 간격(초).")]
    public float volleyInterval = 7f;
    [Tooltip("일제사 간격에 더해지는 무작위 시간(초).")]
    public Vector2 volleyJitter = new Vector2(0f, 3f);
    [Tooltip("일제사 안에서 각 대포의 발사 시차 최대값(초). 우르르 쏘는 느낌.")]
    public float perShotDelayMax = 0.6f;
    [Tooltip("포탄 발사 앙각(도).")]
    public float firingAngle = 25f;
    [Tooltip("목표 주변으로 퍼지는 탄착 반경(m). 클수록 못 맞춘다.")]
    public float inaccuracy = 8f;
    [Tooltip("플레이어 쪽 현이 이 각도 이내로 정렬됐을 때만 발사한다(도).")]
    public float sideArc = 55f;

    [Header("예측 사격")]
    [Tooltip("표적 이동 예측 비율. 1 = 비행시간 동안의 이동량을 전부 반영, 0 = 현재 위치를 쏨(예전 방식).")]
    [Range(0f, 1.2f)] public float leadFactor = 1f;
    [Tooltip("예측에 쓰는 비행시간 상한(초). 먼 거리에서 과하게 앞을 쏘는 것을 막는다.")]
    public float maxLeadTime = 6f;

    [Header("상태 (읽기 전용)")]
    public int volleysFired;

    private EnemyAI _ai;
    private ShipFaction _self;
    private Transform _player;
    private float _nextVolleyTime;

    void Start()
    {
        _ai = GetComponent<EnemyAI>();
        _self = GetComponent<ShipFaction>();
        // 표적은 Start 에서 고정하지 않는다.
        // 진영이 셋이 되면서 표적이 플레이어일 수도, 관군함일 수도 있고
        // 교전 중에도 바뀌므로, 매번 EnemyAI 가 고른 표적을 그대로 따른다.

        if (portPoints == null || portPoints.Length == 0)
            portPoints = CollectShotPoints("CanonL");
        if (starboardPoints == null || starboardPoints.Length == 0)
            starboardPoints = CollectShotPoints("CanonR");
    }

    Transform[] CollectShotPoints(string groupName)
    {
        Transform group = transform.Find(groupName);
        if (group == null) return new Transform[0];

        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform cannon in group)
        {
            // 대포 메시 자식의 ShotPoint 를 찾는다
            for (int i = 0; i < cannon.childCount; i++)
            {
                Transform c = cannon.GetChild(i);
                if (c.name.StartsWith("ShotPoint")) list.Add(c);
            }
        }
        return list.ToArray();
    }

    void Update()
    {
        if (_ai == null || cannonBallPrefab == null) return;

        _player = _ai.CurrentTarget;
        if (_player == null) return;
        if (_ai.state != EnemyAI.State.Broadside) return;
        if (_ai.distanceToPlayer > fireRange) return;
        if (Time.time < _nextVolleyTime) return;

        // 플레이어가 어느 현 쪽인가
        Vector3 to = _player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        to.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        float side = Vector3.Dot(right, to);
        Vector3 sideDir = side >= 0f ? right : -right;
        if (Vector3.Angle(sideDir, to) > sideArc) return; // 아직 현측 정렬이 안 됐다

        Transform[] points = side >= 0f ? starboardPoints : portPoints;
        if (points == null || points.Length == 0) return;

        StartCoroutine(VolleyRoutine(points));
        volleysFired++;
        _nextVolleyTime = Time.time + volleyInterval + Random.Range(volleyJitter.x, volleyJitter.y);
    }

    IEnumerator VolleyRoutine(Transform[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            yield return new WaitForSeconds(Random.Range(0f, perShotDelayMax / Mathf.Max(points.Length, 1)) + (i > 0 ? perShotDelayMax / points.Length : 0f));
            if (_player == null) yield break;
            Fire(points[i]);
        }
    }

    void Fire(Transform firePoint)
    {
        // 예측 조준점: 표적이 포탄 비행시간 동안 움직일 자리 (BallisticAim 참고)
        Vector3 predicted = BallisticAim.PredictAim(firePoint.position, _player.position,
            BallisticAim.VelocityOf(_player), firingAngle, leadFactor, maxLeadTime);

        // 탄착점: 예측 지점 주변 무작위
        Vector2 spread = Random.insideUnitCircle * inaccuracy;
        Vector3 aim = predicted + new Vector3(spread.x, 0f, spread.y);

        float flightTime;
        Vector3 launchVelocity = BallisticAim.LaunchVelocity(firePoint.position, aim, firingAngle, out flightTime);

        GameObject ball = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = ball.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.shotMode = ShotMode.Enemy;
            bullet.useFaction = true;
            bullet.ownerFaction = _self != null ? _self.faction : Faction.Japanese;
        }
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        ball.SetActive(true);
        rb.useGravity = true;
        rb.linearVelocity = launchVelocity;
    }
}
