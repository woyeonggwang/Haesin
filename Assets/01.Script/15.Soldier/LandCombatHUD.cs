using UnityEngine;

/// <summary>
/// 메인 씬 육상 칼싸움 HUD(OnGUI, 임시 — 2026-10-06). CharacterTest 의 CombatHUD 와 같은 그림.
///   왼쪽 아래: 플레이어 체력(빨강)·체간(노랑) — 걸어 다니는 중이고 (덤비는 병사가 있거나 다쳤거나 체간이 찼을 때).
///   위 가운데: 플레이어 표적이 덤비는 병사면 이름·체력·체간.
///   덤비는 병사가 위험 공격을 시작하면 '위험!', 플레이어 판정 결과(튕겨내기·막기·피격…)를 잠깐.
///   쓰러지면 "쓰러졌습니다 — 배에서 깨어납니다".
/// </summary>
public class LandCombatHUD : MonoBehaviour
{
    public SwordCombatant player;
    [Tooltip("덤비는 병사가 이 거리(m) 안이면 전투 중으로 보고 막대를 보임.")] public float showRange = 20f;

    string _msg = ""; float _msgUntil; Color _msgColor = Color.white;
    string _peril = ""; float _perilUntil;
    Texture2D _white; SwordCombatant _hooked; PlayerLandDeath _death;

    void OnEnable() { SoldierAI.OnSoldierAttackStarted += OnSoldierAttack; }
    void OnDisable() { SoldierAI.OnSoldierAttackStarted -= OnSoldierAttack; if (_hooked != null) _hooked.OnCombatEvent -= OnEvent; _hooked = null; }

    void Resolve()
    {
        if (player == null)
        {
            var pcc = FindFirstObjectByType<PlayerCombatController>();
            if (pcc != null) player = pcc.combatant != null ? pcc.combatant : pcc.GetComponent<SwordCombatant>();
        }
        if (player != null && _hooked != player)
        {
            if (_hooked != null) _hooked.OnCombatEvent -= OnEvent;
            _hooked = player; player.OnCombatEvent += OnEvent;
            _death = player.GetComponent<PlayerLandDeath>();
        }
    }

    void OnEvent(SwordCombatant who, string what)
    {
        if (who != player) return;
        _msg = what; _msgUntil = Time.time + 0.9f;
        _msgColor = what.Contains("튕겨") ? new Color(1f, 0.85f, 0.2f) : what.Contains("피격") ? new Color(1f, 0.4f, 0.3f) : what.Contains("결정타") ? Color.red : Color.white;
    }

    void OnSoldierAttack(SoldierAI who, AttackData a)
    {
        if (a == null || !a.IsPerilous || player == null) return;
        if ((who.transform.position - player.transform.position).sqrMagnitude > 64f) return;
        _peril = a.kind == AttackKind.Thrust ? "위험!  찌르기 — 옆으로 회피" : "위험!  베어 넘기기 — 점프";
        _perilUntil = Time.time + a.windup + a.active;
    }

    bool OnFoot()
    {
        if (player == null || !player.isActiveAndEnabled) return false;
        var m = player.GetComponentInParent<OnFootController>();
        return m != null && m.isActiveAndEnabled;
    }

    bool AnyHostileNear()
    {
        float r2 = showRange * showRange;
        foreach (var h in SoldierAI.Hostiles)
            if (h != null && !h.IsDead && (h.transform.position - player.transform.position).sqrMagnitude < r2) return true;
        return false;
    }

    void Bar(float x, float y, float w, float h, float t, Color bg, Color fg)
    {
        GUI.color = bg; GUI.DrawTexture(new Rect(x, y, w, h), _white);
        GUI.color = fg; GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(t), h), _white);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        Resolve();
        if (player == null || player.stats == null) return;
        bool dead = player.state == SwordCombatant.State.Dead;
        if (!OnFoot() && !(dead && _death != null && _death.Pending)) return;
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        float W = Screen.width, H = Screen.height;
        var st = new GUIStyle(GUI.skin.label); st.fontSize = 18; st.alignment = TextAnchor.MiddleLeft; st.normal.textColor = Color.white;

        bool show = dead || AnyHostileNear() || player.stats.health < player.stats.maxHealth - 0.01f || player.stats.posture > 0.5f;
        if (show)
        {
            float x = 40f, y = H - 150f, w = 320f;
            GUI.Label(new Rect(x, y - 26f, 320f, 24f), "체력  " + Mathf.CeilToInt(player.stats.health) + " / " + Mathf.CeilToInt(player.stats.maxHealth), st);
            Bar(x, y, w, 14f, player.stats.Health01, new Color(0.1f, 0.1f, 0.1f, 0.8f), new Color(0.85f, 0.2f, 0.2f));
            Bar(x, y + 20f, w, 10f, player.stats.Posture01, new Color(0.1f, 0.1f, 0.1f, 0.8f), player.stats.IsBroken ? Color.white : new Color(0.95f, 0.75f, 0.2f));
        }

        // 표적
        var tgt = player.target != null ? player.target.GetComponent<SwordCombatant>() : null;
        if (!dead && tgt != null && !tgt.calm && tgt.stats != null && tgt.state != SwordCombatant.State.Dead)
        {
            var ai = tgt.GetComponentInParent<SoldierAI>();
            string nm = ai != null ? ai.displayName : "적";
            float w = 440f, x = (W - w) * 0.5f, y = 60f;
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(x, y - 28f, w, 24f), nm + "  " + Mathf.CeilToInt(tgt.stats.health) + " / " + Mathf.CeilToInt(tgt.stats.maxHealth), st);
            Bar(x, y, w, 12f, tgt.stats.Health01, new Color(0.1f, 0.1f, 0.1f, 0.8f), new Color(0.85f, 0.2f, 0.2f));
            Bar(x, y + 18f, w, 10f, tgt.stats.Posture01, new Color(0.1f, 0.1f, 0.1f, 0.8f), tgt.stats.IsBroken ? Color.white : new Color(0.95f, 0.75f, 0.2f));
            if (tgt.stats.IsBroken) GUI.Label(new Rect(x, y + 32f, w, 26f), "체간 붕괴 — 지금 공격하면 결정타!", st);
        }

        if (dead)
        {
            var ds = new GUIStyle(GUI.skin.label); ds.fontSize = 34; ds.alignment = TextAnchor.MiddleCenter; ds.fontStyle = FontStyle.Bold; ds.normal.textColor = new Color(0.9f, 0.2f, 0.15f);
            GUI.Label(new Rect(0f, H * 0.4f, W, 50f), _death != null && _death.Pending ? "쓰러졌습니다 — 배에서 깨어납니다" : "쓰러졌습니다", ds);
            return;
        }
        if (Time.time < _perilUntil)
        {
            var ps = new GUIStyle(GUI.skin.label); ps.fontSize = 40; ps.alignment = TextAnchor.MiddleCenter; ps.fontStyle = FontStyle.Bold; ps.normal.textColor = new Color(1f, 0.25f, 0.2f);
            GUI.Label(new Rect(0f, H * 0.22f, W, 60f), _peril, ps);
        }
        if (Time.time < _msgUntil)
        {
            var ms = new GUIStyle(GUI.skin.label); ms.fontSize = 28; ms.alignment = TextAnchor.MiddleCenter; ms.fontStyle = FontStyle.Bold; ms.normal.textColor = _msgColor;
            GUI.Label(new Rect(0f, H * 0.55f, W, 50f), _msg, ms);
        }
    }
}
