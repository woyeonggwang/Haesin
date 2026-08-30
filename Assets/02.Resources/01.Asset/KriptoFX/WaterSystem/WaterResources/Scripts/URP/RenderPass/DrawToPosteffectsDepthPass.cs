using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class DrawToPosteffectsDepthPass : WaterPass
    {
        DrawToPosteffectsDepthPassCore _pass;

//#if UNITY_2022_1_OR_NEWER
//        private RTHandle _depthRT;
//#else
//        RenderTargetIdentifier _depthRT = new RenderTargetIdentifier(Shader.PropertyToID("_CameraDepthTexture"));
//#endif

        public DrawToPosteffectsDepthPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                   =  new DrawToPosteffectsDepthPassCore();
            _pass.OnSetRenderTarget += OnSetRenderTarget;
            PassName                =  _pass.PassName;
        }

        private void OnSetRenderTarget(WaterPassContext waterContext)
        {
            ConfigureTarget(waterContext.cameraDepth);
            CoreUtils.SetRenderTarget(waterContext.cmd, waterContext.cameraColor, waterContext.cameraDepth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ExecutePassCore(_pass, ref renderingData);
        }


        public override void Release()
        {
            _pass.OnSetRenderTarget -= OnSetRenderTarget;
            _pass.Release();
        }
    }
}
