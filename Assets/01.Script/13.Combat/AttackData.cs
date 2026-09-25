using UnityEngine;

/// <summary>공격 종류. Normal 은 막을 수 있고, Thrust(찌르기)·Sweep(베어 넘기기)은 '위험 공격' — 막으면 큰 피해, 회피/점프로 피해야 한다.</summary>
public enum AttackKind { Normal, Thrust, Sweep }

/// <summary>
/// 공격 한 동작의 데이터. 시간은 초. windup(예비) → active(맞는 구간) → recovery(후딜).
/// 연타(콤보)는 SwordCombatant.combo 배열의 순서.
/// </summary>
[System.Serializable]
public class AttackData
{
    public string name = "Slash";
    public AttackKind kind = AttackKind.Normal;
    [Tooltip("애니메이터 상태 이름.")] public string animState = "Attack1";
    [Tooltip("예비 동작(초). 이 동안 상대가 튕겨내기 타이밍을 잰다.")] public float windup = 0.25f;
    [Tooltip("맞는 구간(초). 시작 순간에 판정 1회.")] public float active = 0.15f;
    [Tooltip("후딜(초). 이 동안 다음 콤보 입력을 받는다.")] public float recovery = 0.3f;
    [Tooltip("체력 피해.")] public float damage = 15f;
    [Tooltip("맞았을 때 체간 피해.")] public float postureDamage = 20f;
    [Tooltip("막혔을 때 상대 체간 피해.")] public float blockPostureDamage = 10f;
    [Tooltip("사거리(m). 상대 중심까지.")] public float range = 1.9f;
    [Tooltip("판정 각도(도, 정면 기준 반각).")] public float arc = 60f;
    [Tooltip("공격 중 앞으로 나가는 거리(m, active 시작 시).")] public float lunge = 0.35f;
    [Tooltip("콤보 다음 타로 넘어가는 시각(초, 동작 시작부터). 0 이면 동작 끝(windup+active+recovery). 다음 타 클립의 첫 자세가 이 시각의 자세와 같다(ARPGSamurai Combo1→2 0.30 s, 2→3 0.48 s 를 상태 속도로 나눈 값).")] public float chainAt = 0f;
    [Tooltip("이 공격 상태로 넘어갈 때 크로스페이드(초).")] public float fadeIn = 0.06f;

    [Tooltip("이 공격에 맞은 쪽이 재생할 피격 상태(비우면 SwordCombatant.hitStates 중 무작위).")] public string hitState = "";
    [Tooltip("이 공격에 맞은 쪽의 경직(초). 0 이면 SwordCombatant.hitStun.")] public float hitStun = 0f;

    public float Total { get { return windup + active + recovery; } }
    /// <summary>예약된 다음 콤보가 시작되는 시각.</summary>
    public float ChainTime { get { return chainAt > 0f ? Mathf.Min(chainAt, Total) : Total; } }
    public bool IsPerilous { get { return kind != AttackKind.Normal; } }
}
