using LovePandas.Core;
using LovePandas.UI;
using LovePandas.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace LovePandas.App
{
    /// Точка входа. Вся сцена собирается кодом, поэтому .unity-файл пустой и не конфликтует в git.
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindAnyObjectByType<AppUI>() != null) return;

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var world = new GameObject("World").transform;
            BuildLocation(world);
            var character = CharacterView.Create(world);
            character.transform.localRotation = Quaternion.Euler(0, 180, 0);

            var game = new GameService(new LocalStore());
            var ui = new GameObject("UI").AddComponent<AppUI>();
            ui.Init(game, character, CreatePanel());

            AutoShot.TryStart(ui);
        }

        /// Временная «диорама»: полянка, небо, мягкий свет. Заменится локацией от художника.
        static void BuildLocation(Transform parent)
        {
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.SetParent(parent, false);
            cam.transform.localPosition = new Vector3(0, 1.6f, -5.2f);
            cam.transform.localRotation = Quaternion.Euler(8, 0, 0);
            cam.fieldOfView = 40;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.99f, 0.86f, 0.74f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.localRotation = Quaternion.Euler(45, -30, 0);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.62f, 0.58f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Ground";
            Object.Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0, -0.05f, 0);
            ground.transform.localScale = new Vector3(5f, 0.05f, 5f);
            ground.GetComponent<Renderer>().sharedMaterial = Materials.Lit(new Color(0.62f, 0.78f, 0.45f));
        }

        static PanelSettings CreatePanel()
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/Theme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0f;
            return panel;
        }
    }
}
