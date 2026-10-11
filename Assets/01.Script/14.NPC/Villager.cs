using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 마을을 돌아다니는 사람 한 명(VillagePopulation 이 만듦). NavMeshAgent 로 걷고, 도착하면 잠깐 서 있다가 다음 자리로.
/// 다음 자리는 지금 자리에서 20~90 m 안(없으면 구역 아무 데나). 애니메이터 float "Speed" = 0(서 있음)~1(걷기),
/// 걷는 동안 Animator.speed = 실제 속도 / 클립 걷기 속도(walkClipSpeed — Quaternius Walk_Loop 를 시민 모델로 잰 값 약 0.8 m/s).
/// 죽음(2026-10-05 사용자 지시 "마을 NPC 죽일 수 있게"): 플레이어 칼 공격(SwordCombatant.OnStrike — 표적이 아니어도)의
/// 거리(range + hitRadius)·각도(arc) 안이면 한 번에 쓰러짐(Death 상태). 시체는 그 자리에 남고, 마을에서 멀어져 풀로 돌아간 뒤 다시 쓰일 때 되살아남.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Villager : MonoBehaviour
{
    public VillagePopulation population;
    [Tooltip("풀에서 쓰는 중인지(VillagePopulation 이 정함). 카메라에서 멀어 오브젝트가 꺼져 있어도 쓰는 중이면 true.")]
    public bool inUse;
    [Tooltip("걷는 속도(m/s, 최소~최대) — 사람마다 하나 고름.")]
    public Vector2 walkSpeed = new Vector2(0.8f, 1.15f);
    [Tooltip("서 있는 시간(초, 최소~최대).")]
    public Vector2 waitTime = new Vector2(2f, 10f);
    [Tooltip("걷기 클립을 1 배속으로 틀 때의 몸 속도(m/s).")]
    public float walkClipSpeed = 0.8f;

    [Header("죽음")]
    [Tooltip("칼 판정 거리에 더하는 몸 반지름(m).")]
    public float hitRadius = 0.35f;
    [Tooltip("쓰러지는 애니메이터 상태 이름.")]
    public string deathState = "Death";
    [Tooltip("죽었는지(읽기 전용).")]
    public bool dead;

    /// <summary>플레이어가 마을 사람을 쓰러뜨린 자리(병사 SoldierGroup 이 듣고 주변 병사를 덤비게 함, 2026-10-06).</summary>
    public static System.Action<Vector3> OnKilledByPlayer;

    /// <summary>켜져 있는 마을 사람(칼 판정용).</summary>
    public static readonly List<Villager> Active = new List<Villager>();
    static bool _hooked;

    NavMeshAgent _agent;
    Animator _anim;
    AnimatorCullingMode _cull;
    float _waitUntil;
    bool _walking;
    float _speedParam;
    float _stuckTimer;
    Vector3 _lastPos;
    bool _poseDeathOnUpdate;
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int LocoHash = Animator.StringToHash("Locomotion");

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponent<Animator>();
        if (_anim != null) _cull = _anim.cullingMode;
    }

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        if (!_hooked) { SwordCombatant.OnStrike += HandleStrike; _hooked = true; }
        // 다시 켜지면 Animator 가 처음 상태(걷기 트리)로 돌아가므로 쓰러진 자세를 다시 맞춤
        if (dead) _poseDeathOnUpdate = true;
    }

    void OnDisable() { Active.Remove(this); }

    /// <summary>VillagePopulation 이 켤 때 부름.</summary>
    public void Begin(Vector3 pos)
    {
        if (_agent == null) Awake();
        if (dead)
        {
            dead = false;
            _poseDeathOnUpdate = false;
            _agent.enabled = true;
            if (_anim != null) _anim.cullingMode = _cull;
        }
        _agent.speed = Random.Range(walkSpeed.x, walkSpeed.y);
        if (!_agent.isOnNavMesh) _agent.Warp(pos);
        _walking = false;
        _speedParam = 0f;
        _waitUntil = Time.time + Random.Range(0f, waitTime.y);
        if (_anim != null) _anim.Play(LocoHash, 0, Random.value);
    }

    void Update()
    {
        if (dead)
        {
            if (_poseDeathOnUpdate && _anim != null) { _poseDeathOnUpdate = false; _anim.speed = 1f; _anim.Play(deathState, 0, 1f); }
            return;
        }
        if (_agent == null || !_agent.isOnNavMesh) return;

        if (_walking)
        {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
            {
                _walking = false;
                _waitUntil = Time.time + Random.Range(waitTime.x, waitTime.y);
            }
            else
            {
                // 막혀서 거의 못 움직이면 다른 곳으로
                if ((transform.position - _lastPos).sqrMagnitude < 0.0004f) _stuckTimer += Time.deltaTime; else _stuckTimer = 0f;
                if (_stuckTimer > 4f) { _stuckTimer = 0f; PickNext(); }
            }
        }
        else if (Time.time >= _waitUntil)
        {
            PickNext();
        }
        _lastPos = transform.position;

        if (_anim != null)
        {
            float v = _agent.velocity.magnitude;
            float target = Mathf.Clamp01(v / Mathf.Max(0.1f, walkClipSpeed * 0.6f));
            _speedParam = Mathf.MoveTowards(_speedParam, target, Time.deltaTime * 3f);
            _anim.SetFloat(SpeedHash, _speedParam);
            _anim.speed = _speedParam > 0.5f ? Mathf.Clamp(v / walkClipSpeed, 0.7f, 1.6f) : 1f;
        }
    }

    void PickNext()
    {
        if (population == null) return;
        Vector3 p;
        bool ok = population.TryRandomPointNear(transform.position, 90f, out p, 8);
        if (ok && (p - transform.position).sqrMagnitude < 400f) ok = false;   // 20 m 안은 너무 가까움
        if (!ok) ok = population.TryRandomPoint(out p, 8);
        if (!ok) { _waitUntil = Time.time + 3f; return; }
        _agent.SetDestination(p);
        _walking = true;
        _stuckTimer = 0f;
    }

    /// <summary>쓰러뜨림. 쓰러지는 쪽 = from 의 반대(from 을 보게 돌린 뒤 Death).</summary>
    public void Kill(Vector3 from)
    {
        if (dead) return;
        dead = true;
        _walking = false;
        if (_agent != null && _agent.enabled)
        {
            if (_agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
            _agent.enabled = false;
        }
        Vector3 to = from - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        if (_anim != null)
        {
            _anim.speed = 1f;
            _anim.SetFloat(SpeedHash, 0f);
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 화면 밖에서도 끝까지 쓰러지게(선원 때 시행착오)
            _anim.CrossFadeInFixedTime(deathState, 0.12f);
        }
    }

    /// <summary>플레이어 칼 공격 판정 순간 — 범위 안 산 마을 사람을 쓰러뜨림.</summary>
    static void HandleStrike(SwordCombatant attacker, AttackData a)
    {
        if (attacker == null || a == null || Active.Count == 0) return;
        if (!IsPlayer(attacker)) return;
        Vector3 o = attacker.transform.position;
        Vector3 fwd = attacker.transform.forward; fwd.y = 0f;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            Villager v = Active[i];
            if (v == null || v.dead) continue;
            Vector3 to = v.transform.position - o;
            if (Mathf.Abs(to.y) > 2f) continue;
            to.y = 0f;
            float d = to.magnitude;
            if (d > a.range + v.hitRadius) continue;
            if (d > 0.3f && Vector3.Angle(fwd, to) > a.arc) continue;
            v.Kill(o);
            var sa = attacker.GetComponent<SwordAudio>();
            if (sa != null) sa.PlayFleshAt(v.transform.position + Vector3.up * 1.1f);
            if (OnKilledByPlayer != null) OnKilledByPlayer(v.transform.position);
        }
    }

    static bool IsPlayer(SwordCombatant c)
    {
        foreach (var p in FindObjectsByType<PlayerCombatController>(FindObjectsSortMode.None))
            if (p.combatant == c) return true;
        return false;
    }
}
