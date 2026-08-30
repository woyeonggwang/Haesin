using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class WaterPrePass : WaterPass
    {
        WaterPrePassCore _pass;

        public WaterPrePass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                           =  new WaterPrePassCore();
            _pass.OnInitializedRenderTarget += OnInitializedRenderTarget;
            PassName                        =  _pass.PassName;
        }
        private void OnInitializedRenderTarget(WaterPassContext waterContext, RTHandle rt1, RTHandle rt2, RTHandle rt3)
        {
#if UNITY_2022_1_OR_NEWER
            ConfigureTarget(KWS_CoreUtils.GetMrtHandle(rt1, rt2, rt3), rt3); 
#else
            ConfigureTarget(KWS_CoreUtils.GetMrt(rt1, rt2, rt3), rt3.rt); //by some reason, configure target/clear cause flickering in the editor view
#endif
            CoreUtils.SetRenderTarget(waterContext.cmd, KWS_CoreUtils.GetMrt(rt1, rt2, rt3), rt3, ClearFlag.All, Color.black);
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ExecutePassCore(_pass, ref renderingData);
        }

        public override void Release()
        {
            _pass.OnInitializedRenderTarget -= OnInitializedRenderTarget;
            _pass.Release();
        }
    }
}