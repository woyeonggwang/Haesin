using UnityEngine;

/// <summary>선원이 든 무기. 일본군 = 조총, 관군·해적 = 활.</summary>
public enum CrewWeapon { None, Matchlock, Bow }

/// <summary>
/// 선원 한 명. 배(ShipCrew)가 정한 표적 배의 선원을 쏘고, 맞으면 쓰러진다.
///
/// 상태: 대기(Idle 클립, 무기는 몸 옆에 듦) → 표적이 있고 장전이 끝나면 사격 클립을 한 번 재생
/// (조총 2 s 중 1.0 s, 활 3 s 중 1.68 s 에 발사) → 다시 대기(장전 시간 동안) → … .
/// 무기 소켓은 대기 = carry 자세, 사격 = aim 자세(사격 클립에 맞춰 만든 값)로 0.3 s 에 걸쳐 바뀐다.
/// 쏘는 판정은 확률(거리에 따라 hitNear → hitFar)이고, 맞으면 표적 선원이 쓰러진다(배 체력은 안 깎임 — 사용자 지시).
/// 화살·총 연출은 CrewProjectiles 가 맡는다.
///
/// 배가 카메라에서 멀면(ShipCrew.crewHideDistance) 이 오브젝트가 통째로 꺼진다. 꺼진 동안 맞거나 되살아나면 상태만 바꾸고,
/// 다시 켜진 뒤 첫 Update 에서 자세(쓰러진 끝 자세 / 대기)를 맞춘다 — 꺼진 Animator 에 Play 를 부르면 경고가 나서.
/// 예전엔 갑판 위 임시 큐브였다. 지금은 이 오브젝트(배율 1, 발 = 원점) 아래에 병사 모델 프리팹이 붙는다.
/// </summary>
public class CrewMember : MonoBehaviour
{
    [Tooltip("소속 배. ShipCrew 가 채운다.")]
    public ShipCrew ship;
    [Tooltip("이 선원이 서 있는 갑판 자리 번호 (ShipCrew.members 인덱스).")]
    public int slotIndex;

    [Header("무기·애니메이션 (선원 교체 때 채움)")]
    public CrewWeapon weapon = CrewWeapon.None;
    public Animator animator;
    [Tooltip("무기 소켓(조총 = 오른손 Matchlock_Socket, 활 = 왼손 Bow_Socket).")]
    public Transform weaponSocket;
    [Tooltip("사격 클립에 맞춘 소켓 로컬 자세.")]
    public Vector3 aimSocketPosition, aimSocketEuler;
    [Tooltip("대기 때 소켓 로컬 자세(조총은 세워 듦, 활은 몸 옆에 세움).")]
    public Vector3 carrySocketPosition, carrySocketEuler;
    [Tooltip("활 선원이면 활시위 스크립트(대기 중에는 시위를 손에 걸지 않게 끈다).")]
    public BowString bowString;
    [Tooltip("사격 상태 이름 / 대기 / 사망.")]
    public string shootState = "Fire", idleState = "Idle", deathState = "Death";
    [Tooltip("사격 클립에서 발사하는 순간(정규 시간). 조총 1.0/2.0, 활 1.68/3.0.")]
    [Range(0f, 1f)] public float fireAt = 0.5f;

    [Header("상태 (읽기 전용)")]
    public bool alive = true;
    public bool shooting;

    // 런타임
    float _nextShot;
    float _aimWeight;        // 0 = carry, 1 = aim
    bool _fired;
    float _shootStart;
    float _deathTime = -1f;
    float _restYaw;
    bool _initDone;
    Transform _chest;
    CrewMember _aimTarget;
    AnimatorCullingMode _cullMode = AnimatorCullingMode.CullUpdateTransforms;
    bool _poseOnUpdate;      // 다시 켜진 뒤 첫 Update 에서 자세 맞추기

    void Awake() { Init(); }

