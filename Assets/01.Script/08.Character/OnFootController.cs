using UnityEngine;

/// <summary>
/// 하차 상태의 플레이어 이동 (레거시 Input). CharacterController 로 걷고(중력 있음), 카메라는 PlayerOrbitCamera 가 돌린다
/// (배 카메라와 같은 조작: 마우스만 움직이면 돈다).
/// 이 컴포넌트가 있는 오브젝트(OnFootRoot)가 실제로 움직이고, 캐릭터 외형(PlayerCharacter)은 그 자식이다.
/// 외형에 Animator 가 있으면 Speed 파라미터를 준다(대기/걷기/달리기 블렌드용).
/// ICharacterMover — 전투(PlayerCombatController·SwordCombatant)가 이동을 잠그고, 루트 모션은 이 CharacterController 로 옮긴다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class OnFootController : MonoBehaviour, ICharacterMover
{
    [Tooltip("걷기·달리기 속도(m/s). 이동 블렌드 트리(Loco_Root) 임계값과 같게 — 팩 클립 몸 이동 속도: 걷기 2.69, 질주 4.06.")]
    public float walkSpeed = 2.69f;
    public float runSpeed = 4.06f;
    public float acceleration = 8f;
    public float turnSpeed = 720f;
    public float gravity = -20f;
    [Header("카메라 (PlayerOrbitCamera 가 없으면 이 값으로 만든다)")]
    public Transform cameraTransform;
    public float cameraDistance = 3.2f;
    [Tooltip("발(피벗) 위 시선 높이. 예전 큐브용 1.4 → 모델(CharacterTest 와 같게) 1.1.")] public float cameraHeight = 1.1f;
    public float orbitSensitivity = 5f;
    public float minPitch = -15f, maxPitch = 60f;
    public PlayerOrbitCamera orbit;

    [Header("바닥")]
    [Tooltip("CharacterController 가 통과하는 레이어. 계단 디딤판(StepSurface)은 발 IK(FootIK)용이라 몸은 그 밑 터레인 경사를 매끈하게 걷는다(한 단씩 턱에 걸리지 않게).")]
    public string[] walkThroughLayers = { "StepSurface" };

    [Header("전투·테스트")]
    [Tooltip("전투 행동 중 이동 잠금(PlayerCombatController 가 매 프레임 갱신). 중력은 계속 받는다.")] public bool lockMovement;
    [Tooltip("자동 테스트용 입력 흉내.")] public Vector2 debugMove;
    public bool debugRun;

    CharacterController _cc; Animator _anim;
    float _speed, _vy;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    public float CurrentSpeed { get { return _speed; } }
    public float RunSpeed { get { return runSpeed; } }
    public bool LockMovement { get { return lockMovement; } set { lockMovement = value; } }
    public Transform MotionRoot { get { return transform; } }

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        int mask = 0;
        foreach (var n in walkThroughLayers) { int l = LayerMask.NameToLayer(n); if (l >= 0) mask |= 1 << l; }
        _cc.excludeLayers = _cc.excludeLayers | mask;
        EnsureOrbit();
    }

    void EnsureOrbit()
    {
        if (orbit == null) orbit = GetComponent<PlayerOrbitCamera>();
        if (orbit == null)
        {
            orbit = gameObject.AddComponent<PlayerOrbitCamera>();
            orbit.distance = cameraDistance; orbit.height = cameraHeight; orbit.sensitivity = orbitSensitivity;
            orbit.minPitch = minPitch; orbit.maxPitch = maxPitch;
        }
    }

    void OnEnable()
    {
        _anim = GetComponentInChildren<Animator>(); _vy = 0f;
        EnsureOrbit();
        if (cameraTransform != null) orbit.cameraTransform = cameraTransform;
        orbit.SetYaw(transform.eulerAngles.y);
    }

    public void SetYaw(float yaw) { EnsureOrbit(); orbit.SetYaw(yaw); }

    void Update()
    {
        if (cameraTransform != null && orbit.cameraTransform != cameraTransform) orbit.cameraTransform = cameraTransform;
        if (_anim == null) _anim = GetComponentInChildren<Animator>(); // 외형이 나중에 들어와도 찾음
        Vector2 move = lockMovement ? Vector2.zero : debugMove;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
        bool run = debugRun || Input.GetKey(KeyCode.LeftShift);
        if (lockMovement) { move = Vector2.zero; run = false; }

        Vector3 dir = Quaternion.Euler(0f, orbit.Yaw, 0f) * new Vector3(move.x, 0f, move.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        float target = dir.magnitude * (run ? runSpeed : walkSpeed);
        _speed = Mathf.MoveTowards(_speed, target, acceleration * Time.deltaTime);
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), turnSpeed * Time.deltaTime);

        if (_cc.isGrounded && _vy < 0f) _vy = -2f;
        _vy += gravity * Time.deltaTime;
        Vector3 vel = transform.forward * _speed + Vector3.up * _vy;
        _cc.Move(vel * Time.deltaTime);
        if (_anim != null) _anim.SetFloat(SpeedHash, _speed);
    }
}
