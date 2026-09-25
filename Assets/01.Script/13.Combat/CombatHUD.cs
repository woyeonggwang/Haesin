using UnityEngine;

/// <summary>
/// 테스트용 전투 HUD (OnGUI). 아래: 플레이어 체력(빨강)·체간(노랑). 위: 적 체력·체간.
/// 적이 위험 공격을 시작하면 화면 가운데 '위험!' 표시, 판정 결과(튕겨내기·막기·피격…)를 잠깐 띄운다.
/// </summary>
public class CombatHUD : MonoBehaviour
{
    public SwordCombatant player;
    public SwordCombatant enemy;
    string _msg = ""; float _msgUntil; Color _msgColor = Color.white;
    string _peril = ""; float _perilUntil;
    Texture2D _white;

    void Start()
    {
        _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply();
        Hook();
    }

    void Hook()
    {
        foreach (var c in FindObjectsByType<SwordCombatant>(FindObjectsSortMode.None))
        {
            if (c.team == 0 && player == null) player = c;
            if (c.team != 0 && enemy == null) enemy = c;
        }
        if (player != null) player.OnCombatEvent += OnEvent;
        if (enemy != null) { enemy.OnCombatEvent += OnEvent; enemy.OnAttackStarted += OnEnemyAttack; }
    }

    void OnEvent(SwordCombatant who, string what)
    {
        if (who != player) return;
        _msg = what; _msgUntil = Time.time + 0.9f;
        _msgColor = what.Contains("튕겨") ? new Color(1f, 0.85f, 0.2f) : what.Contains("피격") ? new Color(1f, 0.4f, 0.3f) : what.Contains("결정타") ? Color.red : Color.white;
    }

    void OnEnemyAttack(SwordCombatant who, AttackData a)
    {
        if (!a.IsPerilous) return;
        _peril = a.kind == AttackKind.Thrust ? "위험!  찌르기 — 옆으로 회피" : "위험!  베어 넘기기 — 점프";
        _perilUntil = Time.time + a.windup + a.active;
    }

    void Bar(float x, float y, float w, float h, float t, Color bg, Color fg)
    {
        GUI.color = bg; GUI.DrawTexture(new Rect(x, y, w, h), _white);
        GUI.color = fg; GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(t), h), _white);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        float W = Screen.width, H = Screen.height;
        var style = new GUIStyle(GUI.skin.label); style.fontSize = 18; style.alignment = TextAnchor.MiddleLeft; style.normal.textColor = Color.white;
        if (player != null && player.stats != null)
        {
            float x = 40f, y = H - 90f, w = 420f;
            GUI.Label(new Rect(x, y - 26f, 300f, 24f), "플레이어  " + Mathf.CeilToInt(player.stats.health) + " / " + Mathf.CeilToInt(player.stats.maxHealth) + "   [" + player.state + "]", style);
            Bar(x, y, w, 16f, player.stats.Health01, new Color(0.1f, 0.1f, 0.1f, 0.8f), new Color(0.85f, 0.2f, 0.2f));
            Bar(x, y + 22f, w, 12f, player.stats.Posture01, new Color(0.1f, 0.1f, 0.1f, 0.8f), player.stats.IsBroken ? Color.white : new Color(0.95f, 0.75f, 0.2f));
            GUI.Label(new Rect(x + w + 10f, y + 18f, 200f, 24f), "체간", style);
        }
        if (enemy != null && enemy.stats != null)
        {
            float w = 520f, x = (W - w) * 0.5f, y = 60f;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(x, y - 28f, w, 24f), "적  " + Mathf.CeilToInt(enemy.stats.health) + " / " + Mathf.CeilToInt(enemy.stats.maxHealth) + "   [" + enemy.state + (enemy.state == SwordCombatant.State.Attack ? " " + enemy.currentAttackName : "") + "]", style);
            Bar(x, y, w, 14f, enemy.stats.Health01, new Color(0.1f, 0.1f, 0.1f, 0.8f), new Color(0.85f, 0.2f, 0.2f));
            Bar(x, y + 20f, w, 12f, enemy.stats.Posture01, new Color(0.1f, 0.1f, 0.1f, 0.8f), enemy.stats.IsBroken ? Color.white : new Color(0.95f, 0.75f, 0.2f));
            if (enemy.stats.IsBroken) { style.normal.textColor = Color.white; GUI.Label(new Rect(x, y + 36f, w, 26f), "체간 붕괴 — 지금 공격하면 결정타!", style); }
        }
        if (Time.time < _perilUntil)
        {
            var ps = new GUIStyle(GUI.skin.label); ps.fontSize = 44; ps.alignment = TextAnchor.MiddleCenter; ps.fontStyle = FontStyle.Bold; ps.normal.textColor = new Color(1f, 0.25f, 0.2f);
            GUI.Label(new Rect(0f, H * 0.22f, W, 60f), _peril, ps);
        }
        if (Time.time < _msgUntil)
        {
            var ms = new GUIStyle(GUI.skin.label); ms.fontSize = 30; ms.alignment = TextAnchor.MiddleCenter; ms.fontStyle = FontStyle.Bold; ms.normal.textColor = _msgColor;
            GUI.Label(new Rect(0f, H * 0.55f, W, 50f), _msg, ms);
        }
        var hs = new GUIStyle(GUI.skin.label); hs.fontSize = 14; hs.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        GUI.Label(new Rect(W - 460f, H - 180f, 440f, 170f), "좌클릭 연타 콤보(두 가지 자동 섞임)  |  좌클릭 꾹 강베기(다시 꾹 연계)\nShift 달리기 중 좌클릭: 달려 베기  |  회피 직후 좌클릭: 회피 베기  |  Esc 커서\n우클릭 막기 / 닿기 직전 누르면 튕겨내기\n칼: 적이 가까이 오거나 클릭하면 뽑고, 5초간 전투·입력이 없으면 넣음\nSpace 회피(이동키+) / 점프(이동키 없이)\n찌르기는 옆으로 회피, 베어 넘기기는 점프. 막으면 그대로 맞음\nT: 죽은 뒤 부활", hs);
    }
}
