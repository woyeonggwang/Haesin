using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 미행 대상처럼 정해진 길을 걷는 퀘스트 NPC(2026-10-09, 2-5 "은을 받는 가신").
/// 뿌리에 NavMeshAgent·이 스크립트, 자식 모델에 Animator(Villager 컨트롤러 — float "Speed" 0 서 있음 ~ 1 걷기).
/// points 를 차례로 걷고 마지막 점에 닿으면 Arrived. Begin() 으로 출발, ResetToStart() 로 처음 자리로(미행 실패 때).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class QuestWalker : MonoBehaviour
{
    [Tooltip("걸을 길(차례대로). 비우면 걷지 않음.")] public Transform[] points = new Transform[0];
    public float walkSpeed = 1.1f;
    [Tooltip("걷기 클립을 1 배속으로 틀 때 몸 속도(m/s) — 시민 모델 Walk_Loop 값.")] public float walkClipSpeed = 0.8f;
    [Tooltip("이 점에서 잠깐 멈춰 둘러보는 시간(초) — 미행 긴장감용. 0 이면 안 멈춤.")] public float lookAroundSeconds = 2.5f;

    public bool Walking { get; private set; }
    public bool Arrived { get; private set; }
    public int Index { get; private set; }

    NavMeshAgent _agent; Animator _anim; Vector3 _startPos; Quaternion _startRot; float _pauseUntil; float _speedParam;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponentInChildren<Animator>();
        _startPos = transform.position; _startRot = transform.rotation;
        _agent.speed = walkSpeed; _agent.stoppingDistance = 0.3f; _agent.autoBraking = true;
    }

    public void Begin()
    {
        if (points == null || points.Length == 0) { Arrived = true; return; }
        Walking = true; Arrived = false; Index = 0;
        if (_agent.isOnNavMesh) { _agent.isStopped = false; _agent.SetDestination(points[0].position); }
    }

    public void ResetToStart()
    {
        Walking = false; Arrived = false; Index = 0;
        if (_agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
        _agent.Warp(_startPos);
        transform.rotation = _startRot;
    }

    void Update()
    {
        if (Walking && _agent.isOnNavMesh && Time.time >= _pauseUntil)
        {
            if (_agent.isStopped) { _agent.isStopped = false; _agent.SetDestination(points[Index].position); }
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
            {
                Index++;
                if (Index >= points.Length) { Walking = false; Arrived = true; _agent.isStopped = true; }
                else
                {
                    if (lookAroundSeconds > 0f && Index % 2 == 0) { _pauseUntil = Time.time + lookAroundSeconds; _agent.isStopped = true; }
                    else _agent.SetDestination(points[Index].position);
                }
            }
        }
        if (_anim != null)
        {
            float v = _agent.velocity.magnitude;
            float target = Mathf.Clamp01(v / Mathf.Max(0.1f, walkClipSpeed * 0.6f));
            _speedParam = Mathf.MoveTowards(_speedParam, target, Time.deltaTime * 3f);
            _anim.SetFloat(SpeedHash, _speedParam);
            _anim.speed = _speedParam > 0.5f ? Mathf.Clamp(v / walkClipSpeed, 0.7f, 1.6f) : 1f;
        }
    }
}
