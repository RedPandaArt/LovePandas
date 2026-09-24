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

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // Базовый материал для View.Materials: через Resources шейдер гарантированно попадает в сборку.
            const string matPath = "Assets/LovePandas/Resources/Materials/Base.mat";
            if (!File.Exists(matPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(matPath));
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetFloat("_Smoothness", 0.25f);
                AssetDatabase.CreateAsset(mat, matPath);
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

        [MenuItem("LovePandas/Build Android APK")]
        public static void BuildAndroid()
        {
            Setup();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[LovePandas] Build {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
