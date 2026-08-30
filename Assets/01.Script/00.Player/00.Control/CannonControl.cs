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
    public Transform target;               // 타겟 Transform
    public GameObject cannonBallPrefab;    // 포탄 프리팹
    public float firingAngle = 60f;        // 고정 발사각 (도)
    public ShotPos[] shotPos;
    public bool onTarget;
    public float initialSpeed = 20f; // 속도를 수정 설정
    private void Start()
    {
        shotMod = false;

    }

    private void Update()
    {
        if (shotMod)
        {
            if (sideMod)
            {
                if(Input.GetMouseButtonDown(0))
                {
                    StartCoroutine(ShotPlay(0));
                }
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    StartCoroutine(ShotPlay(1));
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            //FireTarget();
        }
        
    }
    public void FireFront(Transform firePoint)
    {
        // 카메라 기준 발사 방향
        Vector3 launchDir = mainCamera.transform.forward;

        // 포탄 생성 및 발사
        GameObject cannonBall = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
        cannonBall.SetActive(true);
        Rigidbody rb = cannonBall.GetComponent<Rigidbody>();
        rb.useGravity = true;

        // 일정한 속도로 발사 (직선 발사 + 중력 적용)
        rb.velocity = launchDir.normalized * initialSpeed;
        //// 카메라 기준 목표 지점 계산
        //Vector3 targetPoint = mainCamera.transform.position + mainCamera.transform.forward * maxView;

        //// 방향 계산
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
        //    Debug.LogWarning("도달할 수 없는 위치입니다. maxView 또는 firingAngle 값을 조절하세요.");
        //    return;
        //}

        //float velocity = Mathf.Sqrt(velocitySquared);

        //// 발사 방향 계산 (위로 firingAngle만큼 꺾기)
        //Vector3 forward = dirXZ.normalized;
        //Vector3 launchDir = Quaternion.LookRotation(forward) * Quaternion.Euler(-firingAngle, 0, 0) * Vector3.forward;

        //Vector3 launchVelocity = launchDir * velocity;

        //// 포탄 생성 및 발사
        //GameObject cannonBall = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
        //cannonBall.SetActive(true);
        //Rigidbody rb = cannonBall.GetComponent<Rigidbody>();
        //rb.useGravity = true;
        //rb.velocity = launchVelocity;
    }
    public void FireTarget(Transform firePoint)
    {
        GameObject cannonBall = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
        cannonBall.GetComponent<Bullet>().shotMode = ShotMode.Player;
        Rigidbody rb = cannonBall.GetComponent<Rigidbody>();
        cannonBall.SetActive(true);
        Vector3 dir = target.position - firePoint.position;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float angleRad = firingAngle * Mathf.Deg2Rad;

        // 수평 방향(XZ)
        Vector3 dirXZ = new Vector3(dir.x, 0, dir.z);
        float horizontalDist = dirXZ.magnitude;
        float heightDiff = dir.y;

        // 속도 계산
        float velocitySquared = (gravity * horizontalDist * horizontalDist) /
                                (2 * (horizontalDist * Mathf.Tan(angleRad) - heightDiff) * Mathf.Pow(Mathf.Cos(angleRad), 2));

        if (velocitySquared <= 0)
        {
            Debug.LogWarning("도달할 수 없는 위치입니다. 각도나 거리 확인하세요.");
            return;
        }

        float velocity = Mathf.Sqrt(velocitySquared);

        // 수정된: 발사 벡터 계산
        Vector3 forward = dirXZ.normalized;
        Vector3 launchDir = Quaternion.LookRotation(forward) * Quaternion.Euler(-firingAngle, 0, 0) * Vector3.forward;

        Vector3 launchVelocity = launchDir * velocity;

        rb.useGravity = true;
        rb.velocity = launchVelocity; // 또는 rb.AddForce(launchVelocity, ForceMode.VelocityChange);
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
