using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using UnityEngine.UI;

public class MissionPanelUI : MonoBehaviour
{
    [Header("List Settings")]
    public Transform contentRoot;
    public GameObject missionPrefab;

    [Header("Resources")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI gemText;

    [Header("Tab UI")]
    public GameObject tab0_Focus, tab0_Normal, tab0_Line, tab0_Alert;
    public GameObject tab1_Focus, tab1_Normal, tab1_Line, tab1_Alert;

    [Header("Sprites")]
    public Sprite goldSprite;
    public Sprite gemSprite;

    private GameObject cachedGlobalHUD;
    private bool showingAchievements = false;
    private readonly List<MissionItemUI> spawnedItems = new List<MissionItemUI>();

    private void Awake()
    {
        cachedGlobalHUD = GameObject.Find("MainUI_Canvas/Resources");
        InitializeReferences();
        InitializeTabs();
    }

    private void InitializeReferences()
    {
        if (!contentRoot) contentRoot = transform.Find("ScrollRect/Veiwport/Content");
        
        if (contentRoot && !missionPrefab)
            missionPrefab = contentRoot.Find("Template")?.gameObject;

        if (missionPrefab)
        {
            missionPrefab.SetActive(false);
            missionPrefab.transform.Find("Alert_Red")?.gameObject.SetActive(false);
        }

        if (contentRoot)
        {
            foreach (Transform child in contentRoot)
            {
                if (child.gameObject != missionPrefab && child.name != "MissionList_Complete")
                {
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    private void InitializeTabs()
    {
        var tabMenu = transform.Find("TabMenu");
        if (!tabMenu) return;

        var tabs = tabMenu.Cast<Transform>().Where(t => t.name == "Tab").ToList();
        if (tabs.Count < 2) return;

        // Assign Tab 0 (Daily)
        AssignTabRefs(tabs[0], ref tab0_Focus, ref tab0_Normal, ref tab0_Line, ref tab0_Alert, ShowQuests);
        // Assign Tab 1 (Achievements)
        AssignTabRefs(tabs[1], ref tab1_Focus, ref tab1_Normal, ref tab1_Line, ref tab1_Alert, ShowAchievements);
    }

    private void AssignTabRefs(Transform tab, ref GameObject focus, ref GameObject normal, ref GameObject line, ref GameObject alert, UnityEngine.Events.UnityAction action)
    {
        if (!focus) focus = tab.Find("TabFocus")?.gameObject;
        if (!normal) normal = tab.Find("TabNormal")?.gameObject;
        if (!line) line = tab.Find("TabFocusLine")?.gameObject;
        if (!alert) alert = tab.Find("Alert_Red")?.gameObject;
        
        var btn = tab.GetComponent<Button>() ?? tab.gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    private void OnEnable()
    {
        if (cachedGlobalHUD) cachedGlobalHUD.SetActive(false);
        UpdateUI();
        if (GameServices.TryGet(out QuestManager quests))
            quests.OnQuestsUpdated += UpdateUI;
    }

    private void OnDisable()
    {
        if (cachedGlobalHUD) cachedGlobalHUD.SetActive(true);
        if (GameServices.TryGet(out QuestManager quests))
            quests.OnQuestsUpdated -= UpdateUI;
    }

    public void ShowQuests() { showingAchievements = false; UpdateUI(); }
    public void ShowAchievements() { showingAchievements = true; UpdateUI(); }

    public void UpdateUI()
    {
        if (!GameServices.TryGet(out QuestManager qm))
            return;

        UpdateTopBanner(qm);
        UpdateResources();
        UpdateTabStates(qm);
        RefreshList(qm);
    }

    private void UpdateTopBanner(QuestManager qm)
    {
        var dailySlot = transform.Find("MissionList_Daily");
        if (!dailySlot) return;

        var topQuest = qm.activeQuests.FirstOrDefault(q => !q.isClaimed);
        dailySlot.gameObject.SetActive(topQuest != null);
        if (topQuest == null) return;

        var txt = dailySlot.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(t => t.name == "MISSION_TITLE_OBJ_TEXT" || t.name == "Text (TMP)" || t.text.Contains("Daily"));
        
        if (txt) txt.text = $"{topQuest.title}\n<size=28>{topQuest.targetEnemyType} {topQuest.currentProgress}/{topQuest.targetAmount}</size>";

        var rewardGroup = dailySlot.Find("MissionReward");
        if (rewardGroup)
        {
            var qtyTxt = rewardGroup.Find("REWARD_AMOUNT_TEXT")?.GetComponent<TextMeshProUGUI>() ?? rewardGroup.Find("Text (1)")?.GetComponent<TextMeshProUGUI>();
            if (qtyTxt) qtyTxt.text = topQuest.rewardAmount.ToString();

            var iconImg = rewardGroup.Find("REWARD_ICON_IMAGE")?.GetComponent<Image>() ?? rewardGroup.Find("RewardType")?.GetComponent<Image>();
            if (iconImg) iconImg.sprite = (topQuest.rewardType == RewardType.Gold) ? goldSprite : gemSprite;
        }
    }


    private void UpdateResources()
    {
        if (!LootManager.Instance) return;
        if (goldText) goldText.text = LootManager.Instance.CurrentGold.ToString("N0");
        if (gemText) gemText.text = LootManager.Instance.CurrentGems.ToString("N0");
    }

    private void UpdateTabStates(QuestManager qm)
    {
        if (tab0_Focus) tab0_Focus.SetActive(!showingAchievements);
        if (tab0_Normal) tab0_Normal.SetActive(showingAchievements);
        if (tab0_Line) tab0_Line.SetActive(!showingAchievements);
        UpdateTabAlert(tab0_Alert, qm.activeQuests);
        
        if (tab1_Focus) tab1_Focus.SetActive(showingAchievements);
        if (tab1_Normal) tab1_Normal.SetActive(!showingAchievements);
        if (tab1_Line) tab1_Line.SetActive(showingAchievements);
        UpdateTabAlert(tab1_Alert, qm.achievements);
    }

    private void UpdateTabAlert(GameObject alertObj, List<Quest> list)
    {
        if (!alertObj) return;
        int count = list.Count(q => q.isCompleted && !q.isClaimed);
        alertObj.SetActive(count > 0);
        var txt = alertObj.GetComponentInChildren<TextMeshProUGUI>();
        if (txt) txt.text = count.ToString();
    }

    private void RefreshList(QuestManager qm)
    {
        if (!contentRoot || !missionPrefab) return;

        spawnedItems.RemoveAll(item => item == null);
        var questList = showingAchievements ? qm.achievements : qm.activeQuests;

        while (spawnedItems.Count < questList.Count)
        {
            var go = Instantiate(missionPrefab, contentRoot);
            var item = go.GetComponent<MissionItemUI>() ?? go.AddComponent<MissionItemUI>();
            
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = 124;

            item.goldSprite = goldSprite;
            item.gemSprite = gemSprite;
            spawnedItems.Add(item);
        }

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            bool inRange = i < questList.Count;
            spawnedItems[i].gameObject.SetActive(inRange);
            if (inRange)
            {
                spawnedItems[i].name = (showingAchievements ? "Achievement_" : "Quest_") + questList[i].id;
                spawnedItems[i].Setup(questList[i]);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot.GetComponent<RectTransform>());
    }
}
