using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Home menu Upgrade button badge — shows how many talent upgrades current gold can buy.
/// </summary>
public class UpgradeAlertController : MonoBehaviour
{
    private static readonly string[] UpgradeAlertPaths =
    {
        "MainUI_Canvas/Home/HomeMenu/Upgrade/Alert_Green",
        "MainUI_Canvas/HomeMenu/Upgrade/Alert_Green",
        "MainUI_Canvas/Panel_Home/HomeMenu/Upgrade/Alert_Green",
    };

    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private GameObject alertVisual;

    private int lastDisplayedCount = -1;

    private void OnEnable()
    {
        LootManager.BalanceChanged += HandleBalanceChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindUi();
        Refresh();
    }

    private void OnDisable()
    {
        LootManager.BalanceChanged -= HandleBalanceChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBindUi();
        Refresh();
    }

    private void HandleBalanceChanged()
    {
        Refresh();
    }

    private void TryBindUi()
    {
        if (countText != null && alertVisual != null)
            return;

        foreach (string path in UpgradeAlertPaths)
        {
            var alertGo = GameObject.Find(path);
            if (alertGo == null)
                continue;

            if (alertVisual == null)
                alertVisual = alertGo;

            if (countText == null)
            {
                var countTransform = alertGo.transform.Find("Text_Count");
                if (countTransform != null)
                    countText = countTransform.GetComponent<TextMeshProUGUI>();
                if (countText == null)
                    countText = alertGo.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (countText != null && alertVisual != null)
                return;
        }

        if (countText != null && alertVisual != null)
            return;

        var canvas = GameObject.Find("MainUI_Canvas");
        if (canvas == null)
            return;

        foreach (var tmp in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name != "Text_Count")
                continue;

            Transform alert = tmp.transform.parent;
            if (alert == null || alert.name != "Alert_Green")
                continue;

            Transform upgrade = alert.parent;
            if (upgrade == null || upgrade.name != "Upgrade")
                continue;

            countText = tmp;
            alertVisual = alert.gameObject;
            return;
        }
    }

    private void Refresh()
    {
        if (countText == null || alertVisual == null)
            TryBindUi();

        if (!GameServices.TryGet(out TalentManager talents))
            return;

        int affordableCount = talents.GetAffordableUpgradeCount();
        if (affordableCount == lastDisplayedCount)
            return;

        lastDisplayedCount = affordableCount;

        if (affordableCount > 0)
        {
            if (alertVisual != null)
                alertVisual.SetActive(true);
            if (countText != null)
                countText.text = affordableCount.ToString();
        }
        else
        {
            if (alertVisual != null)
                alertVisual.SetActive(false);
            if (countText != null)
                countText.text = "0";
        }
    }
}
