using UnityEngine;

/// <summary>
/// 일정 시간마다 눈을 깜빡인다. Body SkinnedMeshRenderer 의 블렌드셰이프 Blink_L / Blink_R 사용.
/// 간격은 minInterval~maxInterval 사이 무작위, 가끔 두 번 연속 깜빡임.
/// </summary>
public class EyeBlink : MonoBehaviour
{
    [Tooltip("비우면 자식에서 이름이 bodyRendererName 인 SkinnedMeshRenderer 를 찾음")]
    public SkinnedMeshRenderer bodyRenderer;
    public string bodyRendererName = "Body";
    public string leftShape = "Blink_L";
    public string rightShape = "Blink_R";
    [Tooltip("중간 보정 셰이프(반쯤 감을 때 눈꺼풀이 눈알을 파고들지 않게). 없으면 무시")]
    public string leftMidShape = "Blink_L_mid";
    public string rightMidShape = "Blink_R_mid";

    [Header("간격 (초)")]
    public float minInterval = 2.5f;
    public float maxInterval = 5.5f;
    [Range(0f, 1f)] public float doubleBlinkChance = 0.15f;

    [Header("한 번 깜빡임 (초)")]
    public float closeTime = 0.07f;
    public float holdTime = 0.03f;
    public float openTime = 0.12f;

    [Header("상태")]
    public bool blinkEnabled = true;
    [Range(0f, 1f), Tooltip("표정 등에서 눈을 반쯤 감은 기본값(0 = 뜸)")]
    public float baseClosed = 0f;

    int _l = -1, _r = -1, _lm = -1, _rm = -1;
    float _next;
    float _t = -1f;
    bool _pendingDouble;

    /// <summary>현재 감긴 정도(0~1). 테스트·디버그용.</summary>
    public float CurrentWeight { get; private set; }
    public int BlinkCount { get; private set; }

    void Awake() { Resolve(); ScheduleNext(); }

    void OnEnable() { Resolve(); }

    void Resolve()
    {
        if (bodyRenderer == null)
        {
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == bodyRendererName) { bodyRenderer = r; break; }
        }
        if (bodyRenderer == null || bodyRenderer.sharedMesh == null) { _l = _r = -1; return; }
        _l = bodyRenderer.sharedMesh.GetBlendShapeIndex(leftShape);
        _r = bodyRenderer.sharedMesh.GetBlendShapeIndex(rightShape);
        _lm = bodyRenderer.sharedMesh.GetBlendShapeIndex(leftMidShape);
        _rm = bodyRenderer.sharedMesh.GetBlendShapeIndex(rightMidShape);
    }

    void ScheduleNext()
    {
        _next = Time.time + Random.Range(minInterval, maxInterval);
    }

    /// <summary>외부에서 즉시 깜빡이게 함(맞았을 때 등).</summary>
    public void BlinkNow()
    {
        _t = 0f;
    }

    void LateUpdate()
    {
        if (_l < 0 && _r < 0) return;

        float w = 0f;
        if (blinkEnabled)
        {
            if (_t < 0f && Time.time >= _next) { _t = 0f; BlinkCount++; }
            if (_t >= 0f)
            {
                _t += Time.deltaTime;
                float total = closeTime + holdTime + openTime;
                if (_t < closeTime) w = Smooth(_t / closeTime);
                else if (_t < closeTime + holdTime) w = 1f;
                else if (_t < total) w = 1f - Smooth((_t - closeTime - holdTime) / openTime);
                else
                {
                    w = 0f; _t = -1f;
                    if (!_pendingDouble && Random.value < doubleBlinkChance)
                    {
                        _pendingDouble = true;
                        _next = Time.time + 0.12f;
                    }
                    else { _pendingDouble = false; ScheduleNext(); }
                }
            }
        }

        CurrentWeight = Mathf.Max(w, baseClosed);
        float v = CurrentWeight * 100f;
        if (_l >= 0) bodyRenderer.SetBlendShapeWeight(_l, v);
        if (_r >= 0) bodyRenderer.SetBlendShapeWeight(_r, v);
        // 선형 보간은 호(弧) 대신 현(弦)으로 움직여 중간에 눈알을 파고듦 → 가운데(0.5)에서 최대인 보정 셰이프
        float mid = 4f * CurrentWeight * (1f - CurrentWeight) * 100f;
        if (_lm >= 0) bodyRenderer.SetBlendShapeWeight(_lm, mid);
        if (_rm >= 0) bodyRenderer.SetBlendShapeWeight(_rm, mid);
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
