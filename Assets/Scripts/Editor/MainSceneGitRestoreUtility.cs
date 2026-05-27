#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Exports committed main.unity from git for editor restore tooling.</summary>
internal static class MainSceneGitRestoreUtility
{
    public const string MainScenePath = MainSceneBootstrapCleanup.MainScenePath;
    public const string TempGitScenePath = "Assets/Scenes/_MainGitRestoreSource.unity";

    public static bool ExportGitMainScene(string assetPath = TempGitScenePath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "show HEAD:Assets/Scenes/main.unity",
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using Process process = Process.Start(psi);
            if (process == null)
                return false;

            using var output = File.Create(fullPath);
            process.StandardOutput.BaseStream.CopyTo(output);
            process.WaitForExit(5000);
            return process.ExitCode == 0 && new FileInfo(fullPath).Length > 0;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"MainSceneGitRestoreUtility: git show failed — {ex.Message}");
            return false;
        }
    }
}
#endif
