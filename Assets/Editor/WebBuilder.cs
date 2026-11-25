using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KartRush.EditorTools
{
    /// <summary>Builds the WebGL player into /docs so GitHub Pages can serve it straight from the repo.</summary>
    public static class WebBuilder
    {
        static string OutputDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs"));   // absolute: the Editor's working directory is not the project

        [MenuItem("KartRush/Switch to WebGL")]
        public static void SwitchToWebGL()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        /// <summary>Batch entry point: regenerate the scene, then build WebGL.</summary>
        public static void BuildAll()
        {
            KartSceneBuilder.Build();
            Build();
        }

        [MenuItem("KartRush/Build WebGL (docs/)")]
        public static void Build()
        {
            PlayerSettings.productName = "Kart Rush";
            PlayerSettings.companyName = "KartRush";
            PlayerSettings.runInBackground = true;
            // GitHub Pages can't set Content-Encoding for .br/.gz files, so ship uncompressed (Pages gzips on the fly).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);

            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/KartTrack.unity" },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            Debug.Log($"[KartRush] WebGL build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) Debug.LogError("[KartRush] build step '" + step.name + "': " + m.content);
            Debug.Log("[KartRush] output exists=" + Directory.Exists(OutputDir) + " files=" + report.GetFiles().Length + " first=" + (report.GetFiles().Length > 0 ? report.GetFiles()[0].path : "-"));
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("WebGL build failed");
            File.WriteAllText(Path.Combine(OutputDir, ".nojekyll"), "");
        }
    }
}
