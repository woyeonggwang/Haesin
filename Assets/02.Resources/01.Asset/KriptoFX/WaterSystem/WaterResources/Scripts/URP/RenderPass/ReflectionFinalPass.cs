using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class ReflectionFinalPass : WaterPass
    {
        ReflectionFinalPassCore _pass;

        public ReflectionFinalPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass                           =  new ReflectionFinalPassCore();
            _pass.OnInitializedRenderTarget += OnInitializedRenderTarget;
            PassName                        =  _pass.PassName;
        }

        private void OnInitializedRenderTarget(WaterPassContext waterContext, RTHandle rt)
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
            _pass.OnInitializedRenderTarget -= OnInitializedRenderTarget;
            _pass.Release();
        }
    }
}