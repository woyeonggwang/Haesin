using UnityEngine;

/// <summary>
/// 훈련용 적 AI. 표적(플레이어)에게 다가가 사거리 안에서 공격 패턴을 반복한다.
///   패턴: 대기 후 [기본 콤보 1~4타 | 두 번째 콤보 1~5타 | 강베기(+강베기 2) | 찌르기 | 베어 넘기기] 를 확률로 선택.
///   반응: 플레이어가 공격을 시작하면 확률로 막기(blockChance) 또는 튕겨내기(parryChance, 상대 예비 동작이 끝나기 직전에 누름).
///   플레이어 체간이 무너지면 결정타. 자신이 무너지면 아무것도 안 함.
/// </summary>
[RequireComponent(typeof(SwordCombatant))]
public class EnemySwordAI : MonoBehaviour
{
    public float moveSpeed = 1.7f;
    public float engageDistance = 1.7f;
    public float turnSpeed = 360f;
    [Tooltip("공격 사이 대기 시간(초) 범위.")] public Vector2 attackInterval = new Vector2(0.9f, 1.8f);
    [Range(0f, 1f)] public float thrustChance = 0.2f;
    [Range(0f, 1f)] public float sweepChance = 0.2f;
    [Range(0f, 1f)] public float blockChance = 0.35f;
    [Range(0f, 1f)] public float parryChance = 0.15f;
    [Range(0f, 1f)] public float heavyChance = 0.12f;
    [Tooltip("콤보를 고를 때 두 번째 콤보(comboB)를 쓸 확률.")] [Range(0f, 1f)] public float comboBChance = 0.4f;
    [Tooltip("콤보 다음 타로 이어갈 확률(이어가면 남은 타 수는 무작위).")] [Range(0f, 1f)] public float comboContinueChance = 0.6f;
    [Tooltip("강베기 뒤 강베기 2 로 이어갈 확률.")] [Range(0f, 1f)] public float heavyFollowChance = 0.5f;
    [Tooltip("행동(공격·피격 등)이 끝난 뒤 다음 공격까지 최소 간격(초).")] public float minGapAfterAction = 0.45f;

    SwordCombatant _me; Animator _anim;
    float _nextAttackTime; float _blockUntil = -1f; float _parryAt = -1f; int _comboLeft;
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    SwordCombatant _targetCombatant;

    void Awake()
    {
        _me = GetComponent<SwordCombatant>();
        _anim = GetComponent<Animator>();
        _nextAttackTime = Time.time + 1.5f;
    }

    void OnEnable()
    {
        foreach (var c in FindObjectsByType<SwordCombatant>(FindObjectsSortMode.None))
            if (c != _me && c.team != _me.team) { _me.target = c.transform; _targetCombatant = c; c.OnAttackStarted += OnTargetAttack; break; }
    }

    void OnDisable() { if (_targetCombatant != null) _targetCombatant.OnAttackStarted -= OnTargetAttack; }

    void OnTargetAttack(SwordCombatant who, AttackData a)
    {
        if (_me.state == SwordCombatant.State.Dead || _me.state == SwordCombatant.State.Broken) return;
        if (_me.IsAttacking) return;
        if (a.IsPerilous)
        {
            // 위험 공격은 회피(찌르기) / 점프(베어 넘기기) 시도 — 절반 확률
            if (Random.value < 0.5f) { if (a.kind == AttackKind.Sweep) _me.TryJump(); else _me.TryDodge(transform.right * (Random.value < 0.5f ? 1f : -1f)); }
            return;
        }
        float r = Random.value;
        if (r < parryChance) _parryAt = Time.time + Mathf.Max(0f, a.windup - 0.1f);
        else if (r < parryChance + blockChance) { _me.PressBlock(); _blockUntil = Time.time + a.windup + a.active + 0.25f; }
    }

    void Update()
    {
        if (_me.state == SwordCombatant.State.Dead) { SetSpeed(0f); return; }
        if (_me.target == null) { SetSpeed(0f); return; }
        if (_targetCombatant != null && _targetCombatant.state == SwordCombatant.State.Dead) { SetSpeed(0f); _me.SetBlock(false); return; }

        // 예약된 튕겨내기
        if (_parryAt > 0f && Time.time >= _parryAt) { _parryAt = -1f; _me.PressBlock(); _blockUntil = Time.time + 0.35f; }
        if (_blockUntil > 0f && Time.time >= _blockUntil) { _blockUntil = -1f; _me.SetBlock(false); }

        if (_me.LocksMovement) { SetSpeed(0f); _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + minGapAfterAction); return; }

        Vector3 to = _me.target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;

        // 상대가 무너졌으면 결정타
        if (_targetCombatant != null && _targetCombatant.state == SwordCombatant.State.Broken && dist <= 2.2f) { _me.TryAttack(); return; }

        if (dist > engageDistance)
        {
            var want = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            transform.position += transform.forward * (moveSpeed * Time.deltaTime);
            SetSpeed(moveSpeed);
            return;
        }
        SetSpeed(0f);
        var face = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * Time.deltaTime);

        if (_me.state == SwordCombatant.State.Block) return; // 막는 중엔 공격 안 함

        if (Time.time >= _nextAttackTime)
        {
            float r = Random.value;
            if (r < thrustChance) _me.TryPerilous(AttackKind.Thrust);
            else if (r < thrustChance + sweepChance) _me.TryPerilous(AttackKind.Sweep);
            else if (r < thrustChance + sweepChance + heavyChance && _me.TryHeavy())
            {
                _comboLeft = Random.value < heavyFollowChance ? 1 : 0;
                StartCoroutine(ComboFollowUp(() => _me.TryHeavy()));
            }
            else
            {
                var chain = (_me.comboB != null && _me.comboB.Length > 0 && Random.value < comboBChance) ? _me.comboB : _me.combo;
                if (_me.TryChain(chain))
                {
                    _comboLeft = Random.value < comboContinueChance ? Random.Range(1, chain.Length) : 0;
                    StartCoroutine(ComboFollowUp(() => _me.TryChain(chain)));
                }
            }
            _nextAttackTime = Time.time + Random.Range(attackInterval.x, attackInterval.y) + 0.8f;
        }
    }

    System.Collections.IEnumerator ComboFollowUp(System.Func<bool> next)
    {
        AttackData queuedOn = null; // 같은 타에서 두 번 예약하지 않게
        while (_comboLeft > 0 && _me.IsAttacking)
        {
            var a = _me.CurrentAttack;
            if (a != null && a != queuedOn && _me.AttackPhaseTime >= a.windup + a.active)
            {
                queuedOn = a;
                if (next()) _comboLeft--; else _comboLeft = 0;
            }
            yield return null;
        }
    }

    void SetSpeed(float s) { if (_anim != null) _anim.SetFloat(SpeedHash, s); }
}
