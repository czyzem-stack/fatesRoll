#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Rewires main-scene roll buttons to <see cref="DiceRollGateway"/> after DiceSpawner moved to Bootstrap.</summary>
public static class MainSceneDiceUiSetup
{
    const string MainScenePath = MainSceneBootstrapCleanup.MainScenePath;
    const string RollButtonPath = "MainUI_Canvas/HUD_Control/Joystick_Button_l_Attack";

    [MenuItem("FatesRoll/Setup/Rewire Main Scene Dice Buttons")]
    public static void RewireMainSceneDiceButtons()
    {
        if (!System.IO.File.Exists(MainScenePath))
        {
            Debug.LogError($"MainSceneDiceUiSetup: missing {MainScenePath}.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        int wired = 0;

        var attackGo = GameObject.Find(RollButtonPath);
        if (attackGo != null && attackGo.TryGetComponent(out Button attackButton))
            wired += RewireButton(attackButton) ? 1 : 0;

        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button == null || button.gameObject == attackGo)
                continue;

            if (!IsLikelyRollButton(button))
                continue;

            if (RewireButton(button))
                wired++;
        }

        if (wired > 0)
            EditorSceneManager.SaveScene(scene);

        Debug.Log(
            wired > 0
                ? $"MainSceneDiceUiSetup: rewired {wired} button(s) to DiceRollGateway.Roll."
                : "MainSceneDiceUiSetup: no roll buttons needed rewiring.");
    }

    static bool RewireButton(Button button)
    {
        RemoveBrokenDiceListeners(button);
        UnityAction roll = DiceRollGateway.Roll;
        UnityEventTools.AddPersistentListener(button.onClick, roll);
        EditorUtility.SetDirty(button);
        return true;
    }

    static bool RemoveBrokenDiceListeners(Button button)
    {
        int count = button.onClick.GetPersistentEventCount();
        bool removed = false;

        for (int i = count - 1; i >= 0; i--)
        {
            Object target = button.onClick.GetPersistentTarget(i);
            string method = button.onClick.GetPersistentMethodName(i);

            if (target == null ||
                method == nameof(DiceSpawner.RollDice) ||
                method == nameof(DiceSpawner.OnRoll) ||
                method == nameof(DiceSpawner.ToggleAutoRoll))
            {
                UnityEventTools.RemovePersistentListener(button.onClick, i);
                removed = true;
            }
        }

        return removed;
    }

    static bool IsLikelyRollButton(Button button)
    {
        string name = button.name;
        return name.Contains("Attack", System.StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Roll", System.StringComparison.OrdinalIgnoreCase);
    }
}
#endif
