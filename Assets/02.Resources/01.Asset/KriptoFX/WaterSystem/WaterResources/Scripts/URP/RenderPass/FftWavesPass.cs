using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class FftWavesPass : WaterPass
    {
        FftWavesPassCore _pass;
        public FftWavesPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass = new FftWavesPassCore();
            PassName = _pass.PassName;
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
            _pass.Release();
        }


      
    }
}