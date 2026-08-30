Shader "Hidden/KriptoFX/KWS/VolumetricLighting"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" { }
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.6

			#pragma multi_compile _ USE_CAUSTIC USE_ADDITIONAL_CAUSTIC

			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			
			#pragma multi_compile _ _FORWARD_PLUS
			
			#include "../PlatformSpecific/Includes/KWS_HelpersIncludes.cginc"
			#include "../Common/CommandPass/KWS_VolumetricLight_Common.cginc"


			half4 RayMarchDirLight(RaymarchData raymarchData, uint rayMarchSteps)
			{
				half4 result = 0;

				Light light = GetMainLight();
				float3 currentPos = raymarchData.currentPos;

				UNITY_LOOP
				for (uint i = 0; i < KWS_RayMarchSteps; ++i)
				{
					float atten = MainLightRealtimeShadow(TransformWorldToShadowCoord(currentPos));
					float3 scattering = raymarchData.stepSize;
					#if defined(USE_CAUSTIC) || defined(USE_ADDITIONAL_CAUSTIC)
						scattering += scattering * RaymarchCaustic(raymarchData, currentPos, light.direction);
					#endif
					half verticalDepth = lerp(1, GetVerticalDepthFade(raymarchData.waterID, currentPos.y), raymarchData.isUnderwater);
					half3 lightResult = atten * scattering * light.color * verticalDepth;
					result.rgb += lightResult;
					currentPos += raymarchData.step;
				}
				float cosAngle = dot(light.direction.xyz, -raymarchData.rayDir);
				result.rgb *= MieScattering(cosAngle);
				if (!raymarchData.isUnderwater) result.a = MainLightRealtimeShadow(TransformWorldToShadowCoord(raymarchData.rayStart));
				
				return result;
			}

			half4 RayMarchAdditionalLights(RaymarchData raymarchData, uint rayMarchSteps)
			{
				half4 result = 0;

				InputData inputData;
				inputData.normalizedScreenSpaceUV = raymarchData.uv;
				inputData.positionWS = raymarchData.currentPos;

				#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					
					#if USE_FORWARD_PLUS
						for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
						{
							float3 currentPos = raymarchData.currentPos;
							if (raymarchData.isUnderwater) result.a = max(result.a, GetAdditionalLight(lightIndex, raymarchData.sceneWorldPos).distanceAttenuation);

							UNITY_LOOP
							for (uint i = 0; i < KWS_RayMarchSteps; ++i)
							{
								Light addLight = GetAdditionalLight(lightIndex, currentPos);
								float atten = AdditionalLightRealtimeShadow(lightIndex, currentPos, addLight.direction);
								
								float3 scattering = raymarchData.stepSize * addLight.color.rgb;
								
								#if defined(USE_ADDITIONAL_CAUSTIC)
									if (_AdditionalLightsPosition[lightIndex].y > KWS_WaterPositionArray[raymarchData.waterID].y)
									{
										scattering += scattering * RaymarchCaustic(raymarchData, currentPos, addLight.direction.xyz);
									}
								#endif

								float3 lightResult = atten * scattering * addLight.distanceAttenuation;

								float cosAngle = dot(-raymarchData.rayDir, addLight.direction.xyz);
								lightResult *= MieScattering(cosAngle);

								result.rgb += lightResult;
								currentPos += raymarchData.step;
							}
						}
					#endif

					
					//uint pixelLightCount = GetAdditionalLightsCount(); AdditionalLightsCount is set on per-object basis by unity's rendering code.
					//Its just gonna return you the number of lights that the last object rendered was affected by
					uint pixelLightCount = _AdditionalLightsCount.x;
					LIGHT_LOOP_BEGIN(pixelLightCount)
					
					float3 currentPos = raymarchData.currentPos;
					if (raymarchData.isUnderwater) result.a = max(result.a, GetAdditionalPerObjectLight(lightIndex, raymarchData.sceneWorldPos).distanceAttenuation);

					UNITY_LOOP
					for (uint i = 0; i < KWS_RayMarchSteps; ++i)
					{
						Light addLight = GetAdditionalPerObjectLight(lightIndex, currentPos);
						float atten = AdditionalLightRealtimeShadow(lightIndex, currentPos, addLight.direction);
						
						float3 scattering = raymarchData.stepSize * addLight.color.rgb;

						//float3 posToLight = normalize(currentPos - addLight.direction.xyz);
						#if defined(USE_ADDITIONAL_CAUSTIC)
							if (_AdditionalLightsPosition[lightIndex].y > KWS_WaterPositionArray[raymarchData.waterID].y)
							{
								scattering += scattering * RaymarchCaustic(raymarchData, currentPos, addLight.direction.xyz);
							}
						#endif

						float3 lightResult = atten * scattering * addLight.distanceAttenuation;

						float cosAngle = dot(-raymarchData.rayDir, addLight.direction.xyz);
						lightResult *= MieScattering(cosAngle);

						result.rgb += lightResult;
						currentPos += raymarchData.step;
					}


					LIGHT_LOOP_END
					
				#endif

				return result;
			}

			inline float4 RayMarch(RaymarchData raymarchData)
			{
				float4 result = 0;
				float extinction = 0;
				
				result += RayMarchDirLight(raymarchData, KWS_RayMarchSteps) * KWS_VolumetricLightIntensityArray[raymarchData.waterID].x;
				result += RayMarchAdditionalLights(raymarchData, KWS_RayMarchSteps) * KWS_VolumetricLightIntensityArray[raymarchData.waterID].y;

				result.rgb /= raymarchData.transparent;
				
				result.rgb = max(MIN_THRESHOLD * 2, result.rgb);
				return result;
			}

			half4 frag(vertexOutput i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				uint waterID = GetWaterID(i.uv);
				if (waterID == 0) return 0;
				
				float depthTop = GetWaterDepth(i.uv);
				float depthBot = GetSceneDepth(i.uv);
				float underwaterMask = IsUnderwaterMask(GetWaterMask(i.uv));

				RaymarchData raymarchData = InitRaymarchData(i, depthTop, depthBot, underwaterMask, waterID);
				half4 finalColor = RayMarch(raymarchData);
				AddTemporalAccumulation(raymarchData.sceneWorldPos, finalColor.xyz);

				return finalColor;
			}

			ENDHLSL
		}
	}
}