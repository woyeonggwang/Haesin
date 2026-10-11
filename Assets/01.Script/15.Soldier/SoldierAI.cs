using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 육상 병사 한 명(관군·일본 병사·일본 장수, 2026-10-06 사용자 지시 "이들과도 싸울 수 있게").
/// 구조: 이 오브젝트(뿌리 — NavMeshAgent·캡슐 충돌체·이 스크립트) 아래 자식 모델(Animator·SwordCombatant·CombatStats·WeaponHolder·HwandoGripIK·SwordAudio).
///   SwordCombatant.motionRoot = 이 뿌리 → 공격·피격 루트 모션은 뿌리를 옮기고, 무릎꿇기·쓰러짐 높이는 모델만 내린다.
/// 평소(calm): 경비(patrolRadius 0 — 제자리에서 정해진 방향을 봄) 또는 순찰(제자리 둘레 patrolRadius 안 NavMesh 점을 걸어 다님). 칼은 허리에.
///   플레이어 자동 칼 뽑기·전투 판정에서 빠짐(SwordCombatant.calm). 사용자 선택 "먼저 공격할 때만":
///   이 병사가 공격받으면(맞음·막음·피함 등) 또는 근처에서 플레이어가 마을 사람을 쓰러뜨리면 SoldierGroup.Alert 로 주변 같은 무리 병사가 덤빈다.
/// 덤빔(hostile): 칼을 뽑고 플레이어에게 달려가 EnemySwordAI 와 같은 방식으로 싸운다(콤보·강베기·위험 공격, 막기·튕겨내기 반응).
///   한 번에 maxAttackers(2) 명만 붙어 싸우고 나머지는 holdDistance 에서 둘러싼다.
///   플레이어가 죽거나 배에 타거나 제자리에서 leashDistance 밖으로 가면 포기하고 제자리로 돌아가 체력을 채운 뒤 다시 calm.
/// 이동: NavMeshAgent(updatePosition·updateRotation 끔) 의 다음 위치를 LateUpdate 에서 뿌리에 옮기고, 루트 모션으로 움직인 뒤에는 거꾸로 에이전트를 맞춘다.
///   높이는 아래로 쏜 레이(자기·다른 병사 충돌체는 Ignore Raycast 층, CharacterController 는 건너뜀)로 땅에 붙인다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SoldierAI : MonoBehaviour
{
    public enum Mode { Calm, Hostile, Returning }

    [Header("이름·무리")]
    [Tooltip("HUD 에 쓰는 이름.")] public string displayName = "병사";
    public SoldierGroup group;
    public SwordCombatant combatant;
    public WeaponHolder weapon;

    [Header("평소")]
    [Tooltip("0 = 제자리 경비, 0 보다 크면 제자리 둘레 이 반지름(m) 안을 순찰.")] public float patrolRadius = 0f;
    [Tooltip("순찰 중 서 있는 시간(초, 최소~최대).")] public Vector2 patrolWait = new Vector2(3f, 9f);
    [Tooltip("평소 걷는 속도(m/s).")] public float calmWalkSpeed = 1.5f;
    [Tooltip("걷기 클립을 1 배속으로 틀 때의 몸 속도(m/s) — rig_Walk_no_weapon_IPC.")] public float walkClipSpeed = 2.69f;
    [Tooltip("0 보다 크면 플레이어가 걸어서 이 거리(m) 안에 들어오면 먼저 덤빈다(왜구 — 2026-10-07). 0 = 먼저 공격받을 때만(관군·일본군, 사용자 선택).")]
    public float aggroOnSightRange = 0f;
    [Tooltip("퀘스트가 숨긴 병사(2026-10-09) — 무리(SoldierGroup)가 거리로 켜지 않고, 알림·되살리기에서도 빠짐. QuestManager 의 SetTagActive 가 정함.")]
    public bool questHidden;
    [Tooltip("퀘스트 상대(두목·영주 등) — 쓰러져도 무리가 되살리지 않음.")]
    public bool questNoRespawn;

    [Header("싸움")]
    public float chaseSpeed = 3.3f;
    public float approachSpeed = 2.69f;
    [Tooltip("이 거리(m) 안이면 멈춰 공격.")] public float engageDistance = 1.7f;
    [Tooltip("공격 차례가 아닐 때 둘러서는 거리(m).")] public float holdDistance = 3.6f;
    [Tooltip("플레이어 하나에 동시에 붙어 싸우는 병사 수.")] public int maxAttackers = 2;
    [Tooltip("플레이어가 제자리에서 이 거리(m) 밖으로 가면 포기.")] public float leashDistance = 60f;
    public float turnSpeed = 540f;
    [Tooltip("공격 사이 대기(초).")] public Vector2 attackInterval = new Vector2(1.4f, 2.8f);
    [Range(0f, 1f)] public float thrustChance = 0.12f;
    [Range(0f, 1f)] public float sweepChance = 0.12f;
    [Range(0f, 1f)] public float heavyChance = 0.12f;
    [Range(0f, 1f)] public float blockChance = 0.3f;
    [Range(0f, 1f)] public float parryChance = 0.1f;
    [Range(0f, 1f)] public float comboBChance = 0.4f;
    [Range(0f, 1f)] public float comboContinueChance = 0.5f;
    [Range(0f, 1f)] public float heavyFollowChance = 0.4f;

    [Header("읽기 전용")]
    public Mode mode = Mode.Calm;

    /// <summary>덤비는 병사가 공격을 시작함(HUD 위험 표시용).</summary>
    public static System.Action<SoldierAI, AttackData> OnSoldierAttackStarted;
    /// <summary>덤비고 있는 병사(공격 차례 계산·HUD).</summary>
    public static readonly List<SoldierAI> Hostiles = new List<SoldierAI>();

    public bool IsHostile { get { return mode == Mode.Hostile; } }
    public bool IsDead { get { return combatant != null && combatant.state == SwordCombatant.State.Dead; } }
    public Vector3 Post { get { return _post; } }

    NavMeshAgent _agent; Animator _anim; Collider _col;
    Vector3 _post; Quaternion _postRot; bool _postSet;
    bool _moving; float _nextRepath; float _waitUntil; float _speedParam; Vector3 _lastPos; float _measuredSpeed;
    float _nextAttackTime; float _blockUntil = -1f; float _parryAt = -1f; int _comboLeft;
    SwordCombatant _player; SwordCombatant _hookedPlayer;
    readonly RaycastHit[] _hits = new RaycastHit[8];
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int LocoHash = Animator.StringToHash("Locomotion");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Hostiles.Clear(); OnSoldierAttackStarted = null; }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.updatePosition = false;
        _agent.updateRotation = false;
        _col = GetComponent<Collider>();
        if (combatant == null) combatant = GetComponentInChildren<SwordCombatant>(true);
        if (weapon == null && combatant != null) weapon = combatant.GetComponent<WeaponHolder>();
        if (combatant != null) _anim = combatant.GetComponent<Animator>();
        if (group == null) group = GetComponentInParent<SoldierGroup>();
        if (_anim != null) _anim.keepAnimatorStateOnDisable = true; // 멀어져 꺼졌다 켜져도 쓰러진 자세 등을 그대로
        _post = transform.position; _postRot = transform.rotation; _postSet = true;
    }

    void Start()
    {
        if (combatant != null)
        {
            combatant.calm = true;
            combatant.motionRoot = transform;
            combatant.OnCombatEvent += OnMyEvent;
            combatant.OnAttackStarted += OnMyAttack;
            combatant.OnDeath += OnMyDeath;
        }
        _lastPos = transform.position;
        _waitUntil = Time.time + Random.Range(0f, patrolWait.y);
    }

    void OnDestroy()
    {
        if (combatant != null) { combatant.OnCombatEvent -= OnMyEvent; combatant.OnAttackStarted -= OnMyAttack; combatant.OnDeath -= OnMyDeath; }
        Unhook();
        Hostiles.Remove(this);
    }

    void OnEnable()
    {
        if (_agent != null && _agent.isOnNavMesh) _agent.nextPosition = transform.position;
    }

    void OnDisable() { _moving = false; }

    // ---------------- 이벤트 ----------------

    void OnMyEvent(SwordCombatant who, string what)
    {
        if (who != combatant || IsDead) return;
        // 공격을 받았을 때 나오는 판정 결과들
        bool attacked = what == "피격" || what == "막기" || what == "회피" || what == "점프 회피" || what == "위험 공격 피격"
            || what.StartsWith("튕겨내기") || what == "결정타" || what == "체간 붕괴!";
        if (!attacked || mode == Mode.Hostile) return;
        if (group != null) group.Alert(transform.position); else BecomeHostile();
        BecomeHostile();
    }

    void OnMyAttack(SwordCombatant who, AttackData a)
    {
        if (OnSoldierAttackStarted != null) OnSoldierAttackStarted(this, a);
    }

    void OnMyDeath(SwordCombatant who)
    {
        _moving = false;
        if (_agent != null && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
        if (_col != null) _col.enabled = false;   // 시체에 걸리지 않게
        if (_anim != null) _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Unhook();
        Hostiles.Remove(this);
        if (group != null) group.Alert(transform.position); // 동료가 쓰러지면 주변이 덤빔
    }

    /// <summary>플레이어에게 덤빈다.</summary>
    public void BecomeHostile()
    {
        if (IsDead || mode == Mode.Hostile) return;
        mode = Mode.Hostile;
        if (combatant != null) combatant.calm = false;
        if (!Hostiles.Contains(this)) Hostiles.Add(this);
        if (_anim != null) _anim.speed = 1f;
        _nextAttackTime = Time.time + Random.Range(0.6f, 1.4f);
        gameObject.SetActive(true);
    }

    void GiveUp()
    {
        mode = Mode.Returning;
        if (combatant != null) { combatant.calm = true; combatant.SetBlock(false); combatant.target = null; }
        Hostiles.Remove(this);
        Unhook();
        _comboLeft = 0; _blockUntil = -1f; _parryAt = -1f;
    }

    /// <summary>쓰러진 병사를 제자리에 되살림(SoldierGroup — 카메라가 멀 때).</summary>
    public void Respawn()
    {
        if (combatant == null) return;
        if (IsDead) combatant.Revive(); else combatant.stats.ResetAll();
        mode = Mode.Calm; combatant.calm = true; combatant.target = null;
        Hostiles.Remove(this);
        Unhook();
        transform.SetPositionAndRotation(_post, _postRot);
        if (_agent != null && _agent.isActiveAndEnabled) { _agent.Warp(_post); if (_agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); } }
        if (_col != null) _col.enabled = true;
        if (weapon != null) weapon.Sheathe();
        if (_anim != null) { _anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms; _anim.speed = 1f; _anim.Play(LocoHash, 0, Random.value); }
        _moving = false;
    }

    void Hook(SwordCombatant p)
    {
        if (_hookedPlayer == p) return;
        Unhook();
        _hookedPlayer = p;
        if (p != null) p.OnAttackStarted += OnPlayerAttack;
    }

    void Unhook()
    {
        if (_hookedPlayer != null) _hookedPlayer.OnAttackStarted -= OnPlayerAttack;
        _hookedPlayer = null;
    }

    /// <summary>플레이어가 나를 노려 공격을 시작하면 확률로 막기·튕겨내기·피하기(EnemySwordAI 와 같은 방식).</summary>
    void OnPlayerAttack(SwordCombatant who, AttackData a)
    {
        if (combatant == null || IsDead || mode != Mode.Hostile) return;
        if (who.target != combatant.transform) return;
        if (combatant.state == SwordCombatant.State.Broken || combatant.IsAttacking) return;
        if (weapon != null && (!weapon.IsDrawn || weapon.IsBusy)) return;
        if (a.IsPerilous)
        {
            if (Random.value < 0.4f) { if (a.kind == AttackKind.Sweep) combatant.TryJump(); else combatant.TryDodge(transform.right * (Random.value < 0.5f ? 1f : -1f)); }
            return;
        }
        float r = Random.value;
        if (r < parryChance) _parryAt = Time.time + Mathf.Max(0f, a.windup - 0.1f);
        else if (r < parryChance + blockChance) { combatant.PressBlock(); _blockUntil = Time.time + a.windup + a.active + 0.25f; }
    }

    // ---------------- 매 프레임 ----------------

    SwordCombatant FindPlayer()
    {
        if (_player != null) return _player;
        var pcc = FindFirstObjectByType<PlayerCombatController>();
        if (pcc != null) _player = pcc.combatant != null ? pcc.combatant : pcc.GetComponent<SwordCombatant>();
        return _player;
    }

    static bool PlayerAvailable(SwordCombatant p)
    {
        if (p == null || !p.isActiveAndEnabled || p.state == SwordCombatant.State.Dead) return false;
        var mover = p.GetComponentInParent<OnFootController>();
        return mover != null && mover.isActiveAndEnabled;   // 걸어 다니는 중(배에 탄 동안은 아님)
    }

    void Update()
    {
        if (combatant == null || _agent == null) return;
        if (IsDead) { SetSpeedParam(0f, false); _moving = false; return; }

        switch (mode)
        {
            case Mode.Hostile: TickHostile(); break;
            case Mode.Returning: TickReturning(); break;
            default: TickCalm(); break;
        }
    }

    void TickCalm()
    {
        if (aggroOnSightRange > 0f && Time.frameCount % 15 == 0)
        {
            var p = FindPlayer();
            if (PlayerAvailable(p) && Flat(p.transform.position - transform.position).sqrMagnitude < aggroOnSightRange * aggroOnSightRange)
            {
                if (group != null) group.Alert(transform.position);
                BecomeHostile();
                return;
            }
        }
        if (weapon != null && weapon.IsDrawn && !weapon.IsBusy && combatant.state == SwordCombatant.State.Idle) weapon.BeginSheathe();
        if (patrolRadius <= 0f)
        {
            // 경비: 제자리로, 다 오면 정해진 방향
            if (Flat(transform.position - _post).sqrMagnitude > 0.25f) MoveTo(_post, calmWalkSpeed);
            else { Stop(); TurnTowards(_postRot * Vector3.forward); }
            return;
        }
        // 순찰
        if (_moving)
        {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f) { Stop(); _waitUntil = Time.time + Random.Range(patrolWait.x, patrolWait.y); }
        }
        else if (Time.time >= _waitUntil)
        {
            Vector3 p;
            if (RandomPointNearPost(out p)) MoveTo(p, calmWalkSpeed, true);
            else _waitUntil = Time.time + 2f;
        }
    }

    void TickReturning()
    {
        if (weapon != null && weapon.IsDrawn && !weapon.IsBusy && combatant.state == SwordCombatant.State.Idle) weapon.BeginSheathe();
        if (Flat(transform.position - _post).sqrMagnitude > 1f) { MoveTo(_post, approachSpeed); return; }
        Stop();
        combatant.stats.ResetAll();
        mode = Mode.Calm;
    }

    void TickHostile()
    {
        var p = FindPlayer();
        if (!PlayerAvailable(p) || Flat(p.transform.position - _post).magnitude > leashDistance) { GiveUp(); return; }
        Hook(p);
        combatant.target = p.transform;

        // 예약된 튕겨내기 / 막기 풀기
        if (_parryAt > 0f && Time.time >= _parryAt) { _parryAt = -1f; combatant.PressBlock(); _blockUntil = Time.time + 0.35f; }
        if (_blockUntil > 0f && Time.time >= _blockUntil) { _blockUntil = -1f; combatant.SetBlock(false); }

        bool drawn = weapon == null || (weapon.IsDrawn && !weapon.IsBusy);
        if (weapon != null && !weapon.IsDrawn && !weapon.IsBusy && combatant.state == SwordCombatant.State.Idle) weapon.BeginDraw();

        if (combatant.LocksMovement)
        {
            Stop();
            _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + 0.45f);
            return;
        }

        Vector3 to = Flat(p.transform.position - transform.position);
        float dist = to.magnitude;
        bool slot = InAttackSlot(p);
        float want = slot ? engageDistance : holdDistance;

        // 플레이어가 무너졌으면 결정타
        if (slot && drawn && p.state == SwordCombatant.State.Broken && dist <= 2.2f) { Stop(); combatant.TryAttack(); return; }

        if (dist > want + 0.35f)
        {
            MoveTo(p.transform.position, dist > 7f ? chaseSpeed : approachSpeed);
            if (dist < 8f) TurnTowards(to);
            return;
        }
        Stop();
        TurnTowards(to);
        if (!slot || !drawn || combatant.state == SwordCombatant.State.Block) return;

        if (Time.time >= _nextAttackTime)
        {
            float r = Random.value;
            if (r < thrustChance) combatant.TryPerilous(AttackKind.Thrust);
            else if (r < thrustChance + sweepChance) combatant.TryPerilous(AttackKind.Sweep);
            else if (r < thrustChance + sweepChance + heavyChance && combatant.TryHeavy())
            {
                _comboLeft = Random.value < heavyFollowChance ? 1 : 0;
                StartCoroutine(ComboFollowUp(() => combatant.TryHeavy()));
            }
            else
            {
                var chain = (combatant.comboB != null && combatant.comboB.Length > 0 && Random.value < comboBChance) ? combatant.comboB : combatant.combo;
                if (combatant.TryChain(chain))
                {
                    _comboLeft = Random.value < comboContinueChance ? Random.Range(1, chain.Length) : 0;
                    StartCoroutine(ComboFollowUp(() => combatant.TryChain(chain)));
                }
            }
            _nextAttackTime = Time.time + Random.Range(attackInterval.x, attackInterval.y) + 0.8f;
        }
    }

    System.Collections.IEnumerator ComboFollowUp(System.Func<bool> next)
    {
        AttackData queuedOn = null;
        while (_comboLeft > 0 && combatant.IsAttacking)
        {
            var a = combatant.CurrentAttack;
            if (a != null && a != queuedOn && combatant.AttackPhaseTime >= a.windup + a.active)
            {
                queuedOn = a;
                if (next()) _comboLeft--; else _comboLeft = 0;
            }
            yield return null;
        }
    }

    /// <summary>플레이어에게 가장 가까운 덤비는 병사 maxAttackers 명 안에 드는지.</summary>
    bool InAttackSlot(SwordCombatant p)
    {
        float mine = Flat(p.transform.position - transform.position).sqrMagnitude;
        int closer = 0;
        for (int i = 0; i < Hostiles.Count; i++)
        {
            var h = Hostiles[i];
            if (h == null || h == this || h.IsDead || !h.isActiveAndEnabled) continue;
            if (Flat(p.transform.position - h.transform.position).sqrMagnitude < mine) closer++;
        }
        return closer < Mathf.Max(1, maxAttackers);
    }

    bool RandomPointNearPost(out Vector3 p)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 c = Random.insideUnitCircle * patrolRadius;
            Vector3 q = _post + new Vector3(c.x, 0f, c.y);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(q, out hit, 3f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                { p = hit.position; return true; }
            }
        }
        p = _post; return false;
    }

    void MoveTo(Vector3 dest, float speed, bool force = false)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.speed = speed;
        _agent.isStopped = false;
        if (force || Time.time >= _nextRepath || !_moving) { _agent.SetDestination(dest); _nextRepath = Time.time + 0.25f; }
        _moving = true;
    }

    void Stop()
    {
        if (_moving && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
        _moving = false;
    }

    void TurnTowards(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), turnSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (_agent == null || !_agent.isOnNavMesh) { _lastPos = transform.position; return; }
        Vector3 pos;
        if (_moving && !IsDead && (combatant == null || !combatant.LocksMovement))
        {
            pos = _agent.nextPosition;
            Vector3 v = Flat(_agent.velocity);
            if (mode != Mode.Hostile || Flat(combatant.target != null ? combatant.target.position - transform.position : Vector3.zero).magnitude >= 8f)
                if (v.sqrMagnitude > 0.04f) TurnTowards(v);
        }
        else
        {
            _agent.nextPosition = transform.position;   // 루트 모션으로 옮겨진 자리를 NavMesh 위로
            pos = _agent.nextPosition;
        }
        pos.y = GroundY(pos);
        transform.position = pos;

        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        float sp = Flat(transform.position - _lastPos).magnitude / dt;
        _measuredSpeed = Mathf.Lerp(_measuredSpeed, sp, Mathf.Clamp01(dt * 10f));
        _lastPos = transform.position;
        if (!IsDead) SetSpeedParam(_moving ? _measuredSpeed : 0f, mode == Mode.Hostile);
    }

    float GroundY(Vector3 p)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 1.2f, Vector3.down, _hits, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            if (_hits[i].collider is CharacterController) continue;
            if (_hits[i].point.y > best) best = _hits[i].point.y;
        }
        return best > float.NegativeInfinity ? best : p.y;
    }

    void SetSpeedParam(float v, bool fighting)
    {
        if (_anim == null) return;
        bool busy = weapon != null && weapon.IsBusy;   // 뽑고 넣는 동작은 제 속도로(WeaponHolder 시각표가 실제 시간)
        if (!fighting && v > 0.2f && !busy)
        {
            // 평소 걷기: 걷기 클립을 느리게 틀어 발이 미끄러지지 않게
            _speedParam = Mathf.MoveTowards(_speedParam, walkClipSpeed, Time.deltaTime * 8f);
            _anim.speed = Mathf.Clamp(v / walkClipSpeed, 0.45f, 1.3f);
        }
        else
        {
            _speedParam = Mathf.MoveTowards(_speedParam, v, Time.deltaTime * 10f);
            _anim.speed = 1f;
        }
        _anim.SetFloat(SpeedHash, _speedParam);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
