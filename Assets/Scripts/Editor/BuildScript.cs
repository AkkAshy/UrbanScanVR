#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// Билд скрипт для сборки Windows Standalone из batch mode.
    /// Запуск: unity-editor -executeMethod Editor.BuildScript.PerformBuild
    /// </summary>
    public static class BuildScript
    {
        public static void PerformBuild()
        {
            Debug.Log("[UrbanScan] === Начинаю сборку Windows x64 ===");

            var scenes = new[] { "Assets/Scenes/Main.unity" };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/UrbanScanVR.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[UrbanScan] Сборка успешна! Размер: {summary.totalSize / (1024 * 1024)} MB");
                Debug.Log($"[UrbanScan] Путь: {summary.outputPath}");
            }
            else
            {
                Debug.LogError($"[UrbanScan] Сборка провалилась: {summary.result}");
                Debug.LogError($"[UrbanScan] Ошибок: {summary.totalErrors}, Предупреждений: {summary.totalWarnings}");

                // Выходим с ошибкой чтобы Docker знал что билд упал
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
