using UnityEngine;
using UnityEngine.Rendering.Universal;

[CreateAssetMenu(fileName = "MassiveCloudsUniversalRPScriptableRendererFeature",
    menuName = "MassiveClouds/UniversalRPScriptableRendererFeature", order = 1)]
public class MassiveCloudsUniversalRPScriptableRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

    private MassiveCloudsUniversalRPScriptableRenderPass currentPass;
    public override void Create()
    {
        currentPass = new MassiveCloudsUniversalRPScriptableRenderPass();
        currentPass.SetRenderPassEvent(renderPassEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        currentPass.SetRenderTarget(renderer.cameraColorTarget);
        renderer.EnqueuePass(currentPass);
    }
}
