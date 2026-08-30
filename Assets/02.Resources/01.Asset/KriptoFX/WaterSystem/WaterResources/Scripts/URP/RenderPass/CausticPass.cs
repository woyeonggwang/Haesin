using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class CausticPass : WaterPass
    {
        CausticPassCore                 _pass;
        readonly RenderTargetIdentifier _cameraDepthAttachmentRT = new RenderTargetIdentifier(Shader.PropertyToID("_CameraDepthAttachment"));

        public CausticPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                         =  new CausticPassCore();
            _pass.OnRenderToCausticTarget += OnRenderToCausticTarget;
            _pass.OnRenderToCameraTarget  += OnRenderToCameraTarget;
            PassName                      =  _pass.PassName;
        }

        private void OnRenderToCausticTarget(WaterPassContext waterContext, RTHandle rt, int slice)
        {
            ConfigureTarget(rt);
            CoreUtils.SetRenderTarget(waterContext.cmd, rt, ClearFlag.Color, Color.black, depthSlice: slice);
        }


        private void OnRenderToCameraTarget(WaterPassContext waterContext)
        {
            //I need to use _cameraDepthAttachmentRT for stencil masking, because I can't set and read _CameraDepthTexture at the same time. Also editor camera doesnt works with _cameraDepthAttachmentRT
            //ConfigureTarget(waterContext.cameraColor, _cameraDepthAttachmentRT); 
            //if(waterContext.cam.cameraType == CameraType.SceneView) CoreUtils.SetRenderTarget(waterContext.cmd, waterContext.cameraColor);
            //else                                                    CoreUtils.SetRenderTarget(waterContext.cmd, waterContext.cameraColor, _cameraDepthAttachmentRT);

            ConfigureTarget(waterContext.cameraColor);
            CoreUtils.SetRenderTarget(waterContext.cmd, waterContext.cameraColor);
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ExecutePassCore(_pass, ref renderingData);
        }
       

        public override void Release()
        {
            _pass.OnRenderToCausticTarget -= OnRenderToCausticTarget;
            _pass.OnRenderToCameraTarget  -= OnRenderToCameraTarget;
            _pass.Release();
        }
    }
}