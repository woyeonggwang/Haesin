using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KWS
{
    internal class KWS_WaterPassHandler
    {
        private List<WaterPass> _waterPasses;

        FftWavesPass                  _fftWavesPass;
        BuoyancyPass                  _buoyancyPass;
        FlowPass                      _flowPass;
        DynamicWavesPass              _dynamicWavesPass;
        ShorelineWavesPass            _shorelineWavesPass;
        WaterPrePass                  _waterPrePass;
        CausticPass                   _causticPass;
        ReflectionFinalPass           _reflectionFinalPass;
        ScreenSpaceReflectionPass     _ssrPass;
        VolumetricLightingPass        _volumetricLightingPass;
        DrawMeshPass                  _drawMeshPass;
        ShorelineFoamPass             _shorelineFoamPass;
        UnderwaterPass                _underwaterPass;
        DrawToPosteffectsDepthPass    _drawToDepthPass;



        OrthoDepthPass _orthoDepthPass;

        internal KWS_WaterPassHandler()
        {
            _dynamicWavesPass   = new DynamicWavesPass(RenderPassEvent.BeforeRenderingSkybox);
            _fftWavesPass       = new FftWavesPass(RenderPassEvent.BeforeRenderingSkybox);
            _buoyancyPass       = new BuoyancyPass(RenderPassEvent.BeforeRenderingSkybox);
            _flowPass           = new FlowPass(RenderPassEvent.BeforeRenderingSkybox);
            _shorelineWavesPass = new ShorelineWavesPass(RenderPassEvent.BeforeRenderingSkybox);
            _waterPrePass       = new WaterPrePass(RenderPassEvent.BeforeRenderingSkybox);
            _causticPass        = new CausticPass(RenderPassEvent.BeforeRenderingSkybox);

            _ssrPass                       = new ScreenSpaceReflectionPass(RenderPassEvent.BeforeRenderingTransparents);
            _reflectionFinalPass           = new ReflectionFinalPass(RenderPassEvent.BeforeRenderingTransparents);
            _volumetricLightingPass        = new VolumetricLightingPass(RenderPassEvent.BeforeRenderingTransparents);
            _drawMeshPass                  = new DrawMeshPass(RenderPassEvent.BeforeRenderingTransparents);
            _shorelineFoamPass             = new ShorelineFoamPass(RenderPassEvent.BeforeRenderingTransparents);
            _underwaterPass                = new UnderwaterPass(RenderPassEvent.AfterRenderingTransparents);
            _drawToDepthPass               = new DrawToPosteffectsDepthPass(RenderPassEvent.AfterRenderingTransparents);

            _waterPasses = new List<WaterPass>
            {
                _dynamicWavesPass, _fftWavesPass, _flowPass, _buoyancyPass, _shorelineWavesPass, _waterPrePass, _causticPass, _reflectionFinalPass,
                _ssrPass, _volumetricLightingPass, _drawMeshPass, _shorelineFoamPass, _underwaterPass, _drawToDepthPass
            };

            _orthoDepthPass = new OrthoDepthPass();


#if UNITY_EDITOR
            var urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.renderPipelineAsset;
            if (urpAsset != null)
            {
                urpAsset.supportsCameraOpaqueTexture = true;
                urpAsset.supportsCameraDepthTexture  = true;
            }
#endif
        }



        internal void OnBeforeFrameRendering(HashSet<Camera> cameras, CustomFixedUpdates fixedUpdates)
        {
            _orthoDepthPass.ExecutePerFrame(cameras, fixedUpdates);
            _fftWavesPass.ExecutePerFrame(cameras, fixedUpdates);
            _buoyancyPass.ExecutePerFrame(cameras, fixedUpdates);
            _flowPass.ExecutePerFrame(cameras, fixedUpdates);
            _dynamicWavesPass.ExecutePerFrame(cameras, fixedUpdates);
        }


        internal void OnBeforeCameraRendering(Camera cam, ScriptableRenderContext ctx)
        {
            try
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) return;

                data.requiresColorOption = CameraOverrideOption.On;
                data.requiresDepthOption = CameraOverrideOption.On;

                var cameraSize = KWS_CoreUtils.GetScreenSizeLimited(KWS_CoreUtils.SinglePassStereoEnabled);
                KWS_CoreUtils.RTHandles.SetReferenceSize(cameraSize.x, cameraSize.y);

                WaterPass.WaterPassContext waterContext = default;
                waterContext.cam = cam;

                waterContext.RenderContext        = ctx;
                waterContext.AdditionalCameraData = data;


                _drawMeshPass.ExecuteBeforeCameraRendering(cam);
                _shorelineFoamPass.ExecuteBeforeCameraRendering(cam);
                _underwaterPass.ExecuteBeforeCameraRendering(cam);

                _orthoDepthPass.Execute(waterContext);

                ExecutePass(_shorelineWavesPass, waterContext);

                _waterPrePass.ConfigureInput(ScriptableRenderPassInput.Depth); //we need depth texture before "copy color" pass and caustic rendering
                ExecutePass(_waterPrePass, waterContext);

                ExecutePass(_causticPass,                   waterContext);
                ExecutePass(_ssrPass,                       waterContext);
                ExecutePass(_reflectionFinalPass,           waterContext);
                ExecutePass(_volumetricLightingPass,        waterContext);
                ExecutePass(_shorelineFoamPass,             waterContext);

                ExecutePass(_underwaterPass,  waterContext);
                ExecutePass(_drawToDepthPass, waterContext);
              

            }
            catch (Exception e)
            {
                Debug.LogError("Water rendering error: " + e.InnerException);
            }
        }

        void ExecutePass(WaterPass pass, WaterPass.WaterPassContext waterContext)
        {
            pass.SetWaterContext(waterContext);
            waterContext.AdditionalCameraData.scriptableRenderer.EnqueuePass(pass);
        }


        public void Release()
        {
            if (_waterPasses != null)
            {
                foreach (var waterPass in _waterPasses)
                    if (waterPass != null)
                        waterPass.Release();
            }

            _orthoDepthPass?.Release();
        }
    }
}