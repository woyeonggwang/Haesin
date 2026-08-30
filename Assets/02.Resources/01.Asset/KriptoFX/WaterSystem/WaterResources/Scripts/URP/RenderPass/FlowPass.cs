using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class FlowPass : WaterPass
    {
        FlowPassCore             _pass;

        public FlowPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass = new FlowPassCore();
            _pass.OnSetRenderTarget+= OnSetRenderTarget;
            PassName = _pass.PassName;
        }

        private void OnSetRenderTarget(CommandBuffer passCommandBuffer, RTHandle rt1, RTHandle rt2)
        {
//#if UNITY_2022_1_OR_NEWER
//            ConfigureTarget(KWS_CoreUtils.GetMrtHandle(rt1, rt2), rt2);
//#else
//            ConfigureTarget(KWS_CoreUtils.GetMrt(rt1, rt2), rt2);
//#endif
            CoreUtils.SetRenderTarget(passCommandBuffer, KWS_CoreUtils.GetMrt(rt1, rt2), rt1.rt.depthBuffer, ClearFlag.None, Color.clear);
        }
        public override void ExecutePerFrame(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
            _pass.ExecutePerFrame(cameras, fixedUpdates);
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