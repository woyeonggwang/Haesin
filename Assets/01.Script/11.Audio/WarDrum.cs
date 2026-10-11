using UnityEngine;

/// <summary>
/// 전투 북소리(2026-10-06 사용자 지시 "적이 근처에 있으면 북소리, 가까이 있을수록 빠르게"). 씬에 하나(2D 소리).
///   배를 타고 있을 때: 플레이어 배(ShipFaction)에서 가장 가까운 적대 배(ShipFaction.FindNearestHostile — 우호도·영해 규칙 그대로).
///   걸어 다닐 때: 덤비는 육상 병사(SoldierAI.Hostiles) 중 가장 가까운 것.
///   거리 farDistance → nearDistance 사이에서 북 간격 slowInterval → fastInterval, 볼륨 farVolume → nearVolume. 4 박마다 한 번 강박.
///   적이 없어지면 그 박자에서 멈춤. 음원 Audio/Drum/war_drum_*.wav(합성).
/// </summary>
public class WarDrum : MonoBehaviour
{
    public AudioClip[] beatClips = new AudioClip[0];
    [Tooltip("강박(4 박마다) 음원. 비우면 beatClips 를 크게.")] public AudioClip[] accentClips = new AudioClip[0];
    [Header("배 — 거리(m)")]
    public float farDistance = 300f;
    public float nearDistance = 40f;
    [Header("걸을 때 — 거리(m)")]
    public float footFarDistance = 40f;
    public float footNearDistance = 4f;
    [Header("박자·볼륨")]
    [Tooltip("멀 때 북 간격(초).")] public float slowInterval = 1.6f;
    [Tooltip("가까울 때 북 간격(초).")] public float fastInterval = 0.35f;
    [Range(0f, 1f)] public float farVolume = 0.08f;
    [Range(0f, 1f)] public float nearVolume = 0.3f;

    [Header("읽기 전용")]
    public float enemyDistance = -1f;
    public float currentInterval;

    AudioSource _src; ShipFaction _playerShip; BoardingSystem _boarding; PlayerCombatController _pcc;
    float _nextScan, _nextBeat; int _beat; float _closeness;

    void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false; _src.spatialBlend = 0f; _src.loop = false;
    }

    void Update()
    {
        if (Time.time >= _nextScan) { _nextScan = Time.time + 0.25f; Scan(); }
        if (_closeness < 0f) { _beat = 0; return; }
        currentInterval = Mathf.Lerp(slowInterval, fastInterval, _closeness);
        if (Time.time < _nextBeat) return;
        _nextBeat = Time.time + currentInterval;
        bool accent = _beat % 4 == 0; _beat++;
        var set = accent && accentClips != null && accentClips.Length > 0 ? accentClips : beatClips;
        if (set == null || set.Length == 0) return;
        var clip = set[Random.Range(0, set.Length)];
        float vol = Mathf.Lerp(farVolume, nearVolume, _closeness) * (accent && (accentClips == null || accentClips.Length == 0) ? 1.3f : 1f);
        _src.pitch = Random.Range(0.97f, 1.03f);
        if (clip != null) _src.PlayOneShot(clip, vol);
    }

    void Scan()
    {
        if (_boarding == null) _boarding = FindFirstObjectByType<BoardingSystem>();
        bool onFoot = _boarding != null && _boarding.IsOnFoot;
        _closeness = -1f; enemyDistance = -1f;
        if (onFoot)
        {
            if (_pcc == null) _pcc = FindFirstObjectByType<PlayerCombatController>();
            if (_pcc == null) return;
            float best = float.MaxValue;
            foreach (var h in SoldierAI.Hostiles)
                if (h != null && !h.IsDead && h.isActiveAndEnabled) { float d = Vector3.Distance(h.transform.position, _pcc.transform.position); if (d < best) best = d; }
            if (best <= footFarDistance) { enemyDistance = best; _closeness = Mathf.InverseLerp(footFarDistance, footNearDistance, best); }
            return;
        }
        if (_playerShip == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerShip = p.GetComponent<ShipFaction>();
            if (_playerShip == null) return;
        }
        var e = ShipFaction.FindNearestHostile(_playerShip, farDistance);
        if (e == null) return;
        float dist = Vector3.Distance(e.transform.position, _playerShip.transform.position);
        if (dist > farDistance) return;
        enemyDistance = dist;
        _closeness = Mathf.InverseLerp(farDistance, nearDistance, dist);
    }
}
