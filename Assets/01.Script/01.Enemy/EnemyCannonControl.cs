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

    [Header("상태 (읽기 전용)")]
    public int volleysFired;

    private EnemyAI _ai;
    private Transform _player;
    private float _nextVolleyTime;

    void Start()
    {
        _ai = GetComponent<EnemyAI>();

        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null) _player = target.transform;

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
        if (_player == null || _ai == null || cannonBallPrefab == null) return;
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
        // 탄착점: 플레이어 주변 무작위
        Vector2 spread = Random.insideUnitCircle * inaccuracy;
        Vector3 aim = _player.position + new Vector3(spread.x, 0f, spread.y);

        Vector3 dir = aim - firePoint.position;
        Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z);
        float horizontalDist = dirXZ.magnitude;
        float heightDiff = dir.y;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float angleRad = firingAngle * Mathf.Deg2Rad;

        // 포물선 탄도 해 (플레이어 FireTarget 과 같은 공식)
        float denom = 2f * (horizontalDist * Mathf.Tan(angleRad) - heightDiff) * Mathf.Pow(Mathf.Cos(angleRad), 2f);
        Vector3 launchVelocity;
        if (denom <= 0.001f)
        {
            // 해가 없으면(너무 가깝거나 높이차 문제) 직사로 쏜다
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
        if (bullet != null) bullet.shotMode = ShotMode.Enemy;
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        ball.SetActive(true);
        rb.useGravity = true;
        rb.linearVelocity = launchVelocity;
    }
}
