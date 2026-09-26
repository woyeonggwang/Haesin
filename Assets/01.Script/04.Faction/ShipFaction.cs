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

    /// <summary>
    /// 상대가 나의 적인가. 관군 ↔ 일본군은 늘 적. 플레이어(해적)와 두 진영 사이는 우호도(ReputationSystem)로 정한다 —
    /// 중립이면 서로 적이 아니다(예전엔 "나와 다른 진영이면 적" 이라 관군·일본군 모두 늘 플레이어를 공격했다).
    /// </summary>
    public bool IsHostileTo(ShipFaction other)
    {
        if (other == null || other == this || other.faction == faction) return false;
        if (faction == Faction.Pirate) return ReputationSystem.PlayerHostileWith(other.faction);
        if (other.faction == Faction.Pirate) return ReputationSystem.PlayerHostileWith(faction);
        return true;
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
            if (!IsTargetableBy(self, s)) continue;

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
    /// 표적으로 삼을 수 있는 배인지. 완파·침몰 중인 배와 베이스 안전지대(BaseSafeZone) 안의 배는 표적이 아니다.
    /// (침몰 중인 플레이어를 계속 때리지 않게, 선착장에 정박한 플레이어를 적이 쫓아오지 않게.)
    /// </summary>
    public static bool IsTargetable(ShipFaction s)
    {
        if (s == null) return false;
        ShipHealth h = s.GetComponent<ShipHealth>();
        if (h != null && (h.isDead || h.isSinking)) return false;
        if (BaseSafeZone.Contains(s.transform.position)) return false;
        return true;
    }

    /// <summary>IsTargetable + 공격자 기준 제한: 다른 진영의 영해(FactionTerritory) 안에 있는 배는 표적이 아니다.</summary>
    public static bool IsTargetableBy(ShipFaction attacker, ShipFaction s)
    {
        if (!IsTargetable(s)) return false;
        if (attacker != null && FactionTerritory.ProtectsFrom(attacker.faction, s.transform.position)) return false;
        return true;
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
