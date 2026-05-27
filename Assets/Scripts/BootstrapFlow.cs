using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Loads the title scene after <see cref="GameServices"/> persists via DontDestroyOnLoad.</summary>
[DefaultExecutionOrder(1000)]
public class BootstrapFlow : MonoBehaviour
{
    [SerializeField] private string firstContentScene = "title";

    private void Start()
    {
        var active = SceneManager.GetActiveScene();
        if (active.name == firstContentScene)
            return;

        SceneManager.LoadScene(firstContentScene, LoadSceneMode.Single);
    }
}
