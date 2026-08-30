using Mewlist;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class MassiveCloudsUniversalRPScriptableRenderPass : ScriptableRenderPass
{
    const string RenderMassiveCloudsTag = "Render MassiveClouds";

    private RenderTargetIdentifier currentTarget;
    private MassiveClouds currentMassiveClouds;

    public MassiveCloudsUniversalRPScriptableRenderPass()
    {
        renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    }

    public void SetRenderTarget(RenderTargetIdentifier target)
    {
        currentTarget = target;
    }
    
    public void SetRenderPassEvent(RenderPassEvent e)
    {
        renderPassEvent = e;
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (currentMassiveClouds == null)
        {
            var massiveClouds = renderingData.cameraData.camera.GetComponent<MassiveClouds>();
            if (massiveClouds == null)
            {
                var mainCamera = GameObject.FindWithTag("MainCamera");
                massiveClouds = mainCamera?.GetComponent<MassiveClouds>();
                if (massiveClouds == null) return;
            }

            currentMassiveClouds = massiveClouds;
        }
        if (!currentMassiveClouds.enabled) return;
        CommandBuffer cmd = CommandBufferPool.Get(RenderMassiveCloudsTag);
        currentMassiveClouds.BuildCommandBuffer(cmd, currentTarget, currentTarget);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}