    void OnEnable()
    {
        // 처음 켜질 때는 Awake(Init)가 대기 자세를 잡음. 그 뒤 다시 켜질 때만(배가 가까워짐) 자세를 다시 맞춤.
        if (_wasDisabled) _poseOnUpdate = true;
    }

    bool _wasDisabled;
    void OnDisable()
    {
        _wasDisabled = true;
        shooting = false;
        _aimTarget = null;
    }

    void ApplyPoseAfterEnable()
    {
        _poseOnUpdate = false;
        if (animator == null) return;
        animator.enabled = true;
        if (!alive)
        {
            // 쓰러진 끝 자세로 바로 — 잠깐 돌려 뼈에 쓴 뒤 Update 가 Animator 를 끔
            animator.speed = 1f;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Play(deathState, 0, 1f);
            _deathTime = Time.time - 2.4f;
            if (bowString != null) bowString.allowHold = false;
            return;
        }
        animator.cullingMode = _cullMode;
        animator.speed = Random.Range(0.92f, 1.08f);
        animator.Play(idleState, 0, Random.value);
        transform.localRotation = Quaternion.Euler(0f, _restYaw, 0f);
        _aimWeight = 0f;
        ApplySocket();
        if (bowString != null) bowString.allowHold = false;
        _nextShot = Time.time + Random.Range(0.5f, 3f);
    }

