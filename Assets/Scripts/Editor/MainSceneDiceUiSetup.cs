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
    const string RollButtonPath = "MainUI_Canvas/Control/Joystick_Button_l_Attack";

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
        if (attackGo != null)
        {
            if (RewireGameObject(attackGo))
                wired++;
        }

        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button == null || button.gameObject == attackGo)
                continue;

            if (!IsLikelyRollButton(button))
                continue;

            if (RewireGameObject(button.gameObject))
                wired++;
        }

        if (wired > 0)
            EditorSceneManager.SaveScene(scene);

        Debug.Log(
            wired > 0
                ? $"MainSceneDiceUiSetup: rewired {wired} object(s) to DiceRollGateway."
                : "MainSceneDiceUiSetup: no roll buttons needed rewiring.");
    }

    static bool RewireGameObject(GameObject go)
    {
        var bridge = go.GetComponent<DiceRollUiBridge>();
        if (bridge == null)
            bridge = go.AddComponent<DiceRollUiBridge>();

        bool changed = false;
        if (go.TryGetComponent(out Button button))
        {
            RemoveBrokenDiceListeners(button.onClick);
            UnityEventTools.AddPersistentListener(button.onClick, bridge.Roll);
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(go);
            
        return changed;
    }

    static void RemoveBrokenDiceListeners(UnityEventBase unityEvent)
    {
        int count = unityEvent.GetPersistentEventCount();
        for (int i = count - 1; i >= 0; i--)
        {
            Object target = unityEvent.GetPersistentTarget(i);
            string method = unityEvent.GetPersistentMethodName(i);

            if (target == null ||
                method == nameof(DiceSpawner.RollDice) ||
                method == nameof(DiceSpawner.OnRoll) ||
                method == "Roll" ||
                method == "ToggleAutoRoll")
            {
                UnityEventTools.RemovePersistentListener(unityEvent, i);
            }
        }
    }

    static bool IsLikelyRollButton(Button button)
    {
        string name = button.name;
        return name.Contains("Attack", System.StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Roll", System.StringComparison.OrdinalIgnoreCase);
    }
}
#endif
