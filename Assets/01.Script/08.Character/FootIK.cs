using UnityEngine;

/// <summary>
/// 발 IK — 계단·경사에서 발을 실제 바닥(디딤판) 위에 놓는다 (Humanoid, 컨트롤러 Base Layer 의 IK Pass 필요).
///
/// 이동(CharacterController)은 계단 디딤판(StepSurface 레이어)을 무시하고 그 밑 터레인 경사를 매끈하게 걷는다
/// (OnFootController.walkThroughLayers). 그래서 애니메이션 발은 디딤판을 뚫거나 떠 있다.
/// 매 프레임 두 발목 아래로 구를 쏴 바닥 높이를 찾고, 캐릭터 피벗(= 발바닥 기준면)과의 차이만큼 발을 올리거나 내린다.
/// 두 발 중 낮은 쪽 차이만큼 골반(bodyPosition)을 옮겨 다리가 늘어나지 않게 한다.
/// PlayerCharacter 가 있으면 OnFoot 상태에서만 켠다(배 위에서는 끔). 사망·점프 중에도 끈다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class FootIK : MonoBehaviour
{
    [Tooltip("피벗 위 이 높이에서 아래로 구를 쏜다(m). 계단 한 단(0.28 m)보다 커야 다음 디딤판을 찾는다.")]
    public float rayStartHeight = 0.6f;
    [Tooltip("피벗 아래로 바닥을 찾는 최대 깊이(m).")]
    public float rayDepth = 0.6f;
    [Tooltip("구 반지름(m). 디딤판 모서리에서 발이 떨어지지 않게 조금 둥글게.")]
    public float sphereRadius = 0.04f;
    [Tooltip("바닥으로 인정하는 최대 경사(°). 계단 챌판(세로면)을 바닥으로 잡지 않게.")]
    public float maxGroundSlope = 50f;
    [Tooltip("바닥을 찾을 레이어. 기본 = Ignore Raycast·Water·UI·Showcase 제외 전부.")]
    public LayerMask groundMask = ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 8));

    [Header("골반")]
    [Tooltip("골반을 내릴 수 있는 최대값(m).")] public float maxPelvisDown = 0.4f;
    [Tooltip("골반을 올릴 수 있는 최대값(m).")] public float maxPelvisUp = 0.3f;
    [Tooltip("골반 보간 속도(1/s).")] public float pelvisSpeed = 8f;

    [Header("발")]
    [Tooltip("발을 올릴 때 보간 속도(1/s). 챌판에 발끝이 박히지 않게 빠르게.")] public float footUpSpeed = 30f;
    [Tooltip("발을 내릴 때 보간 속도(1/s).")] public float footSpeed = 12f;
    [Tooltip("발목에서 발끝까지 앞쪽 거리(m). 발끝 아래도 바닥을 찾아 높은 쪽에 맞춘다(계단 오를 때 발끝이 챌판을 뚫지 않게).")] public float toeReach = 0.17f;
    [Tooltip("발이 이 높이 이상 들리면 바닥 기울기 회전을 0 으로(m).")] public float liftFade = 0.2f;
    [Tooltip("켜고 끌 때 가중치 변화 속도(1/s).")] public float blendSpeed = 4f;
    [Range(0f, 1f)] public float weight = 1f;

    [Header("상태 (읽기 전용)")]
    public float pelvisOffset;
    public float leftDelta, rightDelta;
    [Tooltip("애니메이션(IK 전) 발바닥이 피벗 면에서 들린 높이(m). FootstepAudio 가 발 닿음 판정에 씀 — 계단에서도 애니메이션 박자 그대로.")]
    public float animLiftLeft, animLiftRight;

    Animator _anim; PlayerCharacter _pc; SwordCombatant _sc; CharacterController _cc;
    float _w;
    Vector3 _nL = Vector3.up, _nR = Vector3.up;
    Transform _footL, _footR, _toeL, _toeR;
    readonly RaycastHit[] _hits = new RaycastHit[12];

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _pc = GetComponent<PlayerCharacter>();
        _sc = GetComponent<SwordCombatant>();
        if (_anim != null && _anim.isHuman)
        {
            _footL = _anim.GetBoneTransform(HumanBodyBones.LeftFoot); _toeL = _anim.GetBoneTransform(HumanBodyBones.LeftToes);
            _footR = _anim.GetBoneTransform(HumanBodyBones.RightFoot); _toeR = _anim.GetBoneTransform(HumanBodyBones.RightToes);
        }
    }

    // 발끝 방향(수평). OnAnimatorIK 시점의 뼈는 지난 프레임 자세지만 방향은 천천히 변하므로 충분하다.
    Vector3 ToeDir(Transform foot, Transform toe)
    {
        Vector3 f = (foot != null && toe != null) ? toe.position - foot.position : transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 1e-6f ? f.normalized : transform.forward;
    }

    bool WantActive()
    {
        if (_pc != null && _pc.state != PlayerCharacter.State.OnFoot) return false;
        if (_sc != null && (_sc.state == SwordCombatant.State.Dead || _sc.state == SwordCombatant.State.Jump)) return false;
        if (_cc == null || !transform.IsChildOf(_cc.transform)) _cc = GetComponentInParent<CharacterController>();
        if (_cc != null && _cc.enabled && !_cc.isGrounded) return false;
        return true;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || _anim == null) return;
        float dt = Time.deltaTime;
        animLiftLeft = _anim.GetIKPosition(AvatarIKGoal.LeftFoot).y - _anim.leftFeetBottomHeight - transform.position.y;
        animLiftRight = _anim.GetIKPosition(AvatarIKGoal.RightFoot).y - _anim.rightFeetBottomHeight - transform.position.y;
        _w = Mathf.MoveTowards(_w, WantActive() ? weight : 0f, blendSpeed * dt);
        if (_w <= 0.001f)
        {
            pelvisOffset = 0f; leftDelta = 0f; rightDelta = 0f;
            _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f); _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f); _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            return;
        }

        float rootY = transform.position.y; // 피벗 = 발바닥 기준면(PlayerCharacter.standOffset 0)
        Vector3 pL = _anim.GetIKPosition(AvatarIKGoal.LeftFoot);
        Vector3 pR = _anim.GetIKPosition(AvatarIKGoal.RightFoot);
        Quaternion rL = _anim.GetIKRotation(AvatarIKGoal.LeftFoot);
        Quaternion rR = _anim.GetIKRotation(AvatarIKGoal.RightFoot);
        float liftL = pL.y - _anim.leftFeetBottomHeight - rootY;
        float liftR = pR.y - _anim.rightFeetBottomHeight - rootY;

        float k = 1f - Mathf.Exp(-footSpeed * dt);
        float kUp = 1f - Mathf.Exp(-footUpSpeed * dt);
        Vector3 n, n2;
        float tL = Probe(pL, rootY, out n); _nL = Vector3.Slerp(_nL, n, k);
        float tR = Probe(pR, rootY, out n); _nR = Vector3.Slerp(_nR, n, k);
        // 발끝 아래가 더 높으면(다음 디딤판) 그쪽에 맞춘다
        tL = Mathf.Max(tL, Probe(pL + ToeDir(_footL, _toeL) * toeReach, rootY, out n2));
        tR = Mathf.Max(tR, Probe(pR + ToeDir(_footR, _toeR) * toeReach, rootY, out n2));
        leftDelta = Mathf.Lerp(leftDelta, tL, tL > leftDelta ? kUp : k);
        rightDelta = Mathf.Lerp(rightDelta, tR, tR > rightDelta ? kUp : k);

        // 골반: 낮은 발 쪽에 맞춘다(높은 발은 무릎을 굽혀 올림)
        float pelvisTarget = Mathf.Clamp(Mathf.Min(leftDelta, rightDelta), -maxPelvisDown, maxPelvisUp);
        pelvisOffset = Mathf.Lerp(pelvisOffset, pelvisTarget, 1f - Mathf.Exp(-pelvisSpeed * dt));
        _anim.bodyPosition = _anim.bodyPosition + Vector3.up * (pelvisOffset * _w);

        ApplyFoot(AvatarIKGoal.LeftFoot, pL, rL, leftDelta, _nL, liftL);
        ApplyFoot(AvatarIKGoal.RightFoot, pR, rR, rightDelta, _nR, liftR);
    }

    void ApplyFoot(AvatarIKGoal goal, Vector3 animPos, Quaternion animRot, float delta, Vector3 normal, float lift)
    {
        _anim.SetIKPositionWeight(goal, _w);
        _anim.SetIKPosition(goal, animPos + Vector3.up * delta);
        float rw = _w * (1f - Mathf.Clamp01(lift / Mathf.Max(0.01f, liftFade)));
        _anim.SetIKRotationWeight(goal, rw);
        _anim.SetIKRotation(goal, Quaternion.FromToRotation(Vector3.up, normal) * animRot);
    }

    /// <summary>발목 아래 바닥 높이 − 피벗 높이(m). 못 찾으면 0.</summary>
    float Probe(Vector3 foot, float rootY, out Vector3 normal)
    {
        normal = Vector3.up;
        Vector3 origin = new Vector3(foot.x, rootY + rayStartHeight, foot.z);
        int count = Physics.SphereCastNonAlloc(origin, sphereRadius, Vector3.down, _hits, rayStartHeight + rayDepth, groundMask, QueryTriggerInteraction.Ignore);
        float minY = Mathf.Cos(maxGroundSlope * Mathf.Deg2Rad);
        float best = float.MaxValue; bool found = false; float y = rootY;
        Transform self = _cc != null ? _cc.transform : transform;
        for (int i = 0; i < count; i++)
        {
            var h = _hits[i];
            if (h.distance <= 0f) continue;                       // 시작부터 겹친 콜라이더
            if (h.collider.transform.IsChildOf(self)) continue;   // 자기 몸
            if (h.normal.y < minY) continue;                      // 챌판·벽
            if (h.distance < best) { best = h.distance; y = h.point.y; normal = h.normal; found = true; }
        }
        return found ? y - rootY : 0f;
    }
}
