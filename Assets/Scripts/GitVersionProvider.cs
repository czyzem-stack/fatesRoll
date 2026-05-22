using UnityEngine;
using System.IO;

public static class GitVersionProvider
{
    public static string GetBundleVersion()
    {
        try
        {
            string settingsPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings/ProjectSettings.asset");
            if (File.Exists(settingsPath))
            {
                string[] lines = File.ReadAllLines(settingsPath);
                foreach (string line in lines)
                {
                    if (line.Contains("bundleVersion:"))
                    {
                        return line.Replace("bundleVersion:", "").Trim();
                    }
                }
            }
        }
        catch { }
        return "0.0.0";
    }

    public static string GetShortHash()
    {
        try
        {
            string headPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".git/HEAD");
            if (File.Exists(headPath))
            {
                string head = File.ReadAllText(headPath).Trim();
                if (head.StartsWith("ref: "))
                {
                    string refPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".git/" + head.Substring(5));
                    if (File.Exists(refPath))
                    {
                        string commit = File.ReadAllText(refPath).Trim();
                        return commit.Substring(0, 7);
                    }
                }
                else
                {
                    return head.Substring(0, 7);
                }
            }
        }
        catch { }
        return "unknown";
    }
}
