using UnityEngine;

/// <summary>
/// 플레이어 칼싸움 입력 (레거시 Input).
///   마우스 왼쪽: 콤보(후딜 중 다시 누르면 다음 타 — 기본 4타·두 번째 5타가 자동으로 섞임) / 좌클릭 꾹(heavyHoldTime): 강베기(후딜 중 다시 꾹 → 강베기 2)
///   Shift 달리기 중 좌클릭: 달려 베기 / 회피 후반·직후 좌클릭: 회피 베기 / 공격 도중 다른 공격 키: 그 공격으로 이어 바꾸기
///   마우스 오른쪽: 누르면 튕겨내기 시도, 누른 채 막기
///   Space: 이동 입력이 있으면 그 방향으로 회피, 없으면 점프(베어 넘기기 회피)
/// 칼 뽑기/넣기(자동): 전투가 시작되면(살아 있는 적이 combatRange 안) 또는 칼을 넣은 채 좌클릭·우클릭·E·Q 를 누르면 뽑는다.
///   전투가 아니고 공격·막기 입력·판정 이벤트가 autoSheatheDelay 초 동안 없으면 대기 중에 칼을 넣는다. (drawKey 를 정하면 손으로도 뽑고 넣을 수 있음, 기본 없음)
/// 가장 가까운 다른 팀 SwordCombatant 를 자동 표적으로 잡고, 행동 중에는 이동 컨트롤러(ICharacterMover — 테스트 씬 CharacterTestController, 메인 씬 OnFootController)의 이동을 잠근다.
/// </summary>
[RequireComponent(typeof(SwordCombatant))]
public class PlayerCombatController : MonoBehaviour
{
    [Tooltip("표적 자동 탐색 거리(m).")] public float targetRange = 12f;
    [Tooltip("이동 컨트롤러(ICharacterMover: CharacterTestController 또는 OnFootController). 비우면 자신·부모에서 찾는다.")] public MonoBehaviour mover;
    ICharacterMover _moverCache; Transform _moverParent; bool _moverResolved;
    /// <summary>이동 컨트롤러. 부모가 바뀌면(메인 씬 승·하차로 외형이 OnFootRoot 아래로 옮겨짐) 다시 찾는다.</summary>
    ICharacterMover _mover
    {
        get
        {
            var explicitMover = mover as ICharacterMover;
            if (explicitMover != null && mover != null && mover.isActiveAndEnabled) return explicitMover;
            if (!_moverResolved || transform.parent != _moverParent)
            {
                _moverResolved = true; _moverParent = transform.parent;
                _moverCache = null;
                foreach (var m in GetComponentsInParent<ICharacterMover>())
                {
                    var mb = m as MonoBehaviour;
                    if (mb != null && mb.isActiveAndEnabled) { _moverCache = m; break; }
                }
            }
            return _moverCache;
        }
    }
    public SwordCombatant combatant;
    public WeaponHolder weapon;
    [Tooltip("칼 뽑기/넣기 수동 키(None = 없음, 자동만).")] public KeyCode drawKey = KeyCode.None;

    [Header("자동 뽑기/넣기")]
    [Tooltip("전투가 시작되면 칼을 뽑는다.")] public bool autoDraw = true;
    [Tooltip("전투가 끝나고 입력이 없으면 칼을 넣는다.")] public bool autoSheathe = true;
    [Tooltip("이 거리(m) 안에 살아 있는 적이 있으면 전투 중.")] public float combatRange = 6f;
    [Tooltip("전투가 아니고 공격·막기 입력이 없으면 이 시간(초) 뒤 칼을 넣는다.")] public float autoSheatheDelay = 5f;
    [Tooltip("읽기 전용: 전투 중인지.")] public bool inCombat;
    float _lastActivity;
    [Tooltip("두 번째 콤보 직접 키(None = 없음 — 좌클릭 콤보가 자동으로 섞임, SwordCombatant.autoMixCombos).")] public KeyCode comboBKey = KeyCode.None;
    [Tooltip("강베기 직접 키(None = 없음 — 좌클릭 꾹 누르기로 강베기).")] public KeyCode heavyKey = KeyCode.None;
    [Tooltip("좌클릭을 이 시간(초) 이상 누르고 있으면 강베기. 이보다 짧게 눌렀다 떼면 콤보.")] public float heavyHoldTime = 0.35f;
    float _lmbDownTime = -1f; bool _heavyFired;
    [Tooltip("이 비율(달리기 속도 대비) 이상으로 달리는 중 좌클릭이면 달려 베기.")] [Range(0f, 1f)] public float sprintAttackSpeedRatio = 0.75f;


    void Awake()
    {
        combatant = GetComponent<SwordCombatant>();
        if (weapon == null) weapon = GetComponent<WeaponHolder>();
        _lastActivity = Time.time;
    }

