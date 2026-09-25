using UnityEngine;

/// <summary>
/// 걸어 다니는 플레이어용 3인칭 궤도 카메라. 배 카메라(CameraFollow)와 같은 조작:
///   마우스를 움직이면 버튼 없이 바로 돈다, 커서 숨김, Esc 로 커서 보이기/숨기기.
/// 추가: 휠 줌, 벽·지형에 파묻히지 않게 SphereCast.
/// 이동 컨트롤러(OnFootController·CharacterTestController)는 Yaw 를 읽어 카메라 기준으로 걷는다.
/// 메인 씬에서는 하차(OnFootRoot) 때 켜지고, 전체화면 메뉴가 열리면 GameMenuPause 가 끈다(disableWhileOpen).
/// </summary>
public class PlayerOrbitCamera : MonoBehaviour
{
    [Tooltip("움직일 카메라. 비우면 Camera.main.")] public Transform cameraTransform;
    [Tooltip("바라볼 대상. 비우면 이 오브젝트.")] public Transform target;
    [Tooltip("대상 발 위치에서 시선 높이(m).")] public float height = 1.3f;
    public float distance = 3.5f;
    public float minDistance = 0.8f, maxDistance = 8f;
    [Tooltip("휠 한 칸당 거리(m). 0 이면 줌 끔.")] public float zoomStep = 0.3f;
    [Tooltip("마우스 감도(도/마우스 축 1). 배 카메라 CameraFollow.rotationSpeed 와 같은 뜻.")] public float sensitivity = 5f;
    public float minPitch = -20f, maxPitch = 65f;
    [Tooltip("벽·지형에 카메라가 파묻히지 않게 한다.")] public bool avoidObstacles = true;
    public float obstacleRadius = 0.25f;
    [Tooltip("장애물 검사에서 뺄 레이어(플레이어 자신 등).")] public LayerMask obstacleMask = ~0;
    [Tooltip("켤 때 커서를 숨긴다(배 카메라와 같음). Esc 로 토글.")] public bool hideCursor = true;

    public float Yaw { get { return _yaw; } }
    public float Pitch { get { return _pitch; } }
    float _yaw, _pitch = 12f;

    public void SetYaw(float yaw) { _yaw = yaw; }

    void OnEnable()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (target == null) target = transform;
        _yaw = target.eulerAngles.y;
        if (hideCursor) Cursor.visible = false;
    }

    void Update()
    {
        if (hideCursor && Input.GetKeyDown(KeyCode.Escape)) Cursor.visible = !Cursor.visible;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) { if (Camera.main != null) cameraTransform = Camera.main.transform; else return; }
        if (target == null) target = transform;

        _yaw += Input.GetAxis("Mouse X") * sensitivity;
        _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * sensitivity, minPitch, maxPitch);
        if (zoomStep > 0f) distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * zoomStep, minDistance, maxDistance);

        Vector3 focus = target.position + Vector3.up * height;
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 dir = -(rot * Vector3.forward);
        float dist = distance;
        RaycastHit hit;
        if (avoidObstacles && Physics.SphereCast(focus, obstacleRadius, dir, out hit, distance, obstacleMask, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(target.root))
            dist = Mathf.Max(0.5f, hit.distance - 0.1f);
        cameraTransform.SetPositionAndRotation(focus + dir * dist, rot);
    }
}

/// <summary>걷는 플레이어 이동 컨트롤러 공통 창구(전투 스크립트가 이동을 잠그고 속도를 읽는다).</summary>
public interface ICharacterMover
{
    /// <summary>전투 행동 중 이동 잠금.</summary>
    bool LockMovement { get; set; }
    float CurrentSpeed { get; }
    float RunSpeed { get; }
    /// <summary>실제로 움직이는 오브젝트(CharacterController 가 있으면 그것).</summary>
    Transform MotionRoot { get; }
}