    void Init()
    {
        if (_initDone) return;
        _initDone = true;
        _restYaw = transform.localEulerAngles.y;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman) _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (animator != null) _cullMode = animator.cullingMode;
        // 처음 장전 시간을 흩어 둬서 배 한 척이 한꺼번에 쏘지 않게
        _nextShot = Time.time + Random.Range(0.5f, 4f);
        if (animator != null && gameObject.activeInHierarchy)
        {
            animator.Play(idleState, 0, Random.value);
            animator.speed = Random.Range(0.92f, 1.08f);
        }
        else if (animator != null) _poseOnUpdate = true;
        _aimWeight = 0f;
        ApplySocket();
        if (bowString != null) bowString.allowHold = false;
    }

    /// <summary>적이 겨누는 자리(가슴).</summary>
    public Vector3 AimPosition
    {
        get { return _chest != null ? _chest.position : transform.position + transform.up * 1.3f; }
    }

    /// <summary>총구·활 앞(발사 시작점).</summary>
    public Vector3 MuzzlePosition
    {
        get
        {
            if (weaponSocket != null && weaponSocket.childCount > 0)
            {
                Transform w = weaponSocket.GetChild(0);
                return weapon == CrewWeapon.Matchlock ? w.TransformPoint(0f, 0f, 1.08f) : w.TransformPoint(0.02f, 0.03f, 0.1f);
            }
            return AimPosition;
        }
    }

    void Update()
    {
        if (!_initDone) Init();
        if (_poseOnUpdate) ApplyPoseAfterEnable();
        if (!alive)
        {
            // 쓰러지는 클립이 끝나면 애니메이터를 멈춰 자세를 굳힘(계산 아낌).
            // 쓰러지는 동안은 화면 밖이어도 뼈를 움직여야 한다 — CullUpdateTransforms 그대로 끄면 화면 밖에서 죽은 선원이
            // 선 자세로 굳었다(2026-10-04 Play 시험에서 확인).
            if (_deathTime > 0f && animator != null && animator.enabled && Time.time - _deathTime > 2.6f) animator.enabled = false;
            return;
        }
        if (animator == null || ship == null) return;

        CrewMember target = null;
        bool canFight = ship.combatEnabled && ship.target != null;

        if (shooting)
        {
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            bool inShoot = st.IsName(shootState);
            float nt = inShoot ? st.normalizedTime : 0f;
            if (inShoot && !_fired && nt >= fireAt)
            {
                _fired = true;
                Fire();
            }
            // 클립 한 번이 끝나면(또는 너무 오래 걸리면) 대기로
            if ((inShoot && nt >= 0.96f) || Time.time - _shootStart > 6f)
            {
                shooting = false;
                animator.CrossFadeInFixedTime(idleState, 0.3f);
                _nextShot = Time.time + ship.ReloadTime(weapon);
                _aimTarget = null;
            }
        }
        else if (canFight && Time.time >= _nextShot && weapon != CrewWeapon.None)
        {
            target = ship.PickTargetMember();
            if (target != null)
            {
                _aimTarget = target;
                shooting = true;
                _fired = false;
                _shootStart = Time.time;
                animator.CrossFadeInFixedTime(shootState, 0.25f);
            }
            else _nextShot = Time.time + 1f;
        }

        // 몸 방향: 쏘는 중에는 표적 쪽, 아니면 원래 방향
        float wantYaw = _restYaw;
        if (shooting && _aimTarget != null)
        {
            Vector3 to = _aimTarget.AimPosition - transform.position;
            Transform parent = transform.parent;
            if (parent != null) to = parent.InverseTransformDirection(to);
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }
        float y = Mathf.MoveTowardsAngle(transform.localEulerAngles.y, wantYaw, 240f * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0f, y, 0f);

        // 무기 자세 섞기
        _aimWeight = Mathf.MoveTowards(_aimWeight, shooting ? 1f : 0f, Time.deltaTime / 0.3f);
        ApplySocket();
        if (bowString != null) bowString.allowHold = shooting;
    }

    void ApplySocket()
    {
        if (weaponSocket == null) return;
        float w = Mathf.SmoothStep(0f, 1f, _aimWeight);
        weaponSocket.localPosition = Vector3.Lerp(carrySocketPosition, aimSocketPosition, w);
        weaponSocket.localRotation = Quaternion.Slerp(Quaternion.Euler(carrySocketEuler), Quaternion.Euler(aimSocketEuler), w);
    }

    void Fire()
    {
        CrewMember target = _aimTarget;
        if (target == null || !target.alive) target = ship.PickTargetMember();
        if (target == null) return;

        Vector3 from = MuzzlePosition;
        Vector3 to = target.AimPosition;
        float dist = Vector3.Distance(from, to);
        float p = ship.HitChance(weapon, dist);
        bool hit = Random.value < p;
        if (weapon == CrewWeapon.Matchlock) CrewProjectiles.Musket(from, to, hit ? target : null, ship);
        else CrewProjectiles.Arrow(from, to, hit ? target : null, ship);
    }

    /// <summary>맞아서 쓰러진다.</summary>
    public void Kill()
    {
        if (!alive) return;
        alive = false;
        shooting = false;
        _deathTime = Time.time;
        if (bowString != null) bowString.allowHold = false;
        if (!gameObject.activeInHierarchy) _poseOnUpdate = true; // 꺼진 채 맞음 → 켜질 때 쓰러진 자세
        else if (animator != null)
        {
            animator.enabled = true;
            animator.speed = 1f;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.CrossFadeInFixedTime(deathState, 0.15f);
        }
        if (ship != null) ship.NotifyMemberDied(this);
    }

    /// <summary>되살린다(플레이어 리스폰 등).</summary>
    public void Revive()
    {
        if (!_initDone) Init();
        alive = true;
        shooting = false;
        _deathTime = -1f;
        _aimWeight = 0f;
        transform.localRotation = Quaternion.Euler(0f, _restYaw, 0f);
        if (!gameObject.activeInHierarchy) _poseOnUpdate = true; // 꺼진 채 되살아남 → 켜질 때 대기 자세
        else if (animator != null)
        {
            animator.enabled = true;
            animator.cullingMode = _cullMode;
            animator.Play(idleState, 0, Random.value);
            animator.speed = Random.Range(0.92f, 1.08f);
        }
        _nextShot = Time.time + Random.Range(0.5f, 3f);
        ApplySocket();
        if (bowString != null) bowString.allowHold = false;
    }
}
