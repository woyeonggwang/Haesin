using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class ShorelineWavesPass : WaterPass
    {
        ShorelineWavesPassCore _pass;

        public ShorelineWavesPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                   =  new ShorelineWavesPassCore();
            _pass.OnSetRenderTarget += OnSetRenderTarget;
            PassName                =  _pass.PassName;
        }
        private void OnSetRenderTarget(WaterPassContext waterContext, RTHandle rt1, RTHandle rt2)
        {
#if UNITY_2022_1_OR_NEWER
            ConfigureTarget(KWS_CoreUtils.GetMrtHandle(rt1, rt2), rt1); 
#else
            ConfigureTarget(KWS_CoreUtils.GetMrt(rt1, rt2), rt1.rt.depthBuffer); //by some reason, configure target/clear cause flickering in the editor view
#endif
            CoreUtils.SetRenderTarget(waterContext.cmd, KWS_CoreUtils.GetMrt(rt1, rt2), rt1.rt.depthBuffer, ClearFlag.Color, Color.black);
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