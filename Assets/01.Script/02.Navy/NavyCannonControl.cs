using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 관군 판옥선 포격. NavyPanokAI 가 Broadside(교전) 상태이고 표적 쪽 현이 정렬되면
/// 그 현의 대포들을 일제사한다.
///
/// 표적이 플레이어로 고정되어 있지 않다는 점만 빼면 계산은 다른 배들과 같은 포물선 탄도다.
/// 포탄에는 이 배의 진영(Navy)을 찍어 보내므로, 같은 관군 배에는 맞아도 터지지 않는다.
///
/// portPoints / starboardPoints 를 비워두면 자식의 CanonL / CanonR 그룹에서
/// ShotPoint 로 시작하는 지점들을 자동 수집한다.
/// </summary>
public class NavyCannonControl : MonoBehaviour
{
    [Header("포탄")]
    [Tooltip("발사할 포탄. 플레이어/왜군과 같은 Sphere 프리팹을 쓰면 된다.")]
    public GameObject cannonBallPrefab;

    [Header("발사 지점")]
    [Tooltip("좌현 발사 지점들. 비우면 CanonL 그룹에서 자동 수집.")]
    public Transform[] portPoints;
    [Tooltip("우현 발사 지점들. 비우면 CanonR 그룹에서 자동 수집.")]
    public Transform[] starboardPoints;

    [Header("사격")]
    [Tooltip("이 거리 안에서만 포격한다(m).")]
    public float fireRange = 95f;
    [Tooltip("일제사 간격(초). 판옥선은 화력이 세지만 재장전이 느리다.")]
    public float volleyInterval = 8f;
    [Tooltip("일제사 간격에 더해지는 무작위 시간(초).")]
    public Vector2 volleyJitter = new Vector2(0f, 3f);
    [Tooltip("일제사 안에서 각 대포가 순서대로 터지는 전체 시간(초). 우르르 쏘는 느낌.")]
    public float volleySpread = 1.2f;
    [Tooltip("한 번의 일제사에서 실제로 쏘는 최대 문 수. 14문을 다 쏘면 너무 과하다.")]
    public int maxGunsPerVolley = 8;
    [Tooltip("포탄 발사 앙각(도).")]
    public float firingAngle = 25f;
    [Tooltip("표적 주변으로 퍼지는 탄착 반경(m). 클수록 못 맞춘다.")]
    public float inaccuracy = 7f;
    [Tooltip("표적 쪽 현이 이 각도 이내로 정렬됐을 때만 발사한다(도).")]
    public float sideArc = 55f;

    [Header("상태 (읽기 전용)")]
    public int volleysFired;

    private NavyPanokAI _ai;
    private ShipFaction _self;
    private float _nextVolleyTime;

    void Start()
    {
        _ai = GetComponent<NavyPanokAI>();
        _self = GetComponent<ShipFaction>();

        if (portPoints == null || portPoints.Length == 0)
            portPoints = CollectShotPoints("CanonL");
        if (starboardPoints == null || starboardPoints.Length == 0)
            starboardPoints = CollectShotPoints("CanonR");
    }

    Transform[] CollectShotPoints(string groupName)
    {
        Transform group = transform.Find(groupName);
        if (group == null) return new Transform[0];

        var list = new List<Transform>();
        foreach (Transform cannon in group)
        {
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
        if (_ai.state != NavyPanokAI.State.Broadside) return;

        ShipFaction target = _ai.Target;
        if (target == null || !target.isActiveAndEnabled) return;
        if (_ai.distanceToTarget > fireRange) return;
        if (Time.time < _nextVolleyTime) return;

        Vector3 to = target.AimPosition - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        to.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        float side = Vector3.Dot(right, to);
        Vector3 sideDir = side >= 0f ? right : -right;
        if (Vector3.Angle(sideDir, to) > sideArc) return;   // 아직 현측 정렬이 안 됐다

        Transform[] points = side >= 0f ? starboardPoints : portPoints;
        if (points == null || points.Length == 0) return;

        StartCoroutine(VolleyRoutine(points, target));
        volleysFired++;
        _nextVolleyTime = Time.time + volleyInterval + Random.Range(volleyJitter.x, volleyJitter.y);
    }

    IEnumerator VolleyRoutine(Transform[] points, ShipFaction target)
    {
        int guns = maxGunsPerVolley > 0 ? Mathf.Min(maxGunsPerVolley, points.Length) : points.Length;
        // 문마다 조금씩 시차를 두어 선수에서 선미로 훑듯이 터지게 한다.
        float step = guns > 1 ? volleySpread / (guns - 1) : 0f;
        int start = points.Length > guns ? Random.Range(0, points.Length - guns + 1) : 0;

        for (int i = 0; i < guns; i++)
        {
            Transform p = points[start + i];
            if (p != null && target != null && target.isActiveAndEnabled)
                Fire(p, target);
            if (step > 0f) yield return new WaitForSeconds(step);
        }
    }

    void Fire(Transform firePoint, ShipFaction target)
    {
        Vector2 spread = Random.insideUnitCircle * inaccuracy;
        Vector3 aim = target.AimPosition + new Vector3(spread.x, 0f, spread.y);

        Vector3 dir = aim - firePoint.position;
        Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z);
        float horizontalDist = dirXZ.magnitude;
        float heightDiff = dir.y;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float angleRad = firingAngle * Mathf.Deg2Rad;

        float denom = 2f * (horizontalDist * Mathf.Tan(angleRad) - heightDiff) * Mathf.Pow(Mathf.Cos(angleRad), 2f);
        Vector3 launchVelocity;
        if (denom <= 0.001f)
        {
            // 해가 없으면(너무 가깝거나 높이차 문제) 직사로 쏜다.
            launchVelocity = (dirXZ.normalized + Vector3.up * 0.25f).normalized * 30f;
        }
        else
        {
            float velocity = Mathf.Sqrt(gravity * horizontalDist * horizontalDist / denom);
            Vector3 forward = dirXZ.normalized;
            Vector3 launchDir = Quaternion.LookRotation(forward) * Quaternion.Euler(-firingAngle, 0f, 0f) * Vector3.forward;
            launchVelocity = launchDir * velocity;
        }

        GameObject ball = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);

        Bullet bullet = ball.GetComponent<Bullet>();
        if (bullet != null)
        {
            // 진영을 찍어 보낸다. 이것으로 아군 오사가 걸러진다.
            bullet.ownerFaction = _self != null ? _self.faction : Faction.Navy;
            bullet.useFaction = true;
            bullet.shotMode = ShotMode.Enemy;   // 발사 직후 보정은 플레이어 전용이므로 Enemy 로 둔다
        }

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        ball.SetActive(true);
        if (rb != null)
        {
            rb.useGravity = true;
            rb.linearVelocity = launchVelocity;
        }
    }
}
