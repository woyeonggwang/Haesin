using UnityEngine;

/// <summary>
/// 활시위와 화살 표시. 활 모델은 휘지 않는 통짜라, 시위만 두 토막(위·아래 고자 → 가운데)으로 매 프레임 다시 놓는다.
/// 가운데 = 손이 시위를 잡고 있으면(애니메이터 float "BowString" ≥ 0.5) 오른손 시위 자리(stringHand), 아니면 활에 붙은 쉬는 자리.
/// 화살은 시위를 잡고 있을 때만 보이고, 시위 가운데에서 활 줌통 옆 화살 자리(arrowPassLocal)를 향한다.
/// "BowString" 은 활 쏘기 클립의 커브(가져오기 설정)로 들어온다 — 놓는 순간 0 이 되어 시위가 돌아가고 화살이 사라진다(날아가는 화살은 아직 없음).
/// 활 로컬 좌표(활 FBX): 줌통 = 원점, 위 = +Y, 쏘는 쪽 = +Z, 시위는 −Z 쪽.
/// </summary>
[ExecuteAlways]
public class BowString : MonoBehaviour
{
    public Animator animator;
    [Tooltip("활 모델(왼손 소켓 아래).")] public Transform bow;
    [Tooltip("오른손이 시위를 거는 자리(오른손 뼈 아래 소켓).")] public Transform stringHand;
    public Transform segmentTop, segmentBottom;
    public Transform arrow;
    public Vector3 nockTopLocal = new Vector3(0f, 0.605f, -0.170f);
    public Vector3 nockBottomLocal = new Vector3(0f, -0.605f, -0.170f);
    [Tooltip("시위가 쉬고 있을 때 가운데(활 로컬).")] public Vector3 restMidLocal = new Vector3(0.02f, 0.03f, -0.170f);
    [Tooltip("화살이 지나가는 줌통 옆 자리(활 로컬).")] public Vector3 arrowPassLocal = new Vector3(0.02f, 0.03f, 0f);
    public string holdParameter = "BowString";
    [Tooltip("플레이 중이 아닐 때 시위를 손에 걸어 둘지(편집 모드 확인용).")] public bool editorHold = true;
    [Tooltip("밖에서 시위 걸기를 막을 때 false(선원 CrewMember 가 대기 중에 끔 — 대기 클립엔 BowString 커브가 없어서).")] public bool allowHold = true;

    int _hash; bool _hasParam;

    void OnEnable()
    {
        if (animator == null) animator = GetComponentInParent<Animator>();
        _hash = Animator.StringToHash(holdParameter);
        _hasParam = false;
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var p in animator.parameters) if (p.nameHash == _hash) { _hasParam = true; break; }
    }

    void LateUpdate()
    {
        if (bow == null || segmentTop == null || segmentBottom == null) return;
        bool hold;
        if (Application.isPlaying && _hasParam && animator != null) hold = animator.GetFloat(_hash) >= 0.5f;
        else hold = editorHold;
        hold &= allowHold;

        Vector3 top = bow.TransformPoint(nockTopLocal), bot = bow.TransformPoint(nockBottomLocal);
        Vector3 mid = (hold && stringHand != null) ? stringHand.position : bow.TransformPoint(restMidLocal);
        Place(segmentTop, top, mid);
        Place(segmentBottom, bot, mid);

        if (arrow != null)
        {
            if (arrow.gameObject.activeSelf != hold) arrow.gameObject.SetActive(hold);
            if (hold)
            {
                Vector3 pass = bow.TransformPoint(arrowPassLocal);
                Vector3 dir = pass - mid;
                if (dir.sqrMagnitude > 1e-6f) arrow.SetPositionAndRotation(mid, Quaternion.LookRotation(dir.normalized, bow.up));
            }
        }
    }

    // 토막 메시는 로컬 +Y 로 길이 1 (원점 = 시작). 부모 배율이 1 이라고 본다.
    static void Place(Transform seg, Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; float len = d.magnitude;
        if (len < 1e-5f) return;
        seg.SetPositionAndRotation(a, Quaternion.FromToRotation(Vector3.up, d / len));
        Vector3 ps = seg.parent != null ? seg.parent.lossyScale : Vector3.one;
        seg.localScale = new Vector3(1f / ps.x, len / ps.y, 1f / ps.z);
    }
}
