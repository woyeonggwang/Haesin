using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal abstract class WaterPass : ScriptableRenderPass
    {
        readonly RenderTargetIdentifier _cameraDepthTextureRT    = new RenderTargetIdentifier(Shader.PropertyToID("_CameraDepthTexture"));
        readonly RenderTargetIdentifier _cameraDepthAttachmentRT = new RenderTargetIdentifier(Shader.PropertyToID("_CameraDepthAttachment"));


        internal struct WaterPassContext
        {
            public Camera        cam;
            public CommandBuffer cmd;

#if UNITY_2022_1_OR_NEWER
            public RTHandle cameraDepth;
            public RTHandle cameraColor;
#else
            public RenderTargetIdentifier cameraDepth;
            public RenderTargetIdentifier cameraColor;
#endif
            //public int RequiredFixedUpdateCount;
            public CustomFixedUpdates FixedUpdates;

            public ScriptableRenderContext       RenderContext;
            public UniversalAdditionalCameraData AdditionalCameraData;
        }

        internal string  PassName;
        WaterPassContext _waterContext;

        protected WaterPass(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }


        internal void SetWaterContext(WaterPassContext waterContext)
        {
            _waterContext = waterContext;
        }


        internal void ExecutePassCore(WaterPassCore passCore, ref RenderingData renderingData, bool useStereoTarget = false)
        {
            //if (useStereoTarget && KWS_CoreUtils.SinglePassStereoEnabled) CoreUtils.SetRenderTarget(cmd, BuiltinRenderTextureType.CurrentActive);

            _waterContext.cmd = CommandBufferPool.Get(PassName);
            _waterContext.cmd.Clear();

#if UNITY_2022_1_OR_NEWER
            _waterContext.cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            _waterContext.cameraDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
#else
            _waterContext.cameraColor = renderingData.cameraData.renderer.cameraColorTarget;
            //_waterContext.cameraDepth = renderingData.cameraData.renderer.cameraDepthTarget; //doesnt work in editor and also editor camera ignores "water depth write" issue
            _waterContext.cameraDepth = _cameraDepthTextureRT;
#endif

            passCore.Execute(_waterContext);
            _waterContext.RenderContext.ExecuteCommandBuffer(_waterContext.cmd);

            CommandBufferPool.Release(_waterContext.cmd);
        }

        public virtual void ExecuteBeforeCameraRendering(Camera cam)
        {
        }

        public virtual void ExecutePerFrame(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
        }

        public abstract void Release();
    }
}