    void OnEnable() { if (combatant != null) combatant.OnCombatEvent += OnCombatEvent; }
    void OnDisable() { if (combatant != null) combatant.OnCombatEvent -= OnCombatEvent; }
    void OnCombatEvent(SwordCombatant who, string what) { _lastActivity = Time.time; }

    /// <summary>전투 중: 살아 있는 표적이 combatRange 안.</summary>
    bool CheckCombat()
    {
        if (combatant.target == null || !combatant.target.gameObject.activeInHierarchy) return false;
        var other = combatant.target.GetComponent<SwordCombatant>();
        if (other == null || other.state == SwordCombatant.State.Dead) return false;
        return (combatant.target.position - transform.position).magnitude <= combatRange;
    }

    void AutoDrawSheathe(bool idle, bool attackInput)
    {
        if (weapon == null) return;
        inCombat = CheckCombat();
        if (inCombat || attackInput || combatant.state != SwordCombatant.State.Idle) _lastActivity = Time.time;
        if (weapon.IsBusy || !idle) return;
        if (autoDraw && inCombat && !weapon.IsDrawn) weapon.BeginDraw();
        else if (autoSheathe && weapon.IsDrawn && !inCombat && Time.time - _lastActivity >= autoSheatheDelay) weapon.BeginSheathe();
    }

    void Update()
    {
        if (combatant.target == null || !combatant.target.gameObject.activeInHierarchy || (combatant.target.position - transform.position).magnitude > targetRange * 1.5f
            || (combatant.target.GetComponent<SwordCombatant>() != null && combatant.target.GetComponent<SwordCombatant>().state == SwordCombatant.State.Dead)) FindTarget();

        // 칼 뽑기/넣기 (대기 중에만). 칼을 넣은 상태에서 공격·막기를 누르면 먼저 뽑는다.
        bool idle = combatant.state == SwordCombatant.State.Idle;
        bool attackInput = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetKeyDown(comboBKey) || Input.GetKeyDown(heavyKey);
        AutoDrawSheathe(idle, attackInput);
        if (weapon != null)
        {
            if (Input.GetKeyDown(drawKey) && idle) weapon.Toggle();
            if (!weapon.IsDrawn || weapon.IsBusy)
            {
                if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetKeyDown(comboBKey) || Input.GetKeyDown(heavyKey)) && idle) weapon.BeginDraw();
                combatant.SetBlock(false);
                _lmbDownTime = -1f; // 뽑는 동안 누른 좌클릭은 공격으로 치지 않는다
                if (_mover != null) _mover.LockMovement = combatant.LocksMovement;
                return;
            }
        }
        // 좌클릭: 짧게 눌렀다 떼면 콤보(달리는 중이면 달려 베기), heavyHoldTime 이상 누르고 있으면 그 순간 강베기(강베기 후딜 중 다시 꾹 → 강베기 2)
        if (Input.GetMouseButtonDown(0)) { _lmbDownTime = Time.time; _heavyFired = false; }
        if (_lmbDownTime >= 0f && Input.GetMouseButton(0) && !_heavyFired && Time.time - _lmbDownTime >= heavyHoldTime)
        {
            _heavyFired = true;
            combatant.TryHeavy();
        }
        if (Input.GetMouseButtonUp(0) && _lmbDownTime >= 0f)
        {
            if (!_heavyFired)
            {
                bool sprinting = _mover != null && idle && _mover.CurrentSpeed >= _mover.RunSpeed * sprintAttackSpeedRatio;
                if (!(sprinting && combatant.TrySprintAttack())) combatant.TryAttack();
            }
            _lmbDownTime = -1f;
        }
        if (Input.GetKeyDown(comboBKey)) combatant.TryComboB();
        if (Input.GetKeyDown(heavyKey)) combatant.TryHeavy();
        if (Input.GetMouseButtonDown(1)) combatant.PressBlock();
        combatant.SetBlock(Input.GetMouseButton(1));
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector2 mv = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) mv.y += 1f; if (Input.GetKey(KeyCode.S)) mv.y -= 1f;
            if (Input.GetKey(KeyCode.D)) mv.x += 1f; if (Input.GetKey(KeyCode.A)) mv.x -= 1f;
            if (mv.sqrMagnitude > 0.01f)
            {
                float yaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : transform.eulerAngles.y;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(mv.x, 0f, mv.y);
                combatant.TryDodge(dir);
            }
            else combatant.TryJump();
        }
        if (Input.GetKeyDown(KeyCode.T) && combatant.state == SwordCombatant.State.Dead) combatant.Revive();

        if (_mover != null) _mover.LockMovement = combatant.LocksMovement;
    }

    void FindTarget()
    {
        SwordCombatant best = null; float bestD = targetRange;
        foreach (var c in FindObjectsByType<SwordCombatant>(FindObjectsSortMode.None))
        {
            if (c == combatant || c.team == combatant.team || c.state == SwordCombatant.State.Dead) continue;
            float d = Vector3.Distance(c.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = c; }
        }
        combatant.target = best != null ? best.transform : null;
    }
}
