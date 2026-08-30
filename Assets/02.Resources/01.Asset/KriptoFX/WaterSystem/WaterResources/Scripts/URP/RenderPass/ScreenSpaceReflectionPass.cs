using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class ScreenSpaceReflectionPass: WaterPass
    {
        ScreenSpaceReflectionPassCore _pass;

        public ScreenSpaceReflectionPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                   =  new ScreenSpaceReflectionPassCore();
            _pass.OnSetRenderTarget += OnSetRenderTarget;
            PassName                =  _pass.PassName;
        }

        private void OnSetRenderTarget(WaterPassContext waterContext, RTHandle rt)
        {
            ConfigureTarget(rt);
            CoreUtils.SetRenderTarget(waterContext.cmd, rt, ClearFlag.Color, Color.clear);
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