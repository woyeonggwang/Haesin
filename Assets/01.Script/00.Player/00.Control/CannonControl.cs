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
        // ī�޶� ���� �߻� ����
        Vector3 launchDir = mainCamera.transform.forward;

        // ��ź ���� �� �߻�
        GameObject cannonBall = Instantiate(cannonBallPrefab, firePoint.position, Quaternion.identity);
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
        cannonBall.GetComponent<Bullet>().shotMode = ShotMode.Player;
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
