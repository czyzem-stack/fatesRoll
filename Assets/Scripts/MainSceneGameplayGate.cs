using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>True once main-scene hero and DDOL managers are ready for dice / movement input.</summary>
public static class MainSceneGameplayGate
{
    public const string DefaultMainSceneName = "main";

    public static bool IsReady { get; private set; }

    public static void Reset()
    {
        IsReady = false;
    }

    public static IEnumerator WaitUntilReady(string sceneName = DefaultMainSceneName)
    {
        IsReady = false;

        yield return new WaitUntil(() => GameServices.IsInitialized);

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            float wait = 5f;
            while (wait > 0f)
            {
                scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid() && scene.isLoaded)
                    break;
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        yield return null;

        float timeout = 12f;
        while (timeout > 0f)
        {
            if (GameServices.Hero != null &&
                GameServices.TryGet<DiceSpawner>(out _) &&
                GameServices.TryGet<EnergyManager>(out _))
            {
                IsReady = true;
                yield break;
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning(
            "MainSceneGameplayGate: timed out waiting for hero / DiceSpawner / EnergyManager in main.");
    }
}
