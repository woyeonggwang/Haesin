using UnityEngine;

/// <summary>
/// 플레이어 캐릭터(외형). 메인 씬에서는 Haesin_Player(Haesin_Base 모델) 루트에 붙는다. 피벗 = 발.
/// 상태
///   Aboard : 배에 타 있음. 배 후미의 seat(CharacterSeat) 에 붙어 함께 움직이고, 조종은 MoveControl 이 그대로 한다.
///            배 위에서는 칼싸움을 하지 않으므로(설계 결정) onFootOnly 컴포넌트(PlayerCombatController 등)를 끄고 칼을 넣는다.
///   OnFoot : 하차 상태. BoardingSystem 이 외형을 OnFootRoot(CharacterController + OnFootController) 아래로 옮긴 뒤 SetOnFoot() 을 부른다.
/// 예전 임시 큐브는 피벗이 가운데라 standOffset (0, 0.85, 0) 이었음 → 모델(피벗 = 발)은 0.
/// </summary>
public class PlayerCharacter : MonoBehaviour
{
    public enum State { Aboard, OnFoot }

    [Tooltip("현재 상태.")]
    public State state = State.Aboard;
    [Tooltip("탑승 중 캐릭터가 서 있을 자리(배의 자식). 비워두면 부모에서 'CharacterSeat' 를 찾는다.")]
    public Transform seat;
    [Tooltip("seat / OnFootRoot 기준 캐릭터 로컬 오프셋. 모델은 피벗이 발이므로 0.")]
    public Vector3 standOffset = Vector3.zero;
    [Tooltip("하차(OnFoot) 중에만 켜는 컴포넌트. 배 위에서는 꺼 둔다(칼싸움 입력 등).")]
    public Behaviour[] onFootOnly;

    void Awake()
    {
        if (seat == null && transform.parent != null)
        {
            var s = transform.parent.Find("CharacterSeat");
            if (s != null) seat = s;
            else if (transform.parent.name == "CharacterSeat") seat = transform.parent;
        }
        if (state == State.Aboard) Board(seat);
    }

    /// <summary>배에 탄다. seat 아래로 들어가 배와 함께 움직인다.</summary>
    public void Board(Transform newSeat)
    {
        if (newSeat == null) return;
        seat = newSeat;
        transform.SetParent(seat, false);
        transform.localPosition = standOffset;
        transform.localRotation = Quaternion.identity;
        state = State.Aboard;
        SetOnFootComponents(false);
        // 배 위에서는 칼을 넣은 자세로 (자동 뽑기·넣기는 onFootOnly 의 PlayerCombatController 가 하므로 여기서 즉시 넣음)
        var wh = GetComponent<WeaponHolder>();
        if (wh != null && wh.IsDrawn) wh.Sheathe();
        var anim = GetComponent<Animator>();
        if (anim != null && anim.isActiveAndEnabled) anim.SetFloat("Speed", 0f);
    }

    /// <summary>하차 상태로 전환(외형을 OnFootRoot 아래로 옮긴 뒤 BoardingSystem 이 부른다).</summary>
    public void SetOnFoot()
    {
        state = State.OnFoot;
        SetOnFootComponents(true);
    }

    void SetOnFootComponents(bool on)
    {
        if (onFootOnly == null) return;
        for (int i = 0; i < onFootOnly.Length; i++) if (onFootOnly[i] != null) onFootOnly[i].enabled = on;
    }
}
