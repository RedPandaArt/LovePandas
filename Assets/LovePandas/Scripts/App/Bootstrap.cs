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

            // Адрес сервера — Resources/server_url.txt; для тестов на ПК -lpServer, -lpProfile (второй игрок
            // на той же машине), -lpToken (войти готовым игроком).
#if LP_DEV
            const string urlFile = "server_url_dev"; // dev-сборка: сервер на ПК в локальной сети
#else
            const string urlFile = "server_url";
#endif
            var url = Args.Get("-lpServer") ?? Resources.Load<TextAsset>(urlFile).text.Trim();
            var api = new ApiClient(url, Args.Get("-lpProfile") ?? "default", Args.Get("-lpToken"));
            var game = new GameService(api);
            var ui = new GameObject("UI").AddComponent<AppUI>();
            ui.Init(game, character, CreatePanel());

            AutoShot.TryStart(ui);
        }

        /// Локация: камера, свет под нарисованный фон и атмосфера (Atmosphere).
        /// Камера смотрит горизонтально: лапы персонажа на ~22% высоты экрана, голова на ~62% — он стоит на площадке фона.
        static void BuildLocation(Transform parent)
        {
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.SetParent(parent, false);
            cam.transform.localPosition = new Vector3(0, 1.6f, -8f);
            cam.fieldOfView = 40;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.2f, 0.2f);

            // Солнце сверху-слева, как луч на фоне; тёплое. Бирюзовый заполняющий свет — цвет джунглей.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.9f, 0.72f);
            sun.shadows = LightShadows.None;
            sun.transform.localRotation = Quaternion.Euler(40, 30, 0);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.62f, 0.6f);

            Atmosphere.Create(cam, parent, "jungle");
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
