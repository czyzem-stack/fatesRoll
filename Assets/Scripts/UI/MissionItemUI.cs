using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MissionItemUI : MonoBehaviour
{
    [Header("Core UI")]
    public TextMeshProUGUI nameAndObjectiveText;
    public Slider progressSlider;
    public Button claimButton;
    public GameObject itemAlertRed;
    public GameObject claimAlertRed;

    [Header("Rewards")]
    public Image rewardIcon;
    public TextMeshProUGUI rewardAmountText;
    
    [Header("Sprites")]
    public Sprite goldSprite;
    public Sprite gemSprite;

    private bool referencesValidated = false;

    public void Setup(Quest quest)
    {
        if (!referencesValidated) ValidateReferences();

        // 1. Text Formatting (Title on top, Objective below)
        if (nameAndObjectiveText)
        {
            // Use title and specific progress string: "Skeleton 0/2"
            // No color tags here to respect the original template look
            nameAndObjectiveText.text = $"{quest.title}\n<size=28>{quest.targetEnemyType} {quest.currentProgress}/{quest.targetAmount}</size>";
        }

        if (progressSlider)
        {
            progressSlider.maxValue = quest.targetAmount;
            progressSlider.value = quest.currentProgress;
        }

        if (rewardAmountText) rewardAmountText.text = quest.rewardAmount.ToString();
        
        if (rewardIcon)
            rewardIcon.sprite = (quest.rewardType == RewardType.Gold) ? goldSprite : gemSprite;

        bool isReadyToClaim = quest.isCompleted && !quest.isClaimed;
        bool isClaimed = quest.isClaimed;

        // 2. Alert Badge on Item
        if (itemAlertRed) 
        {
            itemAlertRed.SetActive(isReadyToClaim);
            var alertText = itemAlertRed.GetComponentInChildren<TextMeshProUGUI>();
            if (alertText) alertText.text = "!";
        }

        // 3. Claim Button Logic
        if (claimButton)
        {
            claimButton.gameObject.SetActive(true);
            var btnText = claimButton.GetComponentInChildren<TextMeshProUGUI>();
            
            if (isClaimed)
            {
                if (btnText) btnText.text = "Claimed";
                claimButton.interactable = false;
            }
            else
            {
                if (btnText) btnText.text = "Claim";
                claimButton.interactable = isReadyToClaim;
            }

            claimButton.onClick.RemoveAllListeners();
            if (isReadyToClaim)
                claimButton.onClick.AddListener(() => QuestManager.Instance.ClaimReward(quest));
        }

        // 4. Alert Badge on Button
        if (claimAlertRed) 
        {
            claimAlertRed.SetActive(isReadyToClaim);
            var alertText = claimAlertRed.GetComponentInChildren<TextMeshProUGUI>();
            if (alertText) alertText.text = "!";
        }
    }

    private void ValidateReferences()
    {
        // Absolute reset
        nameAndObjectiveText = null;
        rewardAmountText = null;
        rewardIcon = null;

        // 1. Core Mission Text
        Transform nameObj = transform.Find("MISSION_TITLE_OBJ_TEXT") ?? transform.Find("MISSION_TITLE_FIXED") ?? transform.Find("Mission NameObj") ?? transform.Find("Text (TMP)");
        if (nameObj)
        {
            nameAndObjectiveText = nameObj.GetComponent<TextMeshProUGUI>();
            if (nameAndObjectiveText) nameAndObjectiveText.raycastTarget = false;
        }

        // 2. Rewards
        Transform rewardGroup = transform.Find("MissionReward");
        if (rewardGroup)
        {
            Transform qtyObj = rewardGroup.Find("REWARD_AMOUNT_TEXT") ?? rewardGroup.Find("REWARD_QTY_TEXT") ?? rewardGroup.Find("Text (1)");
            if (qtyObj)
            {
                rewardAmountText = qtyObj.GetComponent<TextMeshProUGUI>();
                if (rewardAmountText) rewardAmountText.raycastTarget = false;
            }

            Transform iconObj = rewardGroup.Find("REWARD_ICON_IMAGE") ?? rewardGroup.Find("RewardType");
            if (iconObj)
            {
                rewardIcon = iconObj.GetComponent<Image>();
                if (rewardIcon) rewardIcon.raycastTarget = false;
            }
        }

        // 3. Slider and Button
        if (!progressSlider) progressSlider = transform.Find("Slider_Progress")?.GetComponent<Slider>();
        if (!claimButton) claimButton = transform.Find("Button_Claim")?.GetComponent<Button>();
        
        if (claimButton)
        {
            if (!claimAlertRed) claimAlertRed = claimButton.transform.Find("Alert_Red")?.gameObject;
            DisableRaycastsRecursive(claimAlertRed);
        }

        // 4. Alerts
        if (!itemAlertRed) itemAlertRed = transform.Find("Alert_Red")?.gameObject;
        DisableRaycastsRecursive(itemAlertRed);

        // 5. Decorative Icon
        var icon = transform.Find("Icon")?.GetComponent<Image>();
        if (icon) icon.raycastTarget = false;

        referencesValidated = true;
    }

    private void DisableRaycastsRecursive(GameObject go)
    {
        if (!go) return;
        foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }
}