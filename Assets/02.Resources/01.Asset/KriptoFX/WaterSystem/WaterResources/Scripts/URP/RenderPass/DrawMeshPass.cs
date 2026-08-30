using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class DrawMeshPass : WaterPass
    {
        DrawMeshPassCore _pass;

        public DrawMeshPass(RenderPassEvent passEvent) : base(passEvent)
        {
            _pass = new DrawMeshPassCore();
            PassName = _pass.PassName;
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
            _pass.Release();
        }
    }
}