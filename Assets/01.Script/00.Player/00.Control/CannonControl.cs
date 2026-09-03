using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonControl : MonoBehaviour
{
    public bool shotMod;
    public bool sideMod;
    public float shotDelay;
    private int shotCount;
    public float maxView;
    public Camera mainCamera;
    public Transform target;               // Ÿ�� Transform
    public GameObject cannonBallPrefab;    // ��ź ������
    public float firingAngle = 60f;        // ���� �߻簢 (��)
    public ShotPos[] shotPos;
    public bool onTarget;
    public float initialSpeed = 20f; // �ӵ��� ���� ����
    [Header("좌우 판단")]
    [Tooltip("기준이 되는 선체. 비우면 이 오브젝트를 쓴다.")]
    public Transform shipTransform;
    [Tooltip("좌현(왼쪽) 발사 지점의 shotPos 인덱스.")]
    public int portShotIndex = 0;
    [Tooltip("우현(오른쪽) 발사 지점의 shotPos 인덱스.")]
    public int starboardShotIndex = 1;
    [Tooltip("선체 정면/후면을 겨눌 때 좌우가 깜빡이는 것을 막는 여유 구간. 이 안에서는 직전 방향을 유지한다.")]
    [Range(0f, 0.5f)]
    public float sideDeadzone = 0.08f;
    private bool _hasSide = false;

    private void Start()
    {
        shotMod = false;

    }

    private void Update()
    {
        // 인스펙터에서 현재 어느 쪽을 겨누는지 보이도록 매 프레임 갱신한다.
        int sideIndex = GetFiringSideIndex();

        if (shotMod)
        {
            if (Input.GetMouseButtonDown(0))
            {
                StartCoroutine(ShotPlay(sideIndex));
            }
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            //FireTarget();
        }
        
    }
    /// <summary>
    /// 지금 겨누는 방향이 선체의 어느 쪽인지 판단해 발사 지점 인덱스를 돌려준다.
    /// 예전에는 카메라의 쿼터니언 y 성분(transform.rotation.y)만 봤는데,
    /// 그것은 각도가 아니라 sin(yaw/2) 이고 선체 기준도 아니어서
    /// 배가 180도 돌면 좌우가 뒤집혔다.
    /// 여기서는 선체의 오른쪽 축과 조준 방향을 내적해 판단하므로 선수 방향과 무관하게 항상 맞는다.
    /// </summary>
    public int GetFiringSideIndex()
    {
        Transform ship = shipTransform != null ? shipTransform : transform;

        Vector3 aim = mainCamera != null ? mainCamera.transform.forward : ship.forward;
        aim.y = 0f;
        if (aim.sqrMagnitude < 0.0001f) aim = ship.forward;
        aim.Normalize();

        Vector3 shipRight = ship.right;
        shipRight.y = 0f;
        shipRight.Normalize();

        // 내적이 음수면 조준 방향이 선체의 왼쪽 = 좌현
        float dot = Vector3.Dot(shipRight, aim);

        // 선체 축과 거의 나란하면 좌우가 원리적으로 없다.
        // 그대로 부호로 판단하면 정면을 겨눌 때 좌우가 매 프레임 뒤집히므로,
        // 여유 구간 안에서는 직전 방향을 유지한다.
        if (!_hasSide || Mathf.Abs(dot) >= sideDeadzone)
        {
            sideMod = dot < 0f;
            _hasSide = true;
        }

        return sideMod ? portShotIndex : starboardShotIndex;
    }

    /// <summary>
    /// 내보내는 포탄에 이 배의 진영을 찍는다.
    /// 진영이 셋(해적/관군/왜군)이 되면서 포탄이 누구 편인지를
    /// 스스로 들고 가야 한다. ShipFaction 이 없으면 해적으로 본다.
    /// </summary>
    void StampFaction(GameObject cannonBall)
    {
        if (cannonBall == null) return;
        Bullet bullet = cannonBall.GetComponent<Bullet>();
        if (bullet == null) return;

        bullet.shotMode = ShotMode.Player;
        bullet.useFaction = true;

        Transform ship = shipTransform != null ? shipTransform : transform;
        ShipFaction sf = ship.GetComponentInParent<ShipFaction>();
        bullet.ownerFaction = sf != null ? sf.faction : Faction.Pirate;
    }


    public void FireFront(Transform firePoint)
    {
        // ī�޶� ���� �߻� ����
        Vector3 launchDir = mainCamera.transform.forward;

        // ��ź ���� �� �߻�
        GameObject cannonBall = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
        StampFaction(cannonBall);
        cannonBall.SetActive(true);
        Rigidbody rb = cannonBall.GetComponent<Rigidbody>();
        rb.useGravity = true;

        // ������ �ӵ��� �߻� (���� �߻� + �߷� ����)
        rb.linearVelocity = launchDir.normalized * initialSpeed;
        //// ī�޶� ���� ��ǥ ���� ���
        //Vector3 targetPoint = mainCamera.transform.position + mainCamera.transform.forward * maxView;

        //// ���� ���
        //Vector3 dir = targetPoint - firePoint.position;

        //float gravity = Mathf.Abs(Physics.gravity.y);
        //float angleRad = firingAngle * Mathf.Deg2Rad;

        //Vector3 dirXZ = new Vector3(dir.x, 0, dir.z);
        //float horizontalDist = dirXZ.magnitude;
        //float heightDiff = dir.y;

        //float velocitySquared = (gravity * horizontalDist * horizontalDist) /
        //                        (2 * (horizontalDist * Mathf.Tan(angleRad) - heightDiff) * Mathf.Pow(Mathf.Cos(angleRad), 2));

        //if (velocitySquared <= 0)
        //{
        //    Debug.LogWarning("������ �� ���� ��ġ�Դϴ�. maxView �Ǵ� firingAngle ���� �����ϼ���.");
        //    return;
        //}

        //float velocity = Mathf.Sqrt(velocitySquared);

        //// �߻� ���� ��� (���� firingAngle��ŭ ����)
        //Vector3 forward = dirXZ.normalized;
        //Vector3 launchDir = Quaternion.LookRotation(forward) * Quaternion.Euler(-firingAngle, 0, 0) * Vector3.forward;

        //Vector3 launchVelocity = launchDir * velocity;

        //// ��ź ���� �� �߻�
        //GameObject cannonBall = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
        //cannonBall.SetActive(true);
        //Rigidbody rb = cannonBall.GetComponent<Rigidbody>();
        //rb.useGravity = true;
        //rb.velocity = launchVelocity;
    }
    public void FireTarget(Transform firePoint)
    {
        GameObject cannonBall = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
        StampFaction(cannonBall);
        Rigidbody rb = cannonBall.GetComponent<Rigidbody>();
        cannonBall.SetActive(true);
        Vector3 dir = target.position - firePoint.position;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float angleRad = firingAngle * Mathf.Deg2Rad;

        // ���� ����(XZ)
        Vector3 dirXZ = new Vector3(dir.x, 0, dir.z);
        float horizontalDist = dirXZ.magnitude;
        float heightDiff = dir.y;

        // �ӵ� ���
        float velocitySquared = (gravity * horizontalDist * horizontalDist) /
                                (2 * (horizontalDist * Mathf.Tan(angleRad) - heightDiff) * Mathf.Pow(Mathf.Cos(angleRad), 2));

        if (velocitySquared <= 0)
        {
            Debug.LogWarning("������ �� ���� ��ġ�Դϴ�. ������ �Ÿ� Ȯ���ϼ���.");
            return;
        }

        float velocity = Mathf.Sqrt(velocitySquared);

        // ������: �߻� ���� ���
        Vector3 forward = dirXZ.normalized;
        Vector3 launchDir = Quaternion.LookRotation(forward) * Quaternion.Euler(-firingAngle, 0, 0) * Vector3.forward;

        Vector3 launchVelocity = launchDir * velocity;

        rb.useGravity = true;
        rb.linearVelocity = launchVelocity; // �Ǵ� rb.AddForce(launchVelocity, ForceMode.VelocityChange);
    }
    IEnumerator ShotPlay(int idx)
    {
        for (int i = 0; i < shotPos[idx].shotPoint.Length; i++)
        {
            if (onTarget)
            {
                FireTarget(shotPos[idx].shotPoint[i]);
            }
            else
            {
                FireFront(shotPos[idx].shotPoint[i]);
            }
        }
        yield return new WaitForSeconds(3f);
        shotCount = 0;
    }

}

[System.Serializable]
public class ShotPos
{
    public Transform[] shotPoint;
}
