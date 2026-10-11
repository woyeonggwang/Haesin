using UnityEngine;

/// <summary>
/// 체력 + 체간(자세) 게이지 (세키로식).
///   - 체간은 막기·피격·튕겨내기로 차고, 마지막 피격 후 postureRegenDelay 가 지나면 초당 postureRegen 만큼 빠진다.
///   - 다 차면 IsBroken(brokenDuration 초 경직) → 이 동안 맞는 공격은 결정타(즉사).
/// 값은 인스펙터에서 조절.
/// </summary>
public class CombatStats : MonoBehaviour
{
    [Tooltip("시험용 무적(체력만, 2026-10-06 사용자 요청): 켜면 체력이 줄지 않고 결정타로도 죽지 않음. 체간은 그대로 차고 무너짐.")]
    public bool invulnerableHealth;
    public float maxHealth = 100f;
    public float health = 100f;
    public float maxPosture = 100f;
    public float posture = 0f;
    [Tooltip("마지막 체간 피해 후 회복이 시작되기까지(초).")] public float postureRegenDelay = 3f;
    [Tooltip("초당 체간 회복량.")] public float postureRegen = 20f;
    [Tooltip("체간이 무너졌을 때 경직 시간(초). 이 안에 결정타를 맞으면 즉사.")] public float brokenDuration = 3f;
    [Tooltip("체력이 낮을수록 체간 회복이 느려진다(세키로). 0 이면 항상 같은 속도.")]
    [Range(0f, 1f)] public float lowHealthRegenPenalty = 0.5f;

    public bool IsDead { get { return health <= 0f; } }
    public bool IsBroken { get { return _brokenUntil > Time.time; } }
    public float BrokenRemaining { get { return Mathf.Max(0f, _brokenUntil - Time.time); } }
    public float Health01 { get { return maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f; } }
    public float Posture01 { get { return maxPosture > 0f ? Mathf.Clamp01(posture / maxPosture) : 0f; } }

    public System.Action OnPostureBroken;
    public System.Action OnDied;
    public System.Action<float> OnDamaged;

    float _lastPostureHitTime = -999f;
    float _brokenUntil = -1f;

    void Update()
    {
        if (IsDead) return;
        if (IsBroken) { posture = maxPosture; return; }
        if (posture > 0f && Time.time - _lastPostureHitTime >= postureRegenDelay)
        {
            float regen = postureRegen * Mathf.Lerp(1f, 1f - lowHealthRegenPenalty, 1f - Health01);
            posture = Mathf.Max(0f, posture - regen * Time.deltaTime);
        }
    }

    public void ResetAll() { health = maxHealth; posture = 0f; _brokenUntil = -1f; _lastPostureHitTime = -999f; }

    public void Damage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        if (invulnerableHealth) { if (OnDamaged != null) OnDamaged(0f); return; }
        health = Mathf.Max(0f, health - amount);
        if (OnDamaged != null) OnDamaged(amount);
        if (health <= 0f && OnDied != null) OnDied();
    }

    /// <summary>체간 피해. 다 차면 무너진다. 무너진 상태에서는 무시.</summary>
    public void AddPosture(float amount)
    {
        if (IsDead || IsBroken || amount <= 0f) return;
        _lastPostureHitTime = Time.time;
        posture = Mathf.Min(maxPosture, posture + amount);
        if (posture >= maxPosture)
        {
            _brokenUntil = Time.time + brokenDuration;
            if (OnPostureBroken != null) OnPostureBroken();
        }
    }

    /// <summary>경직이 끝나면 체간을 비운다 (세키로: 무너진 뒤 회복).</summary>
    public void EndBroken() { _brokenUntil = -1f; posture = 0f; _lastPostureHitTime = Time.time; }

    /// <summary>결정타: 즉사.</summary>
    public void Deathblow() { if (invulnerableHealth) return; health = 0f; if (OnDied != null) OnDied(); }
}
