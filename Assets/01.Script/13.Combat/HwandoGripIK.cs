using UnityEngine;

/// <summary>
/// 두 손으로 쥐는 동작에서 왼손을 환도 손잡이에 붙인다(Humanoid IK, 컨트롤러 Base Layer 의 IK Pass 필요).
/// 애니메이션의 왼손목이 손잡이 축에서 fullWeightDistance 안이면 IK 100 %, zeroWeightDistance 밖이면 0 %(한 손 동작은 건드리지 않음).
/// 오른손 소켓 위치는 OnAnimatorIK 시점에 아직 이번 프레임 값이 아니므로, 오른손 IK 목표(애니메이션 값)와 소켓의 상대 관계를
/// 매 프레임 LateUpdate 에서 보정해 두고 그걸로 이번 프레임 소켓 위치를 계산한다(한 프레임 늦음 방지).
/// </summary>
[RequireComponent(typeof(Animator))]
public class HwandoGripIK : MonoBehaviour
{
    public Transform handSocket;
    public WeaponHolder weapon;
    [Tooltip("손잡이 길이: 코등이(소켓)에서 뒤로(m). 환도 손잡이 끝 0.231.")] public float handleLength = 0.23f;
    [Tooltip("오른손이 쥐는 부분 뒤, 왼손이 쥘 수 있는 구간의 시작(m).")] public float leftGripStart = 0.09f;
    [Tooltip("왼손목(손 뼈)과 손잡이 축 사이 목표 거리(m). 오른손목–손잡이 축 거리와 같게.")] public float wristOffset = 0.06f;
    [Tooltip("애니메이션의 왼손목이 손잡이 축에서 이 거리 안이면 IK 100 %.")] public float fullWeightDistance = 0.20f;
    [Tooltip("이 거리 밖이면 IK 0 %(한 손 동작).")] public float zeroWeightDistance = 0.30f;
    [Tooltip("가중치 변화 속도(1/초).")] public float blendSpeed = 8f;
    [Range(0f, 1f)] public float maxWeight = 1f;
    [Tooltip("왼손가락을 쥔 자세로 덮는 애니메이터 레이어(왼손가락 마스크 + Kevin Iglesias Human@ObjectGripHands01). 가중치 = IK 가중치.")] public string fingerLayerName = "LeftGrip";

    Animator _anim; SwordCombatant _sc; int _fingerLayer = -1;
    float _w; bool _haveRel; Vector3 _relPos; Quaternion _relRot = Quaternion.identity;
    Vector3 _goalPos; Quaternion _goalRot; bool _goalValid;
    public float CurrentWeight { get { return _w; } }

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _sc = GetComponent<SwordCombatant>();
        if (weapon == null) weapon = GetComponent<WeaponHolder>();
        if (_anim != null && !string.IsNullOrEmpty(fingerLayerName)) _fingerLayer = _anim.GetLayerIndex(fingerLayerName);
        if (handSocket == null)
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Socket_Hwando_Hand_R") { handSocket = t; break; }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (handSocket == null || _anim == null) return;
        _goalPos = _anim.GetIKPosition(AvatarIKGoal.RightHand);
        _goalRot = _anim.GetIKRotation(AvatarIKGoal.RightHand);
        _goalValid = true;

        Vector3 sPos; Vector3 sFwd;
        if (_haveRel) { sPos = _goalPos + _goalRot * _relPos; sFwd = (_goalRot * _relRot) * Vector3.forward; }
        else { sPos = handSocket.position; sFwd = handSocket.forward; }

        bool drawn = weapon == null || weapon.IsDrawn;
        bool dead = _sc != null && _sc.state == SwordCombatant.State.Dead;
        // 칼 든 이동(Loco_Drawn: rig_Idle1 등 두 손 자세)에서도 쓴다. 점프(fable 임시 동작)만 제외. 칼을 넣었으면 weapon.IsDrawn=false 로 꺼진다.
        bool combatPose = _sc == null || _sc.state != SwordCombatant.State.Jump;

        Vector3 wrist = _anim.GetIKPosition(AvatarIKGoal.LeftHand);
        Vector3 a = sPos - sFwd * leftGripStart;
        Vector3 b = sPos - sFwd * handleLength;
        Vector3 ab = b - a;
        float k = Mathf.Clamp01(Vector3.Dot(wrist - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        Vector3 q = a + ab * k;
        Vector3 off = Vector3.ProjectOnPlane(wrist - q, sFwd);
        float dist = (wrist - q).magnitude;

        float want = (drawn && !dead && combatPose) ? Mathf.InverseLerp(zeroWeightDistance, fullWeightDistance, dist) * maxWeight : 0f;
        _w = Mathf.MoveTowards(_w, want, blendSpeed * Time.deltaTime);
        if (_fingerLayer > 0) _anim.SetLayerWeight(_fingerLayer, _w);
        if (_w <= 0.001f) { _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f); return; }

        Vector3 dir = off.sqrMagnitude > 1e-8f ? off.normalized : Vector3.down;
        _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, _w);
        _anim.SetIKPosition(AvatarIKGoal.LeftHand, q + dir * wristOffset);
    }

    void LateUpdate()
    {
        if (!_goalValid || handSocket == null) return;
        // 이번 프레임에 기록한 오른손 IK 목표와 실제 소켓 위치의 상대 관계(뼈 기준이라 일정) 보정
        var inv = Quaternion.Inverse(_goalRot);
        _relPos = inv * (handSocket.position - _goalPos);
        _relRot = inv * handSocket.rotation;
        _haveRel = true;
    }
}
