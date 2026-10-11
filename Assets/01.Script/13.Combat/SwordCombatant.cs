using UnityEngine;

/// <summary>
/// 세키로식 칼싸움의 한 사람(플레이어·적 공용). 상태기계 + 판정.
///   공격: windup → active(시작 순간 판정 1회, 사거리·각도 안의 target) → recovery(콤보 입력 창).
///   막기: 버튼을 누른 채 → Block 상태. 공격이 닿기 직전 parryWindow(0.2 s) 안에 '누르면' 튕겨내기.
///   튕겨내기: 상대 체간 +parryPostureToAttacker(25), 자신 +parryPostureToSelf(5). 위험 공격(찌르기·베어 넘기기)은 튕겨낼 수 없다.
///   막기: 상대 공격의 blockPostureDamage 만큼 체간이 찬다. 위험 공격을 막으면 체력 피해를 그대로 받는다.
///   회피: dodgeInvuln 동안 무적. 점프: 베어 넘기기(Sweep)만 피한다.
///   체간이 다 차면 Broken(경직) → 이 동안 맞으면 결정타(즉사).
/// 애니메이션은 상태 이름으로 CrossFade (Attack1~3, Thrust, Sweep, Block, Parry, BlockHit, Hit, Dodge, Jump, Broken, Death, Locomotion).
/// 위치 이동(런지·회피)은 transform 직접 이동(테스트 씬은 평지).
/// </summary>
[RequireComponent(typeof(CombatStats))]
public class SwordCombatant : MonoBehaviour
{
    public enum State { Idle, Attack, Block, Dodge, Jump, HitStun, Broken, Dead }

    [Header("진영")]
    [Tooltip("같은 팀끼리는 맞지 않는다. 0 = 플레이어, 1 = 적.")] public int team = 0;
    public Transform target;
    [Tooltip("싸우지 않는 중(경비·순찰하는 병사 — SoldierAI 가 정함). 플레이어 자동 칼 뽑기·전투 판정에서 빠지고, 표적은 덤비는 적을 먼저 고름. 맞으면 덤빈다(2026-10-06).")]
    public bool calm;

    [Header("공격 데이터")]
    public AttackData[] combo = new AttackData[] {
        new AttackData { name = "내려베기", animState = "Attack1", windup = 0.28f, active = 0.15f, recovery = 0.32f, damage = 15f, postureDamage = 20f, blockPostureDamage = 10f },
        new AttackData { name = "가로베기", animState = "Attack2", windup = 0.22f, active = 0.15f, recovery = 0.30f, damage = 15f, postureDamage = 20f, blockPostureDamage = 10f },
        new AttackData { name = "올려베기", animState = "Attack3", windup = 0.32f, active = 0.15f, recovery = 0.45f, damage = 22f, postureDamage = 28f, blockPostureDamage = 14f } };
    public AttackData thrust = new AttackData { name = "찌르기", kind = AttackKind.Thrust, animState = "Thrust", windup = 0.45f, active = 0.12f, recovery = 0.55f, damage = 35f, postureDamage = 35f, blockPostureDamage = 30f, range = 2.4f, arc = 22f, lunge = 0.9f };
    public AttackData sweep = new AttackData { name = "베어 넘기기", kind = AttackKind.Sweep, animState = "Sweep", windup = 0.5f, active = 0.15f, recovery = 0.6f, damage = 30f, postureDamage = 40f, blockPostureDamage = 35f, range = 2.2f, arc = 100f, lunge = 0.3f };
    [Tooltip("두 번째 콤보(플레이어: E). 순서대로 이어진다. 다른 콤보 도중에 누르면 이 콤보 1타로 바뀐다.")] public AttackData[] comboB = new AttackData[0];
    [Tooltip("강공격(플레이어: Q). 일반 공격(막기·튕겨내기 가능)이지만 체간 피해가 크다.")] public AttackData heavy = new AttackData { name = "강베기", animState = "Heavy" };
    [Tooltip("강공격 후딜 중 다시 강공격 → 이어지는 두 번째 강공격.")] public AttackData heavyFollow = new AttackData { name = "강베기 2", animState = "Heavy2" };
    [Tooltip("달려 베기(플레이어: Shift 달리기 중 좌클릭).")] public AttackData sprintAttack = new AttackData { name = "달려 베기", animState = "SprintAttack" };
    [Tooltip("회피 베기: 회피 후반이나 회피 직후 기본 공격 입력.")] public AttackData evadeAttack = new AttackData { name = "회피 베기", animState = "EvadeAttack" };
    [Tooltip("좌클릭 한 버튼으로 두 콤보를 자동으로 섞는다(새 콤보를 시작할 때마다 고름).")] public bool autoMixCombos = true;
    [Tooltip("새 콤보를 두 번째 콤보(comboB)로 시작할 확률.")] [Range(0f, 1f)] public float comboBChance = 0.5f;
    [Tooltip("같은 콤보가 이 횟수보다 많이 연달아 나오지 않게 한다.")] public int maxSameComboInRow = 2;
    bool _lastPickB; int _sameComboCount;
    [Tooltip("회피 시작 후 이 시간(초)부터 회피 베기 입력을 받는다.")] public float evadeAttackFrom = 0.2f;
    [Tooltip("회피가 끝난 뒤 이 시간(초) 안의 기본 공격 입력도 회피 베기.")] public float evadeAttackGrace = 0.3f;

