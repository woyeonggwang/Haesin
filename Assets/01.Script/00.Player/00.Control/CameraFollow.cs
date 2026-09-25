using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;         // ���� ��� (Player)
    public Vector3 offset = new Vector3(0, 2, -5); // ī�޶� ��ġ ������
    public float rotationSpeed = 5f; // ȸ�� �ӵ�
    public CannonControl cannon;
    [Tooltip("카메라가 해수면 아래로 내려가지 않게 하는 최저 높이(m, 월드 y). 배가 침몰할 때 바다 밑이 보이지 않게 한다.")]
    public float minHeightAboveSea = 3f;
    private ShipHealth _health; // 침몰 중인지 보기 위해
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
        if (target != null) _health = target.GetComponentInParent<ShipHealth>();
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

        // 침몰 중에는 배가 해수면 아래로 내려가므로 카메라 높이를 해수면 위로 고정하고,
        // 시선도 해수면 근처(가라앉는 자리)로 잡아 바다 밑이 보이지 않게 한다.
        // 평소에는 시야를 자유롭게 내릴 수 있어야 하므로(원거리 조준) 클램프는 침몰 중에만 건다.
        bool sinking = _health != null && _health.isSinking;
        if (sinking && desiredPosition.y < minHeightAboveSea) desiredPosition.y = minHeightAboveSea;
        transform.position = desiredPosition;
        Vector3 lookAt = target.position;
        if (sinking && lookAt.y < 0f) lookAt.y = 0f;
        transform.LookAt(lookAt);
    }
}
