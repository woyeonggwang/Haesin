using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class VolumetricLightingPass: WaterPass
    {
        VolumetricLightingPassCore _pass;

        public VolumetricLightingPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                   =  new VolumetricLightingPassCore();
            _pass.OnSetRenderTarget += OnSetRenderTarget;
            PassName                =  _pass.PassName;
        }
        private void OnSetRenderTarget(WaterPassContext waterContext, RTHandle rt)
        {
            ConfigureTarget(rt);
            CoreUtils.SetRenderTarget(waterContext.cmd, rt, ClearFlag.Color, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ExecutePassCore(_pass, ref renderingData);
        }

    

        public override void Release()
        {
            _pass.Release();
            _pass.OnSetRenderTarget -= OnSetRenderTarget;
        }
    }
}