    [Header("반응 동작 (여럿이면 무작위, 애니메이터에 없는 이름은 건너뜀)")]
    public string[] hitStates = new string[] { "Hit" };
    public string[] blockHitStates = new string[] { "BlockHit" };
    public string[] deathStates = new string[] { "Death" };

    [Header("판정 시간")]
    [Tooltip("막기 버튼을 누른 순간부터 이 시간(초) 안에 공격이 닿으면 튕겨내기.")] public float parryWindow = 0.2f;
    public float parryPostureToAttacker = 25f;
    public float parryPostureToSelf = 5f;
    [Tooltip("피격 경직(초).")] public float hitStun = 0.35f;
    [Tooltip("콤보 다음 타를 입력할 수 있는 시간: 후딜 중 언제든. 후딜이 끝나면 콤보가 끊긴다.")] public bool comboInRecovery = true;
    [Tooltip("다음 타 입력을 미리 받아 두는(버퍼) 시작 시점 = windup × 이 값. 예약된 다음 타는 AttackData.ChainTime 에 시작한다(판정 전에 끊기지 않음).")] [Range(0f, 1f)] public float inputBufferFromWindup = 0.5f;

    [Header("회피 / 점프")]
    public float dodgeDuration = 0.45f;
    public float dodgeInvuln = 0.3f;
    public float dodgeDistance = 2.4f;
    public float jumpDuration = 0.6f;
    [Tooltip("점프 높이(m). 애니메이션 RootT.y 는 루트 모션이 꺼져 있어 적용되지 않으므로 스크립트가 루트를 올린다.")] public float jumpHeight = 0.9f;
    [Tooltip("체간 붕괴(무릎꿇기) 시 루트를 내리는 높이(m).")] public float kneelDrop = 0.35f;
    [Tooltip("사망(뒤로 쓰러짐) 시 루트를 내리는 높이(m).")] public float deathDrop = 0.55f;

    [Header("회전")]
    [Tooltip("공격 예비·막기 중 상대를 향해 도는 속도(도/초).")] public float faceTurnSpeed = 540f;
    [Tooltip("공격 예비 동작 중 이 비율까지만 상대를 추적한다(그 뒤엔 방향 고정 → 회피가 통한다).")] [Range(0f, 1f)] public float trackUntilWindupFraction = 0.55f;

