using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배 한 척의 선원 목록. 갑판 위 자식 오브젝트 'Crew' 아래의 CrewMember 들을 모은다.
/// 선원 수는 배 크기에 비례해 에디터에서 미리 세워 둔다(판옥선 10, 관군 판옥선 10, 안택선 12, 세키부네 4).
/// 스포너가 배를 복제하면 선원도 자식이라 함께 복제된다.
/// </summary>
public class ShipCrew : MonoBehaviour
{
    [Tooltip("이 배의 선원들. Awake 에서 자식의 CrewMember 를 자동으로 모은다.")]
    public List<CrewMember> members = new List<CrewMember>();

    [Tooltip("영입 가능한 최대 선원 수.")]
    public int maxCrew = 16;
    [Tooltip("영입한 선원이 설 자리(배 로컬, x=우현, y=갑판 높이(0 이면 기존 선원 평균), z=선수). 순서대로 채운다.")]
    public Vector3[] recruitSpots = new Vector3[] {
        new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, 6f), new Vector3(-1.5f, 0f, -8f), new Vector3(1.5f, 0f, -8f), new Vector3(-1.5f, 0f, -3f), new Vector3(-1.5f, 0f, 3f) };
    int _recruited;

    public int Count { get { return members.Count; } }
    public bool HasFreeSlot { get { return members.Count < maxCrew && _recruited < recruitSpots.Length; } }

    /// <summary>선원 한 명을 recruitSpots 의 다음 자리에 추가한다(기존 선원 큐브를 복제). 성공하면 true.</summary>
    public bool AddRecruit()
    {
        if (!HasFreeSlot || members.Count == 0) return false;
        CrewMember template = members[0];
        Transform holder = template.transform.parent;
        Vector3 spot = recruitSpots[_recruited];
        if (spot.y <= 0f) { float sum = 0f; for (int i = 0; i < members.Count; i++) sum += members[i].transform.localPosition.y; spot.y = sum / members.Count; }
        var go = Instantiate(template.gameObject, holder);
        go.name = "Crew_" + (members.Count + 1);
        go.transform.localPosition = spot;
        go.transform.localRotation = template.transform.localRotation;
        go.transform.localScale = template.transform.localScale;
        _recruited++;
        Collect();
        return true;
    }

    void Awake()
    {
        Collect();
    }

    /// <summary>자식 계층에서 CrewMember 를 다시 모은다(에디터에서 큐브를 추가·삭제한 뒤 호출).</summary>
    public void Collect()
    {
        members.Clear();
        GetComponentsInChildren<CrewMember>(true, members);
        for (int i = 0; i < members.Count; i++)
        {
            members[i].ship = this;
            members[i].slotIndex = i;
        }
    }
}
