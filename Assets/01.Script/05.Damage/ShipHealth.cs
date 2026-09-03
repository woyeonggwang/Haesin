using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배 한 척의 체력. 피해를 받고, 손상 단계를 알리고, 완파되면 가라앉는다.
///
/// 손상 단계는 체력 비율로 나눈다. 이 단계값을 ShipBurnVisual 이 받아
/// 선체 그을음과 연기·불의 세기를 정한다.
///
/// 자동 회복은 두 조건을 모두 만족할 때만 돈다.
///   1) 마지막 피격에서 regenDelay 초가 지났다
///   2) regenSafeDistance 안에 적대 진영의 배가 없다
/// 둘 중 하나라도 깨지면 회복이 멈추고 대기 시간이 다시 채워진다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ShipFaction))]
public class ShipHealth : MonoBehaviour
{
    [Header("체력")]
    [Tooltip("최대 체력.")]
    public float maxHealth = 100f;

    [Header("피해")]
    [Tooltip("체력이 낮을수록 피해가 커지는 정도. 0이면 항상 원래 피해 그대로. 1이면 빈사에서 2배까지 커진다.")]
    [Range(0f, 3f)]
    public float damageEscalation = 0f;

    [Header("자동 회복")]
    [Tooltip("자동 회복을 쓸지.")]
    public bool regenerates = true;
    [Tooltip("마지막 피격 후 회복이 시작되기까지의 시간(초).")]
    public float regenDelay = 10f;
    [Tooltip("초당 회복량.")]
    public float regenPerSecond = 5f;
    [Tooltip("이 거리 안에 적대 진영의 배가 있으면 전투 중으로 보고 회복하지 않는다(m).")]
    public float regenSafeDistance = 150f;
    [Tooltip("적이 있는지 확인하는 간격(초).")]
    public float threatCheckInterval = 0.5f;

    [Header("침몰")]
    [Tooltip("체력이 0이 되면 완파로 처리할지. 끄면 체력이 0에서 멈출 뿐 배는 계속 움직이고, 시간이 지나면 다시 회복한다. 플레이어는 아직 이쪽이다.")]
    public bool diesAtZeroHealth = true;
    [Tooltip("완파되면 가라앉을지. 플레이어는 꺼둔다.")]
    public bool sinkOnDeath = true;
    [Tooltip("가라앉는 데 걸리는 시간(초).")]
    public float sinkDuration = 6f;
    [Tooltip("해수면 아래로 내려가는 깊이(m).")]
    public float sinkDepth = 40f;
    [Tooltip("가라앉으면서 기우는 각도(도).")]
    public float sinkRollAngle = 55f;
    [Tooltip("다 가라앉은 뒤 오브젝트를 없앨지.")]
    public bool destroyAfterSink = true;

    [Header("손상 단계")]
    [Tooltip("단계가 바뀌는 체력 비율. 위에서부터 온전 / 경상 / 중상 / 빈사 의 경계.")]
    public float[] stageThresholds = new float[] { 0.75f, 0.5f, 0.25f };

    [Header("상태 (읽기 전용)")]
    public float currentHealth;
    [Tooltip("0 = 온전, 1 = 경상, 2 = 중상, 3 = 빈사.")]
    public int damageStage;
    [Tooltip("0(온전) ~ 1(완파) 의 연속값. 셰이더가 이 값을 쓴다.")]
    [Range(0f, 1f)] public float damage01;
    public bool isDead;
    public bool isSinking;
    [Tooltip("회복이 시작되기까지 남은 시간(초).")]
    public float regenCountdown;
    [Tooltip("지금 회복 중인지.")]
    public bool regenerating;
    [Tooltip("근처에 적이 있어 회복이 막혔는지.")]
    public bool threatNearby;

    /// <summary>체력이 바뀔 때. (현재, 최대)</summary>
    public System.Action<float, float> OnHealthChanged;
    /// <summary>손상 단계가 바뀔 때. (단계, 0~1 손상도)</summary>
    public System.Action<int, float> OnStageChanged;
    /// <summary>완파되는 순간.</summary>
    public System.Action OnDied;
    /// <summary>피해를 받은 순간. (피해량, 월드 좌표)</summary>
    public System.Action<float, Vector3> OnDamaged;

    /// <summary>살아있는 모든 배의 체력. 다른 시스템이 훑어볼 수 있게 둔다.</summary>
    public static readonly List<ShipHealth> All = new List<ShipHealth>();

    private ShipFaction _faction;
    private float _lastDamageTime = -999f;
    private float _nextThreatCheck;
    private float _sinkTimer;
    private Vector3 _sinkStartPos;
    private Quaternion _sinkStartRot;
    private Quaternion _sinkEndRot;