    [Header("루트 모션 (ARPGSamurai 클립)")]
    [Tooltip("공격·뒤로 회피·피격·사망 동작의 이동을 애니메이션 루트 모션으로 한다(lunge 대신). 끄면 예전 스크립트 이동.")] public bool useRootMotion = true;
    [Tooltip("루트 모션(공격·피격·사망 등)으로 상대에게 이 거리(m)보다 가까이 다가가지 않는다.")] public float minTargetDistance = 0.9f;
    [Tooltip("실제로 옮기고 돌릴 오브젝트. 비우면 부모의 ICharacterMover(메인 씨 OnFootRoot) → 없으면 자기 자신. CharacterController 가 있으면 Move 로 옮긴다(벽·지형 충돌).")] public Transform motionRoot;
    [Tooltip("뒤로 회피 애니메이터 상태(루트 모션). 없으면 옆 회피와 같은 Dodge(스크립트 이동).")] public string dodgeBackState = "DodgeBack";
    [Tooltip("뒤로 회피 길이(초). rig_Dodge 0.70 s.")] public float dodgeBackDuration = 0.7f;

    [Header("읽기 전용")]
    public State state = State.Idle;
    public string currentAttackName;

    public Animator animator;
    public CombatStats stats;

    /// <summary>공격 시작(예비 동작 시작) 알림 — HUD 위험 표시, 적 AI 반응용.</summary>
    public System.Action<SwordCombatant, AttackData> OnAttackStarted;
    /// <summary>판정 결과 알림 (문자열: 튕겨내기/막기/피격/회피/결정타). (누가, 무엇을)</summary>
    public System.Action<SwordCombatant, string> OnCombatEvent;
    public System.Action<SwordCombatant> OnDeath;
    /// <summary>공격 판정 순간(active 시작) — 표적이 아닌 것(마을 사람 등)도 맞았는지 따로 보게(2026-10-05, Villager).</summary>
    public static System.Action<SwordCombatant, AttackData> OnStrike;

    // 내부
    AttackData _attack; AttackData[] _chain; int _comboIndex = -1; float _stateTime; bool _hitDone; bool _lungeDone;
    AttackData _queued; AttackData[] _queuedChain; int _queuedIndex = -1; float _lastDodgeEnd = -99f; float _hitStunDur = 0.35f;
    float _parryPressTime = -999f; bool _blockHeld;
    Vector3 _dodgeDir; float _brokenEndTime;
    bool _dead;
    bool _dodgeRoot; float _dodgeDur = 0.45f;
    float _groundY; float _yOffset;
    Transform _root; Transform _rootParent; bool _rootResolved; CharacterController _cc; float _baseLocalY;

    /// <summary>옮기고 돌릴 오브젝트. 부모가 바뀌면(메인 씨 승·하차로 외형이 옮겨짐) 다시 찾는다.</summary>
    Transform Root
    {
        get
        {
            if (!_rootResolved || transform.parent != _rootParent)
            {
                _rootResolved = true; _rootParent = transform.parent;
                if (motionRoot != null) _root = motionRoot;
                else
                {
                    _root = transform;
                    foreach (var m in GetComponentsInParent<ICharacterMover>())
                    {
                        var mb = m as MonoBehaviour;
                        if (mb != null && mb.isActiveAndEnabled) { _root = m.MotionRoot; break; }
                    }
                }
                _cc = _root.GetComponent<CharacterController>();
                if (_root != transform) _baseLocalY = transform.localPosition.y;
            }
            return _root;
        }
    }

    void MoveRoot(Vector3 d)
    {
        var r = Root;
        if (_cc != null && _cc.enabled && _cc.gameObject.activeInHierarchy) _cc.Move(d);
        else r.position += d;
    }

    void Start() { _groundY = transform.position.y; }

    void SetY(float offset)
    {
        _yOffset = offset;
        if (Root != transform)
        {
            // 부모(OnFootRoot)가 땅을 따라 움직이므로 외형만 위아래로
            var lp = transform.localPosition; lp.y = _baseLocalY + offset; transform.localPosition = lp;
            return;
        }
        var p = transform.position; p.y = _groundY + offset; transform.position = p;
    }

