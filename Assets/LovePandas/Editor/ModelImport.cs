using UnityEditor;

namespace LovePandas.Editor
{
    /// Настройки импорта моделей из Art/build_panda.py: оси Blender запекаются в меш,
    /// материалы не создаются (их ставит код — шейдер LovePandas/Toon), анимации нет — оживление кодом.
    public class ModelImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/LovePandas/Resources/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.bakeAxisConversion = true;
            mi.globalScale = 1;
            mi.useFileScale = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.optimizeGameObjects = false; // кости и сокеты нужны как Transform
            mi.importNormals = ModelImporterNormals.Import;
        }
    }
}