    public float HealthRatio { get { return maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth); } }

    void Awake()
    {
        _faction = GetComponent<ShipFaction>();
        currentHealth = maxHealth;
        RecomputeStage(true);
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (isSinking) { TickSink(); return; }
        if (isDead) return;

        TickRegen();
    }

    // ---------- 피해 ----------

    /// <summary>포탄 등이 이 배에 피해를 준다.</summary>
    public void TakeDamage(float amount, Vector3 worldPoint)
    {
        if (isDead || amount <= 0f) return;

        // 체력이 낮을수록 더 아프게. damageEscalation 이 0이면 그대로 들어간다.
        float weakness = 1f - HealthRatio;
        float scaled = amount * (1f + damageEscalation * weakness);

        currentHealth = Mathf.Max(0f, currentHealth - scaled);
        _lastDamageTime = Time.time;
        regenerating = false;

        if (OnDamaged != null) OnDamaged(scaled, worldPoint);
        if (OnHealthChanged != null) OnHealthChanged(currentHealth, maxHealth);
        RecomputeStage(false);

        if (currentHealth <= 0f && diesAtZeroHealth) Die();
    }

    /// <summary>즉시 회복시킨다. (디버그·아이템용)</summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (OnHealthChanged != null) OnHealthChanged(currentHealth, maxHealth);
        RecomputeStage(false);
    }

    void RecomputeStage(bool force)
    {
        float r = HealthRatio;
        damage01 = 1f - r;

        int stage = 0;
        if (stageThresholds != null)
        {
            for (int i = 0; i < stageThresholds.Length; i++)
                if (r < stageThresholds[i]) stage = i + 1;
        }

        if (force || stage != damageStage)
        {
            damageStage = stage;
            if (OnStageChanged != null) OnStageChanged(damageStage, damage01);
        }
        else if (OnStageChanged != null)
        {
            // 단계가 그대로여도 연속값은 계속 흘려보낸다 (셰이더가 부드럽게 따라오게).
            OnStageChanged(damageStage, damage01);
        }
    }

    // ---------- 회복 ----------

    void TickRegen()
    {
        if (!regenerates || currentHealth >= maxHealth)
        {
            regenerating = false;
            regenCountdown = 0f;
            return;
        }

        if (Time.time >= _nextThreatCheck)
        {
            _nextThreatCheck = Time.time + Mathf.Max(0.1f, threatCheckInterval);
            threatNearby = HasThreatNearby();
        }

        if (threatNearby)
        {
            // 적이 붙어 있으면 회복도 없고 대기 시간도 다시 채워진다.
            _lastDamageTime = Mathf.Max(_lastDamageTime, Time.time - regenDelay + 1f);
            regenerating = false;
            regenCountdown = regenDelay;
            return;
        }

        float since = Time.time - _lastDamageTime;
        regenCountdown = Mathf.Max(0f, regenDelay - since);
        if (regenCountdown > 0f) { regenerating = false; return; }

        regenerating = true;
        currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.deltaTime);
        if (OnHealthChanged != null) OnHealthChanged(currentHealth, maxHealth);
        RecomputeStage(false);
    }

    bool HasThreatNearby()
    {
        if (_faction == null || regenSafeDistance <= 0f) return false;

        float sqr = regenSafeDistance * regenSafeDistance;
        Vector3 p = transform.position;

        for (int i = 0; i < ShipFaction.All.Count; i++)
        {
            ShipFaction s = ShipFaction.All[i];
            if (s == null || !_faction.IsHostileTo(s)) continue;
            // 이미 가라앉는 중인 배는 위협이 아니다.
            ShipHealth h = s.GetComponent<ShipHealth>();
            if (h != null && (h.isDead || h.isSinking)) continue;

            if ((s.transform.position - p).sqrMagnitude <= sqr) return true;
        }
        return false;
    }

    // ---------- 완파 / 침몰 ----------

    void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHealth = 0f;
        RecomputeStage(true);
        if (OnDied != null) OnDied();

        if (!sinkOnDeath) return;   // 플레이어는 아직 가라앉지 않는다
        BeginSink();
    }

    void BeginSink()
    {
        isSinking = true;
        _sinkTimer = 0f;
        _sinkStartPos = transform.position;
        _sinkStartRot = transform.rotation;

        // 옆으로 기울며 뱃머리부터 들어가는 느낌
        float roll = Random.value < 0.5f ? sinkRollAngle : -sinkRollAngle;
        _sinkEndRot = _sinkStartRot * Quaternion.Euler(sinkRollAngle * 0.35f, 0f, roll);

        // 조종·부력·충돌을 모두 끊는다. 이제 이 배는 연출일 뿐이다.
        foreach (var b in GetComponents<MonoBehaviour>())
        {
            if (b == this) continue;
            string n = b.GetType().Name;
            if (n == "ShipHealth" || n == "ShipFaction" || n == "ShipBurnVisual") continue;
            b.enabled = false;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;

        // 항적과 뱃머리 파도는 더 이상 만들지 않는다.
        Transform wake = transform.Find("WakeFoam");
        if (wake != null) wake.gameObject.SetActive(false);
        Transform bow = transform.Find("BowWave");
        if (bow != null) bow.gameObject.SetActive(false);
    }

    void TickSink()
    {
        _sinkTimer += Time.deltaTime;
        float t = sinkDuration <= 0f ? 1f : Mathf.Clamp01(_sinkTimer / sinkDuration);

        // 처음엔 천천히 기울다가 물이 차면서 빨라진다.
        float fall = t * t;
        transform.position = _sinkStartPos + Vector3.down * (sinkDepth * fall);
        transform.rotation = Quaternion.Slerp(_sinkStartRot, _sinkEndRot, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.6f)));

        if (t >= 1f && destroyAfterSink) Destroy(gameObject);
    }
}