    public bool IsBusy { get { return state != State.Idle && state != State.Block; } }
    public bool LocksMovement { get { return state != State.Idle; } }
    public bool IsInvulnerable { get { return state == State.Dodge && _stateTime <= dodgeInvuln; } }
    public bool IsAttacking { get { return state == State.Attack; } }
    public AttackData CurrentAttack { get { return state == State.Attack ? _attack : null; } }
    /// <summary>다음 공격이 예약돼 있는지.</summary>
    public bool HasQueued { get { return _queued != null; } }
    public float AttackPhaseTime { get { return _stateTime; } }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        stats = GetComponent<CombatStats>();
        stats.OnPostureBroken += OnBroken;
        stats.OnDied += OnStatsDied;
        if (useRootMotion && animator != null) animator.applyRootMotion = true; // 실제 적용은 OnAnimatorMove 가 상태별로 한다
    }

    void OnDestroy()
    {
        if (stats != null) { stats.OnPostureBroken -= OnBroken; stats.OnDied -= OnStatsDied; }
    }

    void Update()
    {
        _stateTime += Time.deltaTime;
        if (_dead) { SetY(Mathf.Lerp(_yOffset, -deathDrop, Time.deltaTime * 3f)); return; }
        switch (state)
        {
            case State.Attack: TickAttack(); break;
            case State.Block: FaceTarget(); break;
            case State.Dodge:
                if (!_dodgeRoot) MoveRoot(_dodgeDir * (dodgeDistance / dodgeDuration) * Time.deltaTime);
                if (_stateTime >= _dodgeDur) { _lastDodgeEnd = Time.time; ToIdle(); }
                break;
            case State.Jump:
                { float t = Mathf.Clamp01(_stateTime / jumpDuration); SetY(jumpHeight * 4f * t * (1f - t)); }
                if (_stateTime >= jumpDuration) ToIdle();
                break;
            case State.HitStun: if (_stateTime >= _hitStunDur) ToIdle(); break;
            case State.Broken:
                SetY(Mathf.Lerp(_yOffset, -kneelDrop, Time.deltaTime * 8f));
                if (Time.time >= _brokenEndTime) { stats.EndBroken(); ToIdle(); }
                break;
            case State.Dead:
                SetY(Mathf.Lerp(_yOffset, -deathDrop, Time.deltaTime * 3f));
                break;
        }
    }

    // ---------------- 공격 ----------------

    /// <summary>
    /// 공격(좌클릭). autoMixCombos 면 새 콤보를 시작할 때마다 기본 콤보(4타)와 두 번째 콤보(5타) 중 하나를 고르고
    /// (같은 콤보가 maxSameComboInRow 번 넘게 연달아 나오지 않게), 콤보 도중에는 그 콤보를 이어 간다.
    /// 회피 후반·직후면 회피 베기.
    /// </summary>
    public bool TryAttack()
    {
        if (!autoMixCombos || comboB == null || comboB.Length == 0) return TryChain(combo);
        if (state == State.Attack && _chain != null) return TryChain(_chain);
        bool evade = state == State.Dodge || ((state == State.Idle || state == State.Block) && Time.time - _lastDodgeEnd <= evadeAttackGrace);
        if (evade && Valid(evadeAttack)) return TryChain(combo); // 회피 베기는 TryChain(combo) 에서 처리
        if (state == State.Attack && _queued != null) return false; // 이미 다음 공격 예약됨
        return TryChain(PickChain());
    }

    AttackData[] PickChain()
    {
        bool b = Random.value < comboBChance;
        if (_sameComboCount >= maxSameComboInRow) b = !_lastPickB;
        if (b == _lastPickB) _sameComboCount++; else _sameComboCount = 1;
        _lastPickB = b;
        return b ? comboB : combo;
    }

    /// <summary>두 번째 콤보를 직접(자동 테스트·AI 용).</summary>
    public bool TryComboB() { return TryChain(comboB); }

    /// <summary>콤보 배열 chain 의 다음 타. 다른 공격 도중이면 chain 의 1타를 예약(콤보 바꾸기).</summary>
    public bool TryChain(AttackData[] chain)
    {
        if (_dead || chain == null || chain.Length == 0 || !Valid(chain[0])) return false;
        if (chain == combo && Valid(evadeAttack))
        {
            bool inDodge = state == State.Dodge && _stateTime >= evadeAttackFrom;
            bool afterDodge = (state == State.Idle || state == State.Block) && Time.time - _lastDodgeEnd <= evadeAttackGrace;
            if (inDodge || afterDodge) { Begin(evadeAttack, null, -1); return true; }
        }
        if (state == State.Idle || state == State.Block) { Begin(chain[0], chain, 0); return true; }
        if (!CanQueue()) return false;
        if (_chain == chain)
        {
            if (_comboIndex + 1 < chain.Length && Valid(chain[_comboIndex + 1])) { Queue(chain[_comboIndex + 1], chain, _comboIndex + 1); return true; }
            return false;
        }
        Queue(chain[0], chain, 0); return true;
    }

    /// <summary>강공격(Q). 강공격 후딜 중 다시 누르면 heavyFollow.</summary>
    public bool TryHeavy()
    {
        if (_dead || !Valid(heavy)) return false;
        if (state == State.Idle || state == State.Block) { Begin(heavy, null, -1); return true; }
        if (!CanQueue()) return false;
        if (_attack == heavy) { if (Valid(heavyFollow)) { Queue(heavyFollow, null, -1); return true; } return false; }
        if (_attack == heavyFollow) return false;
        Queue(heavy, null, -1); return true;
    }

    /// <summary>달려 베기. 대기(이동) 중에만.</summary>
    public bool TrySprintAttack()
    {
        if (_dead || state != State.Idle || !Valid(sprintAttack)) return false;
        Begin(sprintAttack, null, -1); return true;
    }

    bool CanQueue()
    {
        return state == State.Attack && comboInRecovery && _attack != null && _attack.kind == AttackKind.Normal
            && _stateTime >= _attack.windup * inputBufferFromWindup && _stateTime < _attack.Total;
    }

    bool Valid(AttackData a)
    {
        return a != null && !string.IsNullOrEmpty(a.animState) && animator != null && animator.HasState(0, Animator.StringToHash(a.animState));
    }

    void Begin(AttackData a, AttackData[] chain, int index) { _chain = chain; _comboIndex = index; StartAttack(a); }
    void Queue(AttackData a, AttackData[] chain, int index) { _queued = a; _queuedChain = chain; _queuedIndex = index; }

    /// <summary>위험 공격(찌르기·베어 넘기기). 대기/막기 상태에서만.</summary>
    /// <summary>위험 공격(찌르기·베어 넘기기). 대기/막기 상태에서만.</summary>
    public bool TryPerilous(AttackKind kind)
    {
        if (_dead || (state != State.Idle && state != State.Block)) return false;
        Begin(kind == AttackKind.Thrust ? thrust : sweep, null, -1);
        return true;
    }

    void StartAttack(AttackData a)
    {
        _attack = a; state = State.Attack; _stateTime = 0f; _hitDone = false; _queued = null; _lungeDone = false;
        currentAttackName = a.name;
        _blockHeld = false;
        Play(a.animState, a.fadeIn);
        if (OnAttackStarted != null) OnAttackStarted(this, a);
    }

    void TickAttack()
    {
        var a = _attack;
        if (_stateTime < a.windup) { if (_stateTime < a.windup * trackUntilWindupFraction) FaceTarget(); return; }
        if (!_lungeDone)
        {
            _lungeDone = true;
            if (a.lunge > 0f && !useRootMotion)
            {
                // 상대와 너무 가까우면 덜 나간다
                float d = target != null ? Vector3.Distance(Flat(transform.position), Flat(target.position)) : 99f;
                float move = Mathf.Min(a.lunge, Mathf.Max(0f, d - 1.1f));
                MoveRoot(transform.forward * move);
            }
        }
        if (!_hitDone && _stateTime >= a.windup)
        {
            _hitDone = true;
            ResolveHit(a);
        }
        if (_queued != null && _stateTime >= Mathf.Max(a.ChainTime, a.windup + a.active)) Begin(_queued, _queuedChain, _queuedIndex);
        else if (_stateTime >= a.Total) ToIdle();
    }

    void ResolveHit(AttackData a)
    {
        if (OnStrike != null) OnStrike(this, a);
        if (target == null) return;
        var other = target.GetComponent<SwordCombatant>();
        if (other == null || other.team == team) return;
        Vector3 to = Flat(target.position) - Flat(transform.position);
        float dist = to.magnitude;
        if (dist > a.range) return;
        if (Vector3.Angle(transform.forward, to) > a.arc) return;
        other.ReceiveAttack(this, a);
    }

    // ---------------- 막기 / 튕겨내기 ----------------

    /// <summary>막기 버튼을 '누른' 순간. 튕겨내기 타이밍 기록 + 막기 시작.</summary>
    public void PressBlock()
    {
        if (_dead) return;
        _parryPressTime = Time.time;
        SetBlock(true);
    }

    /// <summary>막기 버튼을 누르고 있는지.</summary>
    public void SetBlock(bool held)
    {
        if (_dead) return;
        _blockHeld = held;
        if (held && state == State.Idle) { state = State.Block; _stateTime = 0f; Play("Block", 0.08f); }
        else if (!held && state == State.Block) ToIdle();
    }

    // ---------------- 회피 / 점프 ----------------

    public bool TryDodge(Vector3 worldDir)
    {
        if (_dead || (state != State.Idle && state != State.Block)) return false;
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.01f) worldDir = -transform.forward;
        _dodgeDir = worldDir.normalized;
        bool back = Vector3.Dot(_dodgeDir, -transform.forward) > 0.7f;
        _dodgeRoot = useRootMotion && back && animator != null && animator.HasState(0, Animator.StringToHash(dodgeBackState));
        _dodgeDur = _dodgeRoot ? dodgeBackDuration : dodgeDuration;
        state = State.Dodge; _stateTime = 0f; _blockHeld = false;
        Play(_dodgeRoot ? dodgeBackState : "Dodge", 0.05f);
        return true;
    }

    public bool TryJump()
    {
        if (_dead || (state != State.Idle && state != State.Block)) return false;
        state = State.Jump; _stateTime = 0f; _blockHeld = false;
        Play("Jump", 0.05f);
        return true;
    }

    // ---------------- 피격 ----------------

    /// <summary>attacker 의 공격 a 가 나에게 닿았다. 결과를 판정한다.</summary>
    public void ReceiveAttack(SwordCombatant attacker, AttackData a)
    {
        if (_dead) return;
        if (IsInvulnerable) { Event("회피"); return; }
        if (state == State.Jump && a.kind == AttackKind.Sweep) { Event("점프 회피"); return; }

        // 무너진 상태 → 결정타
        if (state == State.Broken)
        {
            Event("결정타");
            attacker.Event("결정타!");
            stats.Deathblow();
            return;
        }

        bool blocking = state == State.Block || _blockHeld;
        bool parry = blocking && !a.IsPerilous && (Time.time - _parryPressTime) <= parryWindow;
        if (parry)
        {
            _parryPressTime = -999f; // 한 번만
            attacker.stats.AddPosture(parryPostureToAttacker);
            stats.AddPosture(parryPostureToSelf);
            state = State.Block; _stateTime = 0f;
            Play("Parry", 0.03f);
            Event("튕겨내기!");
            attacker.Event("튕겨짐");
            return;
        }
        if (blocking && state == State.Block)
        {
            if (a.IsPerilous)
            {
                // 위험 공격은 막아도 그대로 맞는다
                Hit(a, "위험 공격 피격");
                return;
            }
            stats.AddPosture(a.blockPostureDamage);
            if (!stats.IsBroken) { Play(Pick(blockHitStates, "BlockHit"), 0.03f); Event("막기"); }
            return;
        }
        Hit(a, "피격");
    }

    void Hit(AttackData a, string label)
    {
        stats.Damage(a.damage);
        if (_dead) return;
        stats.AddPosture(a.postureDamage);
        if (stats.IsBroken) return; // OnBroken 이 상태를 바꿨다
        state = State.HitStun; _stateTime = 0f; _blockHeld = false; _queued = null;
        _hitStunDur = a.hitStun > 0f ? a.hitStun : hitStun;
        bool own = !string.IsNullOrEmpty(a.hitState) && animator != null && animator.HasState(0, Animator.StringToHash(a.hitState));
        Play(own ? a.hitState : Pick(hitStates, "Hit"), 0.03f);
        Event(label);
    }

    /// <summary>names 중 애니메이터에 있는 상태 하나를 무작위로. 없으면 fallback.</summary>
    string Pick(string[] names, string fallback)
    {
        if (names == null || names.Length == 0 || animator == null) return fallback;
        int start = Random.Range(0, names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            var n = names[(start + i) % names.Length];
            if (!string.IsNullOrEmpty(n) && animator.HasState(0, Animator.StringToHash(n))) return n;
        }
        return fallback;
    }

    void OnBroken()
    {
        if (_dead) return;
        state = State.Broken; _stateTime = 0f; _blockHeld = false; _queued = null;
        _brokenEndTime = Time.time + stats.brokenDuration;
        Play("Broken", 0.08f);
        Event("체간 붕괴!");
    }

    void OnStatsDied()
    {
        if (_dead) return;
        _dead = true; state = State.Dead;
        Play(Pick(deathStates, "Death"), 0.05f);
        Event("사망");
        if (OnDeath != null) OnDeath(this);
    }

    /// <summary>테스트용 부활.</summary>
    public void Revive()
    {
        _dead = false; stats.ResetAll(); ToIdle();
    }

    // ---------------- 공용 ----------------

    void ToIdle()
    {
        state = State.Idle; _stateTime = 0f; _attack = null; currentAttackName = ""; _queued = null; _chain = null; _comboIndex = -1;
        SetY(0f);
        Play("Locomotion", 0.15f);
        if (_blockHeld) SetBlock(true);
    }

    /// <summary>루트 모션: 공격·뒤로 회피·피격·사망 중에만 애니메이션 이동(XZ)을 적용. 대기·이동은 이동 컨트롤러(ICharacterMover) / AI 가 움직인다. 옮기는 대상은 Root(부모 OnFootRoot 면 CharacterController.Move).</summary>
    void OnAnimatorMove()
    {
        if (animator == null || !useRootMotion) return;
        bool apply = state == State.Attack || state == State.HitStun || state == State.Dead || state == State.Broken || (state == State.Dodge && _dodgeRoot);
        if (!apply) return;
        Vector3 d = animator.deltaPosition; d.y = 0f;
        if (d.sqrMagnitude < 1e-10f) return;
        if (target != null)
        {
            Vector3 to = Flat(target.position) - Flat(transform.position);
            float cur = to.magnitude;
            if (cur > 1e-4f)
            {
                Vector3 dir = to / cur;
                float along = Vector3.Dot(d, dir);
                float allowed = Mathf.Max(0f, cur - minTargetDistance);
                if (along > allowed) d -= dir * (along - allowed); // 상대 쪽으로 파고드는 성분만 자른다
            }
        }
        MoveRoot(d);
    }

    void FaceTarget()
    {
        if (target == null) return;
        Vector3 to = Flat(target.position) - Flat(transform.position);
        if (to.sqrMagnitude < 0.0001f) return;
        var want = Quaternion.LookRotation(to.normalized, Vector3.up);
        var r = Root;
        r.rotation = Quaternion.RotateTowards(r.rotation, want, faceTurnSpeed * Time.deltaTime);
    }

    void Play(string stateName, float fade)
    {
        if (animator == null) return;
        if (animator.HasState(0, Animator.StringToHash(stateName))) animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
    }

    void Event(string what) { if (OnCombatEvent != null) OnCombatEvent(this, what); }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
