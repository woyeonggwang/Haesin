using System.Collections;
using UnityEngine;

/// <summary>
/// 칼싸움 효과음(SwordCombatant 옆에 붙인다, 플레이어·적 공용).
///   휘두르는 소리: 공격 시작(OnAttackStarted) 때 예약 — 소리의 가장 큰 순간(swingPeak)이 맞는 순간(windup)과 겹치게 늦춰 재생. 볼륨 swingVolume.
///   칼 부딪히는 소리("챙"): 이 사람이 막거나 튕겨냈을 때(OnCombatEvent "막기"/"튕겨내기") clashClips 중 무작위(바로 앞과 다른 것). 볼륨 clashVolume.
///   칼 뽑기·넣기 소리: WeaponHolder.OnSwordMoved 때 drawClips / sheatheClips 중 무작위. (넣기 소리는 0.26초에 칼집 닿는 딸깍)
/// 음원: Assets/02.Resources/01.Asset/Audio/Sword/ (StarNinjas, OpenGameArt CC0 — LICENSE.txt).
/// 재생은 OneShotAudio(3D, Linear 감쇠).
/// </summary>
[RequireComponent(typeof(SwordCombatant))]
public class SwordAudio : MonoBehaviour
{
    [Header("휘두르기")]
    public AudioClip[] swingClips = new AudioClip[0];
    [Range(0f, 1f)] public float swingVolume = 0.1f;
    [Tooltip("휘두르는 소리에서 가장 큰 순간(초). 재생 시각 = 공격 windup − 이 값(0 보다 작으면 바로).")] public float swingPeak = 0.28f;

    [Header("칼 부딪힘(막기·튕겨내기)")]
    public AudioClip[] clashClips = new AudioClip[0];
    [Range(0f, 1f)] public float clashVolume = 0.6f;

    [Header("칼 뽑기·넣기 (WeaponHolder.OnSwordMoved — 검이 손↔칼집으로 옮겨지는 순간)")]
    public AudioClip[] drawClips = new AudioClip[0];
    [Range(0f, 1f)] public float drawVolume = 0.1f;
    public AudioClip[] sheatheClips = new AudioClip[0];
    [Range(0f, 1f)] public float sheatheVolume = 0.1f;

    [Header("공통")]
    public float minDistance = 2f;
    public float maxDistance = 40f;
    [Tooltip("재생마다 피치를 이 범위에서 무작위로(같은 소리 반복 느낌 줄이기).")] public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    SwordCombatant _sc; WeaponHolder _weapon;
    int _lastSwing = -1, _lastClash = -1, _lastDraw = -1, _lastSheathe = -1;

    void Awake() { _sc = GetComponent<SwordCombatant>(); _weapon = GetComponent<WeaponHolder>(); }

    void OnEnable()
    {
        if (_sc == null) _sc = GetComponent<SwordCombatant>();
        _sc.OnAttackStarted += OnAttackStarted;
        _sc.OnCombatEvent += OnCombatEvent;
        if (_weapon == null) _weapon = GetComponent<WeaponHolder>();
        if (_weapon != null) _weapon.OnSwordMoved += OnSwordMoved;
    }

    void OnDisable()
    {
        if (_sc == null) return;
        _sc.OnAttackStarted -= OnAttackStarted;
        _sc.OnCombatEvent -= OnCombatEvent;
        if (_weapon != null) _weapon.OnSwordMoved -= OnSwordMoved;
    }

    void OnSwordMoved(bool drawn)
    {
        if (drawn) Play(drawClips, ref _lastDraw, drawVolume);
        else Play(sheatheClips, ref _lastSheathe, sheatheVolume);
    }

    void OnAttackStarted(SwordCombatant who, AttackData a)
    {
        if (swingClips == null || swingClips.Length == 0 || a == null) return;
        StartCoroutine(SwingAt(a, Mathf.Max(0f, a.windup - swingPeak)));
    }

    IEnumerator SwingAt(AttackData a, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (_sc.CurrentAttack != a) yield break; // 그 사이 맞거나 다른 동작으로 바뀜
        Play(swingClips, ref _lastSwing, swingVolume);
    }

    void OnCombatEvent(SwordCombatant who, string what)
    {
        if (who != _sc || string.IsNullOrEmpty(what)) return;
        if (what == "막기" || what.StartsWith("튕겨내기")) Play(clashClips, ref _lastClash, clashVolume);
    }

    void Play(AudioClip[] clips, ref int last, float volume)
    {
        if (clips == null || clips.Length == 0 || volume <= 0f) return;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last) i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        last = i;
        Vector3 pos = _weapon != null && _weapon.sword != null ? _weapon.sword.position : transform.position + Vector3.up * 1.2f;
        OneShotAudio.Play(clips[i], pos, volume, minDistance, maxDistance, Random.Range(pitchRange.x, pitchRange.y));
    }
}
