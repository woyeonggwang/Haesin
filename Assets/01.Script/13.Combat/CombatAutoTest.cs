using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// (테스트 전용) 플레이어를 자동으로 조작해 판정을 검증한다. MCP 로 켜고 끈다.
/// mode: Parry = 적 공격 예비가 끝나기 직전 막기 누름 / Block = 계속 막기 / Dodge = 위험 공격에 회피·점프 / Attack = 계속 공격 / None
/// 판정 이벤트를 log 에 모은다.
/// </summary>
public class CombatAutoTest : MonoBehaviour
{
    public enum Mode { None, Parry, Block, Dodge, Attack, Script }
    [Tooltip("Script 모드: 쉼표로 구분한 행동. A=기본 콤보, B=두 번째 콤보, H=강베기, S=달려 베기, D=뒤로 회피, W=0.5초 대기. 실패하면(입력 창 밖) 2초까지 다시 시도.")] public string script = "";
    int _step; float _stepStart = -1f; float _waitUntil;
    public Mode mode = Mode.None;
    public SwordCombatant player, enemy;
    public List<string> log = new List<string>();
    float _pressAt = -1f; float _releaseAt = -1f; float _nextAttack; float _dodgeAt = -1f; AttackKind _dodgeKind;

    void OnEnable()
    {
        foreach (var c in FindObjectsByType<SwordCombatant>(FindObjectsSortMode.None)) { if (c.team == 0) player = c; else enemy = c; }
        if (enemy != null) { enemy.OnAttackStarted += OnEnemyAttack; enemy.OnCombatEvent += OnEvent; }
        if (player != null) { player.OnCombatEvent += OnEvent; player.OnAttackStarted += OnPlayerAttack; }
        _step = 0; _stepStart = -1f;
    }
    void OnDisable()
    {
        if (enemy != null) { enemy.OnAttackStarted -= OnEnemyAttack; enemy.OnCombatEvent -= OnEvent; }
        if (player != null) { player.OnCombatEvent -= OnEvent; player.OnAttackStarted -= OnPlayerAttack; }
    }

    void OnEvent(SwordCombatant who, string what) { log.Add(Time.time.ToString("F2") + " " + (who == player ? "P:" : "E:") + what + "  (P hp " + Mathf.RoundToInt(player.stats.health) + " post " + Mathf.RoundToInt(player.stats.posture) + " / E hp " + Mathf.RoundToInt(enemy.stats.health) + " post " + Mathf.RoundToInt(enemy.stats.posture) + ")"); }

    void OnPlayerAttack(SwordCombatant who, AttackData a) { log.Add(Time.time.ToString("F2") + " P attack: " + a.name + " [" + a.animState + "]"); }

    void TickScript()
    {
        if (string.IsNullOrEmpty(script) || Time.time < _waitUntil) return;
        var steps = script.Split(',');
        if (_step >= steps.Length) return;
        string s = steps[_step].Trim();
        if (player.HasQueued) return; // 이미 예약된 다음 타가 시작될 때까지 기다린다
        if (_stepStart < 0f) _stepStart = Time.time;
        bool ok = false;
        switch (s)
        {
            case "A": ok = player.TryAttack(); break;
            case "B": ok = player.TryComboB(); break;
            case "H": ok = player.TryHeavy(); break;
            case "S": ok = player.TrySprintAttack(); break;
            case "D": ok = player.TryDodge(-player.transform.forward); break;
            case "W": ok = true; _waitUntil = Time.time + 0.5f; break;
            default: ok = true; break;
        }
        if (ok || Time.time - _stepStart > 2f)
        {
            if (!ok) log.Add(Time.time.ToString("F2") + " step " + _step + " " + s + " FAILED");
            _step++; _stepStart = -1f;
            if (ok && s != "W") _waitUntil = Time.time + 0.12f;
        }
    }

    void OnEnemyAttack(SwordCombatant who, AttackData a)
    {
        log.Add(Time.time.ToString("F2") + " E attack: " + a.name);
        if (mode == Mode.Parry && !a.IsPerilous) { _pressAt = Time.time + Mathf.Max(0f, a.windup - 0.08f); _releaseAt = _pressAt + 0.4f; }
        // 사람처럼 공격이 닿기 직전(예비 끝 0.15 s 전)에 회피/점프
        if ((mode == Mode.Dodge || mode == Mode.Parry) && a.IsPerilous) { _dodgeAt = Time.time + Mathf.Max(0f, a.windup - 0.15f); _dodgeKind = a.kind; }
    }

    void Update()
    {
        if (player == null) return;
        if (mode == Mode.Block) player.SetBlock(true);
        if (_dodgeAt > 0f && Time.time >= _dodgeAt) { _dodgeAt = -1f; if (_dodgeKind == AttackKind.Sweep) player.TryJump(); else player.TryDodge(player.transform.right); }
        if (_pressAt > 0f && Time.time >= _pressAt) { _pressAt = -1f; player.PressBlock(); }
        if (_releaseAt > 0f && Time.time >= _releaseAt) { _releaseAt = -1f; player.SetBlock(false); }
        if (mode == Mode.Script) TickScript();
        if (mode == Mode.Attack && Time.time >= _nextAttack) { player.TryAttack(); _nextAttack = Time.time + 0.5f; }
    }
}
