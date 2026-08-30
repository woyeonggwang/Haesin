using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;         // ���� ��� (Player)
    public Vector3 offset = new Vector3(0, 2, -5); // ī�޶� ��ġ ������
    public float rotationSpeed = 5f; // ȸ�� �ӵ�
    public CannonControl cannon;
    private float fovValue;
    private Camera cam;
    public float targetFov;
    private float fovLerpVal;
    private float currentFov;
    private RaycastHit hitInfo;
    float yaw = 0f;
    float pitch = 0f;
    private void Start()
    {
        Cursor.visible = false;
        cam = transform.GetChild(0).GetComponent<Camera>();
        targetFov = 60f;
        currentFov = 28f;
        fovLerpVal = 1f;
        fovValue = 60f;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.visible = !Cursor.visible;
        }
        // 좌우 판단은 CannonControl.GetFiringSideIndex() 가 선체 기준으로 처리한다.
        cam.fieldOfView = fovValue;
        if (fovLerpVal < 1f)
        {
            fovLerpVal += Time.deltaTime*10f;
        }
        else
        {
            fovLerpVal = 1f;
        }
        fovValue = Mathf.Lerp(currentFov, targetFov, fovLerpVal);
        if (Input.GetMouseButtonDown(1))
        {
            fovLerpVal = 0f;
            targetFov = 28f;
            currentFov = 60f;
            cannon.shotMod = true;
            Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

            // RaycastHit�� �浹�� ������ ����
            if (Physics.Raycast(ray, out hitInfo))
            {
                // �浹�� ������Ʈ ���
                Debug.Log("�浹�� ������Ʈ: " + hitInfo.collider.name);

                // �浹�� �ݶ��̴� ��ü ��������
                Collider collided = hitInfo.collider;
                cannon.target=collided.transform;
                // ���ϴ� �۾� ���� ����
                // ��: collided.GetComponent<YourComponent>() ��
            }
            else
            {
                Debug.Log("�浹�� ������Ʈ�� �����ϴ�.");
            }
        }
        if (Input.GetMouseButtonUp(1))
        {
            fovLerpVal = 0f;
            targetFov = 60f;
            currentFov = 28f;
            cannon.shotMod = false;
        }
    }
    void LateUpdate()
    {
        if (target == null) return;

        // ���콺 �Է����� ȸ���� ����
        yaw += Input.GetAxis("Mouse X") * rotationSpeed;
        pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
        pitch = Mathf.Clamp(pitch, -30f, 60f); // ���Ʒ� ����

        // ȸ�� ����
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;

        transform.position = desiredPosition;
        transform.LookAt(target);
    }
}
