using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class UnderwaterPass: WaterPass
    {
        UnderwaterPassCore _pass;

        public UnderwaterPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                   =  new UnderwaterPassCore();
            _pass.OnSetRenderTarget += OnSetRenderTarget;
            PassName                =  _pass.PassName;
        }

        private void OnSetRenderTarget(WaterPassContext waterContext, RTHandle rt)
        {
            if (rt == null)
            {
                ConfigureTarget(waterContext.cameraColor, waterContext.cameraDepth);
                CoreUtils.SetRenderTarget(waterContext.cmd, waterContext.cameraColor);
            }
            else
            {
                ConfigureTarget(rt);
                CoreUtils.SetRenderTarget(waterContext.cmd, rt);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ExecutePassCore(_pass, ref renderingData);
        }

        public override void ExecuteBeforeCameraRendering(Camera cam)
        {
            _pass.ExecuteBeforeCameraRendering(cam);
        }

        public override void Release()
        {
            _pass.OnSetRenderTarget -= OnSetRenderTarget;
            _pass.Release();
        }
    }
}