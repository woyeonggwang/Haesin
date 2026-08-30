using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;         // 따라갈 대상 (Player)
    public Vector3 offset = new Vector3(0, 2, -5); // 카메라 위치 오프셋
    public float rotationSpeed = 5f; // 회전 속도
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
        cannon.sideMod = transform.rotation.y < 0;
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

            // RaycastHit에 충돌한 정보를 저장
            if (Physics.Raycast(ray, out hitInfo))
            {
                // 충돌한 오브젝트 출력
                Debug.Log("충돌한 오브젝트: " + hitInfo.collider.name);

                // 충돌한 콜라이더 객체 가져오기
                Collider collided = hitInfo.collider;
                cannon.target=collided.transform;
                // 원하는 작업 수행 가능
                // 예: collided.GetComponent<YourComponent>() 등
            }
            else
            {
                Debug.Log("충돌한 오브젝트가 없습니다.");
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

        // 마우스 입력으로 회전값 조정
        yaw += Input.GetAxis("Mouse X") * rotationSpeed;
        pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
        pitch = Mathf.Clamp(pitch, -30f, 60f); // 위아래 제한

        // 회전 적용
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 desiredPosition = target.position + rotation * offset;

        transform.position = desiredPosition;
        transform.LookAt(target);
    }
}
