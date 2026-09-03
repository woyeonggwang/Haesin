using System.Collections.Generic;
using UnityEngine;

/// <summary>배가 속한 진영.</summary>
public enum Faction
{
    /// <summary>플레이어. 해적 판옥선.</summary>
    Pirate,
    /// <summary>관군. 판옥선.</summary>
    Navy,
    /// <summary>일본군. 세키부네 / 안택선.</summary>
    Japanese
}

/// <summary>
/// 배 하나의 진영표이자, 배들이 서로를 찾는 창구.
///
/// 진영이 셋이 되면서 "Player 태그면 적" 이라는 판단이 더 이상 성립하지 않는다.
/// (관군은 플레이어도 왜군도 적이고, 왜군은 플레이어도 관군도 적이다)
/// 그래서 누가 누구의 적인지를 여기 한 곳에서만 정한다. 기본 규칙은
/// "나와 다른 진영이면 적" 이다.
///
/// 배들이 매 프레임 FindObjectsByType 을 도는 것을 막기 위해
/// 활성/비활성 시점에 정적 목록(All)에 스스로 등록한다.
/// </summary>
[DisallowMultipleComponent]
public class ShipFaction : MonoBehaviour
{
    [Tooltip("이 배가 속한 진영.")]
    public Faction faction = Faction.Japanese;

    [Tooltip("적이 조준할 지점. 비우면 이 오브젝트의 위치를 쓴다.")]
    public Transform aimPoint;

    /// <summary>씬에서 활성 상태인 모든 배.</summary>
    public static readonly List<ShipFaction> All = new List<ShipFaction>();

    private Rigidbody _rb;

    /// <summary>이 배의 강체. 표적 예측(리드 사격)에 쓴다. 없으면 null.</summary>
    public Rigidbody Body { get { return _rb; } }

    /// <summary>적이 조준해야 할 월드 위치.</summary>
    public Vector3 AimPosition { get { return aimPoint != null ? aimPoint.position : transform.position; } }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    /// <summary>상대가 나의 적인가.</summary>
    public bool IsHostileTo(ShipFaction other)
    {
        return other != null && other != this && other.faction != faction;
    }

    /// <summary>
    /// self 기준 maxRange 안에서 가장 가까운 적대 진영의 배를 찾는다.
    /// maxRange 가 0 이하이면 거리 제한 없이 찾는다. 없으면 null.
    /// </summary>
    public static ShipFaction FindNearestHostile(ShipFaction self, float maxRange)
    {
        if (self == null) return null;

        ShipFaction best = null;
        float bestSqr = maxRange > 0f ? maxRange * maxRange : float.MaxValue;
        Vector3 p = self.transform.position;

        for (int i = 0; i < All.Count; i++)
        {
            ShipFaction s = All[i];
            if (s == null || !self.IsHostileTo(s)) continue;

            float d = (s.transform.position - p).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = s;
            }
        }
        return best;
    }

    /// <summary>
    /// 맞은 오브젝트의 진영을 찾는다.
    /// 포탄은 보통 선체가 아니라 자식 콜라이더에 맞으므로 부모까지 거슬러 올라간다.
    /// </summary>
    public static ShipFaction Of(GameObject go)
    {
        if (go == null) return null;
        return go.GetComponentInParent<ShipFaction>();
    }
}
