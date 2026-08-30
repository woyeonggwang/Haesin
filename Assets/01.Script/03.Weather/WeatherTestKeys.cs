using UnityEngine;

/// <summary>
/// 날씨를 키보드로 넘겨보며 확인하는 테스트용 컴포넌트.
/// WASD(이동)와 Space(부스트), 마우스 버튼(포격)은 건드리지 않는다.
///
/// 기본 키
///   ]  다음 날씨      [  이전 날씨
///   1~9  해당 번호 날씨로 바로 전환
///   \  자동 변경 켜기/끄기
///   0  현재 날씨 즉시 다시 적용(리셋)
///
/// 빌드에 넣고 싶지 않으면 이 컴포넌트만 끄거나 지우면 된다.
/// </summary>
[DefaultExecutionOrder(150)]
public class WeatherTestKeys : MonoBehaviour
{
    [Header("대상 (비우면 자동 탐색)")]
    public WeatherSystem weather;

    [Header("키 설정")]
    public bool enableKeys = true;
    public KeyCode nextKey = KeyCode.RightBracket;   // ]
    public KeyCode prevKey = KeyCode.LeftBracket;    // [
    public KeyCode toggleAutoKey = KeyCode.Backslash;// \
    [Tooltip("1~9 키로 해당 번호 날씨를 바로 고른다.")]
    public bool numberKeysSelect = true;

    [Header("전환")]
    [Tooltip("테스트 키로 바꿀 때의 전환 시간(초). 짧게 두면 빠르게 확인할 수 있다.")]
    public float testTransitionSeconds = 3f;

    [Header("화면 표시")]
    public bool showLabel = true;
    public int fontSize = 15;

    private int _index;

    void Start()
    {
        if (weather == null) weather = Object.FindFirstObjectByType<WeatherSystem>();
        if (weather != null) _index = Mathf.Clamp(weather.startIndex, 0, Mathf.Max(0, weather.presets.Count - 1));
    }

    void Update()
    {
        if (!enableKeys || weather == null || weather.presets.Count == 0) return;

        if (Input.GetKeyDown(nextKey))
        {
            _index = (_index + 1) % weather.presets.Count;
            weather.SetWeather(_index, testTransitionSeconds);
        }
        else if (Input.GetKeyDown(prevKey))
        {
            _index = (_index - 1 + weather.presets.Count) % weather.presets.Count;
            weather.SetWeather(_index, testTransitionSeconds);
        }
        else if (Input.GetKeyDown(toggleAutoKey))
        {
            weather.autoChange = !weather.autoChange;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            weather.SetWeather(_index, 0.1f);
        }
        else if (numberKeysSelect)
        {
            for (int i = 0; i < 9 && i < weather.presets.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _index = i;
                    weather.SetWeather(_index, testTransitionSeconds);
                    break;
                }
            }
        }
    }

    void OnGUI()
    {
        if (!showLabel || weather == null) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;

        string auto = weather.autoChange ? "자동 ON" : "자동 OFF";
        string line1 = "날씨: " + weather.currentWeather;
        if (weather.transitionProgress < 1f)
            line1 += "  →  " + weather.nextWeather + "  (" + Mathf.RoundToInt(weather.transitionProgress * 100f) + "%)";
        else
            line1 += "   유지 " + Mathf.Max(0f, weather.holdRemaining).ToString("F0") + "초";

        string line2 = "[ 이전   ] 다음   1~" + Mathf.Min(9, weather.presets.Count) + " 직접선택   \\ " + auto;

        GUI.Box(new Rect(10, 10, 430, 52), GUIContent.none);
        GUI.Label(new Rect(20, 14, 420, 22), line1, style);

        style.fontSize = Mathf.Max(10, fontSize - 3);
        style.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
        GUI.Label(new Rect(20, 36, 420, 20), line2, style);
    }
}
