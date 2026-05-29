using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TopQuestDisplay : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI detailText;
    public GameObject alertRed;

    private void OnEnable()
    {
        ValidateReferences();
        UpdateDisplay();
        if (GameServices.TryGet(out QuestManager quests))
            quests.OnQuestsUpdated += UpdateDisplay;
    }

    private void OnDisable()
    {
        if (GameServices.TryGet(out QuestManager quests))
            quests.OnQuestsUpdated -= UpdateDisplay;
    }

    private void ValidateReferences()
    {
        if (!titleText) titleText = transform.Find("TextMission")?.GetComponent<TextMeshProUGUI>();
        if (!detailText) detailText = transform.Find("TextMissionInfo")?.GetComponent<TextMeshProUGUI>();
        
        if (titleText) titleText.raycastTarget = false;
        if (detailText) detailText.raycastTarget = false;

        if (!alertRed) alertRed = transform.Find("Alert_Red")?.gameObject;

        if (alertRed)
        {
            foreach (var graphic in alertRed.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private void UpdateDisplay()
    {
        if (!GameServices.TryGet(out QuestManager qm))
            return;

        var q = qm.GetFirstActiveQuest();
        if (q != null)
        {
            if (titleText) titleText.text = q.title;
            if (detailText) detailText.text = $"{q.targetEnemyType} {q.currentProgress}/{q.targetAmount}";
        }
        else
        {
            if (titleText) titleText.text = "All Clear!";
            if (detailText) detailText.text = "No active quests";
        }

        if (alertRed)
        {
            int count = qm.GetClaimableRewardsCount();
            alertRed.SetActive(count > 0);
            var txt = alertRed.GetComponentInChildren<TextMeshProUGUI>();
            if (txt) txt.text = count.ToString();
        }
    }
}