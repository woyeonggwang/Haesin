using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace KWS
{
    internal static partial class KWS_CoreUtils
    {
        static bool CanRenderWaterForCurrentCamera_PlatformSpecific(Camera cam)
        {
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null && camData.renderType == CameraRenderType.Overlay) return false;
            return true;
        }


        public static Vector2Int GetCameraRTHandleViewPortSize(Camera cam)
        {
#if ENABLE_VR
            if (XRSettings.enabled)
            {
                return new Vector2Int(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight);
            }
            else
#endif
            {
                var viewPortSize = RTHandles.rtHandleProperties.currentViewportSize;
                if (viewPortSize.x == 0 || viewPortSize.y == 0) return new Vector2Int(cam.pixelWidth, cam.pixelHeight);
                else return viewPortSize;
            }

        }

        public static bool CanRenderSinglePassStereo(Camera cam)
        {
#if ENABLE_VR
            return XRSettings.enabled &&
                   (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced && cam.cameraType != CameraType.SceneView);
#else
            return false;
#endif
        }

        public static bool IsSinglePassStereoActive()
        {
#if ENABLE_VR
            return XRSettings.enabled && XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced;
#else
            return false;
#endif
        }


        public static void SetPlatformSpecificPlanarReflectionParams(Camera reflCamera)
        {

        }

        
        public static void UniversalCameraRendering(WaterSystem waterInstance, Camera camera)
        {
            UniversalRenderPipeline.RenderSingleCamera(waterInstance.CurrentContext, camera);
        }

        public static void UpdatePlatformSpecificPlanarReflectionParams(Camera reflCamera, WaterSystem waterInstance)
        {
            //if (waterInstance.Settings.UseScreenSpaceReflection && waterInstance.Settings.UseAnisotropicReflections)
            //{
            //    reflCamera.clearFlags      = CameraClearFlags.Color;
            //    reflCamera.backgroundColor = Color.black;
            //}
            //else
            //{
            //    reflCamera.clearFlags = CameraClearFlags.Skybox;
            //}
        }

        public static void SetComputeShadersDefaultPlatformSpecificValues(this CommandBuffer cmd, ComputeShader cs, int kernel)
        {

        }
    }
}