using KWS;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class DemoGUI: MonoBehaviour
{
    public GameObject button;
    public GameObject slider;

    public Camera cam;
    public Light sun;
    public GameObject environment;
    public WaterSystem water;

    public Camera Posteffects;
    public GameObject terrain;
    public Terrain terrainDetails;

    int buttonOffset = 35;
    int sliderOffset = 25;
    Vector2 currentElementOffset;

    List<GameObject> waterUIElements = new List<GameObject>();

    GameObject CreateButton(string text, Action action, bool currentActive, bool isWaterElement, params string[] prefixStatus)
    {
        var instance = Instantiate(button, transform);
        if (isWaterElement) waterUIElements.Add(instance);
        var uiElement = instance.GetComponent<KWS_DemoUIElement>();
        uiElement.Initialize(text, action, currentActive, prefixStatus);
       
        uiElement.Rect.anchoredPosition = currentElementOffset;
        currentElementOffset.y -= buttonOffset;
        return instance;
    }

    GameObject CreateSlider(string text, Action<float> action, bool isWaterElement = false)
    {
        var instance = Instantiate(slider, transform);
        if (isWaterElement) waterUIElements.Add(instance);
        var uiElement = instance.GetComponent<KWS_DemoUIElement>();
        uiElement.Initialize(text, action);
        uiElement.Rect.anchoredPosition = currentElementOffset;
        currentElementOffset.y -= sliderOffset;
        return instance;
    }

    void Start () 
    {
//#if KWS_DEBUG
//        var notes = GetComponentInChildren<Text>();
//        if(notes != null) notes.enabled = false;
//#endif

        currentElementOffset = new Vector2(10, -10);

        CreateButton("Next Scene", () =>
        {
            var currentSceneID = SceneManager.GetActiveScene().buildIndex;
            if (currentSceneID < SceneManager.sceneCountInBuildSettings - 1) currentSceneID++;
            else currentSceneID = 0;
            SceneManager.LoadScene(currentSceneID);
        }, currentActive: true, false);

        CreateButton("Previous Scene", () =>
         {
             var currentSceneID = SceneManager.GetActiveScene().buildIndex;
             if (currentSceneID > 0) currentSceneID--;
             else currentSceneID = 0;
             SceneManager.LoadScene(currentSceneID);
         },
            currentActive: true, false);

        if (sun != null)
        {
            CreateButton("Shadows", () =>
            {
                sun.shadows = (sun.shadows == LightShadows.None) ? sun.shadows = LightShadows.Soft : LightShadows.None;
            }, 
            currentActive: true, false, "On", "Off");
        }

        if(environment != null)
        {
            CreateButton("Environment", () =>
            {
                environment.gameObject.SetActive(!environment.gameObject.activeSelf);
            },
           currentActive: true, false, "On", "Off");
        }

        if (terrain != null)
        {
            CreateButton("Terrain", () =>
            {
                terrain.SetActive(!terrain.activeSelf);
            },
           currentActive: true, false, "On", "Off");
        }

        if (terrainDetails != null)
        {
            CreateButton("Terrain details", () =>
            {
                terrainDetails.drawTreesAndFoliage = !terrainDetails.drawTreesAndFoliage;
            },
            currentActive: true, false, "On", "Off");
        }

        if (Posteffects != null)
        {
            CreateButton("Post processing", () =>
            {
                var data = Posteffects.GetComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = !data.renderPostProcessing;
            },
           currentActive: true, false, "On", "Off");
        }

        if (water != null)
        {
            CreateButton("Water", () =>
            {
                water.gameObject.SetActive(!water.gameObject.activeSelf);
                SetWaterUIElementsActiveStatus(water.gameObject.activeSelf);
            },
           currentActive: true, false, "On", "Off");
        }

        InitializeWaterUI();

        CreateButton("Quit", () =>
        {
            Application.Quit();
        }, currentActive: true, false);
    }

    void SetWaterUIElementsActiveStatus(bool isActive)
    {
        foreach(var element in waterUIElements)
        {
            element.SetActive(isActive);
        }
    }
    int shorelineQuality = 1;
    void InitializeWaterUI()
    {
        currentElementOffset.y -= 50;
        CreateSlider("Transparent", (sliderVal) =>
        {
            water.Settings.Transparent = Mathf.Lerp(0.1f, 20f, sliderVal);
        }, true);

        CreateButton("Flowing", () =>
        {
            water.Settings.UseFlowMap = !water.Settings.UseFlowMap;
        },
        currentActive: water.Settings.UseFlowMap, true, "On", "Off");

        CreateButton("Dynamic waves", () =>
        {
            water.Settings.UseDynamicWaves = !water.Settings.UseDynamicWaves;
        },
        currentActive: water.Settings.UseDynamicWaves, true, "On", "Off");

        CreateButton("Shoreline", () =>
        {
            water.Settings.UseShorelineRendering = !water.Settings.UseShorelineRendering;
        },
        currentActive: water.Settings.UseShorelineRendering, true, "On", "Off");
        CreateButton("Foam Quality", () =>
        {
            shorelineQuality++;
            if (shorelineQuality == 3) shorelineQuality = 0;

            if (shorelineQuality == 0)
            {
                // water.FoamLodQuality = WaterSystem.QualityEnum.Low;
                //water.FoamCastShadows = false;
                //  water.FoamReceiveShadows = false;
            }
            else if (shorelineQuality == 1)
            {
                // water.FoamLodQuality = WaterSystem.QualityEnum.Medium;
                //water.FoamCastShadows = true;
                //  water.FoamReceiveShadows = true;
            }
            else if (shorelineQuality == 2)
            {
                //water.FoamLodQuality = WaterSystem.QualityEnum.High;
                //water.FoamCastShadows = true;
                //   water.FoamReceiveShadows = true;
            }
        },
        currentActive: water.Settings.UseShorelineRendering, true, "Medium", "High", "Low");

        CreateButton("Volumetric Lighting", () =>
        {
            water.Settings.UseVolumetricLight = !water.Settings.UseVolumetricLight;
        },
        currentActive: water.Settings.UseVolumetricLight, true, "On", "Off");

        CreateButton("Caustic Effect", () =>
        {
            water.Settings.UseCausticEffect = !water.Settings.UseCausticEffect;
        },
        currentActive: water.Settings.UseCausticEffect, true, "On", "Off");

        CreateButton("Underwater Effect", () =>
        {
            water.Settings.UseUnderwaterEffect = !water.Settings.UseUnderwaterEffect;
        },
        currentActive: water.Settings.UseUnderwaterEffect, true, "On", "Off");

        CreateButton("Draw to Depth", () =>
        {
            water.Settings.DrawToPosteffectsDepth = !water.Settings.DrawToPosteffectsDepth;
        },
                     currentActive: water.Settings.DrawToPosteffectsDepth, true, "On", "Off");

        CreateButton("Use Tesselation", () =>
        {
            water.Settings.UseTesselation = !water.Settings.UseTesselation;
            water.enabled = false;
            water.enabled = true;
        },
         currentActive: water.Settings.UseTesselation, true, "On", "Off");
    }
}
