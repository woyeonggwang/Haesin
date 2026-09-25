using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 전체화면 메뉴(함선 Y / 인벤토리 I)가 열릴 때 게임을 멈추는 공용 로직. MapSystem.SetOpen 과 같은 규칙:
///   Time.timeScale 0, 모든 WaterSurface.timeMultiplier 0(파도 정지), AudioListener.pause,
///   CameraFollow/CannonControl/WeatherTestKeys 끔, 커서 표시.
/// 닫을 때 컴포넌트·커서 복구는 한 프레임 뒤(같은 프레임의 ESC 를 CameraFollow 가 받아 커서를 토글하므로) — 
/// 그래서 Tick() 을 매 프레임 불러줘야 한다(각 메뉴의 Update 에서).
/// 한 번에 하나의 메뉴만 소유할 수 있다(Owner).
/// </summary>
public static class GameMenuPause
{
    public static Object Owner { get; private set; }
    public static bool IsPaused { get { return Owner != null; } }

    public static string[] disableWhileOpen = { "CameraFollow", "CannonControl", "WeatherTestKeys", "PlayerOrbitCamera", "PlayerCombatController" };

    static float _prevTimeScale = 1f;
    static readonly List<KeyValuePair<WaterSurface, float>> _waters = new List<KeyValuePair<WaterSurface, float>>();
    static readonly List<MonoBehaviour> _disabled = new List<MonoBehaviour>();
    static bool _prevCursorVisible; static CursorLockMode _prevCursorLock;
    static int _restoreFrame = -1;

    /// <summary>지도(MapSystem)나 다른 메뉴가 열려 있으면 false.</summary>
    public static bool CanOpen(Object requester)
    {
        if (Owner != null && Owner != requester) return false;
        if (_restoreFrame >= 0) return false;
        var map = Object.FindFirstObjectByType<MapSystem>();
        if (map != null && map.isOpen) return false;
        return true;
    }

    public static bool Acquire(Object owner)
    {
        if (!CanOpen(owner)) return false;
        if (Owner == owner) return true;
        Owner = owner;

        _prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        _waters.Clear();
        foreach (var w in Object.FindObjectsByType<WaterSurface>(FindObjectsSortMode.None))
        {
            _waters.Add(new KeyValuePair<WaterSurface, float>(w, w.timeMultiplier));
            w.timeMultiplier = 0f;
        }
        AudioListener.pause = true;

        _disabled.Clear();
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || !mb.enabled) continue;
            string tn = mb.GetType().Name;
            for (int i = 0; i < disableWhileOpen.Length; i++)
                if (tn == disableWhileOpen[i]) { mb.enabled = false; _disabled.Add(mb); break; }
        }

        _prevCursorVisible = Cursor.visible; _prevCursorLock = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        return true;
    }

    public static void Release(Object owner)
    {
        if (Owner != owner) return;
        Owner = null;
        Time.timeScale = _prevTimeScale;
        for (int i = 0; i < _waters.Count; i++) if (_waters[i].Key != null) _waters[i].Key.timeMultiplier = _waters[i].Value;
        _waters.Clear();
        AudioListener.pause = false;
        _restoreFrame = Time.frameCount + 1;
    }

    /// <summary>매 프레임 호출: 닫은 다음 프레임에 컴포넌트·커서를 되돌린다.</summary>
    public static void Tick()
    {
        if (_restoreFrame >= 0 && Time.frameCount >= _restoreFrame)
        {
            _restoreFrame = -1;
            for (int i = 0; i < _disabled.Count; i++) if (_disabled[i] != null) _disabled[i].enabled = true;
            _disabled.Clear();
            Cursor.visible = _prevCursorVisible; Cursor.lockState = _prevCursorLock;
        }
    }
}
