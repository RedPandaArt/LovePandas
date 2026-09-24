using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LovePandas.Editor
{
    /// Настройка проекта и сборка из меню или из командной строки (-executeMethod).
    public static class BuildTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ApkPath = "Builds/LovePandas.apk";

        [MenuItem("LovePandas/Setup Project")]
        public static void Setup()
        {
            PlayerSettings.companyName = "RedPandaArt";
            PlayerSettings.productName = "LovePandas";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.redpandaart.lovepandas");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Сервер на Railway по https. Для теста с сервером на ПК (http) временно поставить AlwaysAllowed.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // Везде мобильный URP. PC-рендерер из шаблона с SSAO в сборке падает и даёт чёрный экран.
            var mobile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = mobile;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = mobile;
            }
            QualitySettings.SetQualityLevel(current, false);

            // Базовые материалы в Resources: через них шейдеры гарантированно попадают в сборку.
            EnsureMaterial("Assets/LovePandas/Resources/Materials/Base.mat", "LovePandas/Toon");
            EnsureMaterial("Assets/LovePandas/Resources/Materials/FX.mat", "LovePandas/FX");

            // Фоны: полное качество, без сжатия в кашу на телефоне.
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/LovePandas/Resources/Backgrounds" }))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (ti.maxTextureSize == 2048 && ti.mipmapEnabled == false) continue;
                ti.maxTextureSize = 2048;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[LovePandas] Project setup done");
        }

        /// Сборка под Windows — для быстрой проверки экранов через AutoShot.
        [MenuItem("LovePandas/Build Windows (test)")]
        public static void BuildWindows()
        {
            Setup();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Win/LovePandas.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[LovePandas] Win build {report.summary.result}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        static void EnsureMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(new Material(shader), path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
        }

        [MenuItem("LovePandas/Build Android APK")]
        public static void BuildAndroid() => Android(dev: false);

        /// Dev-сборка: отдельное приложение «LovePandas Dev» рядом с боевым, ходит на сервер на ПК
        /// (Resources/server_url_dev.txt) по http, в углу метка DEV. Для быстрых проверок без выката на Railway.
        [MenuItem("LovePandas/Build Android DEV APK")]
        public static void BuildAndroidDev() => Android(dev: true);

        static void Android(bool dev)
        {
            Setup();
            if (dev)
            {
                PlayerSettings.productName = "LovePandas Dev";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.redpandaart.lovepandas.dev");
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            }
            var path = dev ? "Builds/LovePandas-dev.apk" : ApkPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorUserBuildSettings.buildAppBundle = false;
            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    options = dev ? BuildOptions.Development : BuildOptions.None,
                    extraScriptingDefines = dev ? new[] { "LP_DEV" } : null,
                });
            }
            finally
            {
                if (dev) { Setup(); AssetDatabase.SaveAssets(); } // вернуть боевые настройки, чтобы они не попали в git
            }
            Debug.Log($"[LovePandas] Build {(dev ? "DEV " : "")}{report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
