using UnityEngine;

/// <summary>
/// 입 표정 블렌드셰이프 제어. Body SkinnedMeshRenderer 의
/// MouthOpen / Smile_L / Smile_R / Frown_L / Frown_R / Pucker / Press 를 사용한다.
/// 인스펙터에서 preset 을 바꾸거나 값(0~1)을 직접 조절, 코드에서는 SetPreset / SetSmile 등을 호출.
/// 눈 깜빡임(EyeBlink)과는 서로 다른 셰이프만 건드리므로 같이 써도 된다.
/// </summary>
public class FacialExpression : MonoBehaviour
{
    public enum Preset { Neutral, Smile, SmileLeft, SmileRight, Frown, Pucker, Press, MouthOpen, OpenSmile, Surprised, Custom }

    [Tooltip("비우면 자식에서 이름이 bodyRendererName 인 SkinnedMeshRenderer 를 찾음")]
    public SkinnedMeshRenderer bodyRenderer;
    public string bodyRendererName = "Body";

    [Header("프리셋 (Custom 이면 아래 값을 그대로 사용)")]
    public Preset preset = Preset.Neutral;

    [Header("목표 값 (0~1)")]
    [Range(0f, 1f)] public float mouthOpen;
    [Range(0f, 1f)] public float smileL;
    [Range(0f, 1f)] public float smileR;
    [Range(0f, 1f)] public float frownL;
    [Range(0f, 1f)] public float frownR;
    [Range(0f, 1f)] public float pucker;
    [Range(0f, 1f)] public float press;

    [Header("전환 속도 (초당 변화량)")]
    public float blendSpeed = 6f;

    [Header("간단한 말하기 (입 벌림 흔들기)")]
    public bool talking;
    public float talkRate = 9f;
    [Range(0f, 1f)] public float talkAmount = 0.45f;

    static readonly string[] Names = { "MouthOpen", "Smile_L", "Smile_R", "Frown_L", "Frown_R", "Pucker", "Press" };
    readonly int[] _idx = new int[7];
    readonly float[] _cur = new float[7];
    Preset _applied = (Preset)(-1);

    void OnEnable() { Resolve(); }

    void Resolve()
    {
        if (bodyRenderer == null)
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == bodyRendererName) { bodyRenderer = r; break; }
        for (int i = 0; i < Names.Length; i++)
            _idx[i] = (bodyRenderer != null && bodyRenderer.sharedMesh != null) ? bodyRenderer.sharedMesh.GetBlendShapeIndex(Names[i]) : -1;
    }

    /// <summary>프리셋으로 목표 값을 바꾼다(부드럽게 전환).</summary>
    public void SetPreset(Preset p)
    {
        preset = p;
        ApplyPreset(p);
    }

    public void SetSmile(float w) { preset = Preset.Custom; smileL = smileR = Mathf.Clamp01(w); frownL = frownR = 0f; }
    public void SetMouthOpen(float w) { preset = Preset.Custom; mouthOpen = Mathf.Clamp01(w); }

    void ApplyPreset(Preset p)
    {
        _applied = p;
        if (p == Preset.Custom) return;
        mouthOpen = smileL = smileR = frownL = frownR = pucker = press = 0f;
        switch (p)
        {
            case Preset.Smile: smileL = smileR = 1f; break;
            case Preset.SmileLeft: smileL = 1f; break;
            case Preset.SmileRight: smileR = 1f; break;
            case Preset.Frown: frownL = frownR = 1f; break;
            case Preset.Pucker: pucker = 1f; break;
            case Preset.Press: press = 1f; break;
            case Preset.MouthOpen: mouthOpen = 1f; break;
            case Preset.OpenSmile: mouthOpen = 0.6f; smileL = smileR = 1f; break;
            case Preset.Surprised: mouthOpen = 0.8f; pucker = 0.4f; break;
        }
    }

    void LateUpdate()
    {
        if (bodyRenderer == null) return;
        if (preset != _applied) ApplyPreset(preset);

        float open = mouthOpen;
        if (talking)
        {
            float t = Time.time * talkRate;
            float n = Mathf.PerlinNoise(t, 0.37f);
            open = Mathf.Clamp01(open + talkAmount * Mathf.Clamp01(n * 1.6f - 0.3f));
        }
        float[] target = { open, smileL, smileR, frownL, frownR, pucker, press };
        float step = blendSpeed * Time.deltaTime;
        for (int i = 0; i < 7; i++)
        {
            _cur[i] = talking && i == 0 ? Mathf.MoveTowards(_cur[i], target[i], step * 3f) : Mathf.MoveTowards(_cur[i], target[i], step);
            if (_idx[i] >= 0) bodyRenderer.SetBlendShapeWeight(_idx[i], _cur[i] * 100f);
        }
    }
}
