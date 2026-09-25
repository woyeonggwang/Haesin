using UnityEngine;

/// <summary>
/// [테스트용] 캐릭터를 직접 움직여 치마·포니테일 물리, 옷 교체, 칼싸움을 확인하는 컨트롤러.
/// 메인 씬에서는 같은 역할을 OnFootController(CharacterController + 중력)가 한다 — 둘 다 ICharacterMover 라서
/// 전투 스크립트(PlayerCombatController·SwordCombatant)는 어느 쪽이든 그대로 쓴다.
///
/// 조작
///  - W/A/S/D 또는 방향키: 이동 (카메라 기준)   - 왼쪽 Shift: 달리기
///  - 마우스 이동: 카메라 돌리기(배 카메라와 같음, 버튼 없이)   - 휠: 줌   - Esc: 커서 보이기/숨기기
///  - 1: 옷 입기/벗기   2: 신발 입기/벗기   3: 머리카락 입기/벗기   R: 처음 위치로
/// 카메라는 PlayerOrbitCamera(메인 씬 하차 카메라와 같은 컴포넌트)가 맡는다.
/// </summary>
public class CharacterTestController : MonoBehaviour, ICharacterMover
{
    public Animator animator;
    public Transform cameraTransform;

    [Header("이동")]
    public float walkSpeed = 0.85f;
    public float runSpeed = 2.6f;
    [Tooltip("속도 변화량 (m/s²)")] public float acceleration = 5f;
    [Tooltip("회전 속도 (도/초)")] public float turnSpeed = 540f;

    [Header("카메라 (처음 한 번 PlayerOrbitCamera 로 옮김)")]
    public PlayerOrbitCamera orbit;
    public float cameraDistance = 3.2f;
    public float cameraTargetHeight = 1.1f;
    public float orbitSensitivity = 5f;
    public float minPitch = -15f, maxPitch = 60f;

    [Header("자동 테스트용 (MCP 에서 입력 흉내)")]
    public Vector2 debugMove;
    public bool debugRun;
    [Tooltip("전투 행동(공격·회피 등) 중 이동 잠금. PlayerCombatController 가 매 프레임 갱신.")]
    public bool lockMovement;

    CharacterOutfit outfit;
    SpringBoneSystem spring;
    float speed;
    Vector3 startPos;
    Quaternion startRot;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    public float CurrentSpeed { get { return speed; } }
    public float RunSpeed { get { return runSpeed; } }
    public bool LockMovement { get { return lockMovement; } set { lockMovement = value; } }
    public Transform MotionRoot { get { return transform; } }

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        outfit = GetComponent<CharacterOutfit>();
        spring = GetComponent<SpringBoneSystem>();
        startPos = transform.position;
        startRot = transform.rotation;
        if (orbit == null) orbit = GetComponent<PlayerOrbitCamera>();
        if (orbit == null)
        {
            orbit = gameObject.AddComponent<PlayerOrbitCamera>();
            orbit.distance = cameraDistance; orbit.height = cameraTargetHeight; orbit.sensitivity = orbitSensitivity;
            orbit.minPitch = minPitch; orbit.maxPitch = maxPitch;
        }
        if (orbit.cameraTransform == null) orbit.cameraTransform = cameraTransform;
        // 처음엔 캐릭터 뒤에서 본다 (W = 캐릭터가 바라보는 쪽).
        orbit.SetYaw(transform.eulerAngles.y);
    }

    void Update()
    {
        Vector2 move = lockMovement ? Vector2.zero : debugMove;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
        bool run = debugRun || Input.GetKey(KeyCode.LeftShift);
        if (lockMovement) { move = Vector2.zero; run = false; }

        float yaw = orbit != null ? orbit.Yaw : transform.eulerAngles.y;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float target = dir.magnitude * (run ? runSpeed : walkSpeed);
        speed = Mathf.MoveTowards(speed, target, acceleration * Time.deltaTime);
        if (dir.sqrMagnitude > 0.0001f)
        {
            var want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        }
        transform.position += transform.forward * (speed * Time.deltaTime);
        if (animator != null) animator.SetFloat(SpeedHash, speed);

        if (outfit != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) outfit.Toggle(CharacterOutfit.Slot.Clothes);
            if (Input.GetKeyDown(KeyCode.Alpha2)) outfit.Toggle(CharacterOutfit.Slot.Shoes);
            if (Input.GetKeyDown(KeyCode.Alpha3)) outfit.Toggle(CharacterOutfit.Slot.Hair);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.SetPositionAndRotation(startPos, startRot);
            speed = 0f;
            if (orbit != null) orbit.SetYaw(startRot.eulerAngles.y);
            if (spring != null) spring.ResetTails();
        }
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 330, 118), GUIContent.none);
        GUI.Label(new Rect(20, 16, 320, 110),
            "[캐릭터 테스트]\n" +
            "WASD/방향키: 이동   Shift: 달리기\n" +
            "마우스: 카메라   휠: 줌   Esc: 커서\n" +
            "1: 옷  2: 신발  3: 머리카락 (입기/벗기)   R: 위치 초기화\n" +
            "속도 " + speed.ToString("F2") + " m/s");
    }
}
