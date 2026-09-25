using UnityEngine;

/// <summary>
/// 선원 한 명. 지금은 갑판 위 임시 큐브(모델링 제작 중)이며 ShipCrew 가 목록으로 관리한다.
/// 추후 역할(노잡이/포수/사수), 사기, 전투 등을 여기에 붙인다.
/// </summary>
public class CrewMember : MonoBehaviour
{
    [Tooltip("소속 배. ShipCrew 가 채운다.")]
    public ShipCrew ship;
    [Tooltip("이 선원이 서 있는 갑판 자리 번호 (ShipCrew.slots 인덱스).")]
    public int slotIndex;
}
