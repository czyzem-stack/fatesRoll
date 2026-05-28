using UnityEngine;
using TMPro;

public class UpgradeAlertController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private GameObject alertVisual;

    private void Update()
    {
        if (TalentManager.Instance == null) return;

        int affordableCount = TalentManager.Instance.GetAffordableUpgradeCount();
        
        if (affordableCount > 0)
        {
            if (alertVisual != null && !alertVisual.activeSelf) alertVisual.SetActive(true);
            if (countText != null) countText.text = affordableCount.ToString();
        }
        else
        {
            if (alertVisual != null && alertVisual.activeSelf) alertVisual.SetActive(false);
        }
    }
}
