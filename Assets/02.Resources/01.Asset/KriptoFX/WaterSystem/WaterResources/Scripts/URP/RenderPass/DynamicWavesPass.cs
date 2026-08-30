using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class DynamicWavesPass : WaterPass
    {
        DynamicWavesPassCore _pass;

        public DynamicWavesPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                   =  new DynamicWavesPassCore();
            _pass.OnSetRenderTarget += OnSetRenderTarget;
            PassName                =  _pass.PassName;
        }

        private void OnSetRenderTarget(CommandBuffer passCommandBuffer, RTHandle rt)
        {
            //ConfigureTarget(rt);
            CoreUtils.SetRenderTarget(passCommandBuffer, rt, ClearFlag.Color, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ExecutePassCore(_pass, ref renderingData);
        }
        public override void ExecutePerFrame(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
            _pass.ExecutePerFrame(cameras, fixedUpdates);
        }


        public override void Release()
        {
            _pass.OnSetRenderTarget -= OnSetRenderTarget;
            _pass.Release();
        }

        
    }
}