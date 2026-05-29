using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public enum RewardType { Gold, Gem }

[Serializable]
public class Quest
{
    public string id;
    public string title;
    public string description;
    public POIType targetEnemyType;
    public int targetAmount;
    public int currentProgress;
    public RewardType rewardType;
    public int rewardAmount;
    public bool isCompleted;
    public bool isClaimed;

    public void OnEnemyKilled(POIType enemyType)
    {
        if (isCompleted || enemyType != targetEnemyType) return;

        currentProgress++;
        if (currentProgress >= targetAmount)
        {
            currentProgress = targetAmount;
            isCompleted = true;
        }
    }
}

[DefaultExecutionOrder(-100)]
public class QuestManager : GameServiceBehaviour<QuestManager>
{
    public List<Quest> activeQuests = new List<Quest>();
    public List<Quest> achievements = new List<Quest>();

    public event Action OnQuestsUpdated;

    protected override void Awake()
    {
        base.Awake();
        if (IsDataEmpty())
        {
            GenerateInitialData();
        }
    }

    private bool IsDataEmpty()
    {
        return (activeQuests == null || activeQuests.Count == 0 || (activeQuests.Count == 1 && string.IsNullOrEmpty(activeQuests[0].title))) &&
               (achievements == null || achievements.Count == 0 || (achievements.Count == 1 && string.IsNullOrEmpty(achievements[0].title)));
    }

    [ContextMenu("Reset and Generate Quests")]
    public void GenerateInitialData()
    {
        activeQuests.Clear();
        POIType[] enemies = { POIType.Skeleton, POIType.Orc, POIType.Slime, POIType.Bat, POIType.Spider };
        
        for (int i = 1; i <= 10; i++)
        {
            POIType enemy = enemies[UnityEngine.Random.Range(0, enemies.Length)];
            activeQuests.Add(new Quest
            {
                id = "quest_" + i,
                title = $"Slayer {i}",
                description = $"Exterminate {i * 2} {enemy}s",
                targetEnemyType = enemy,
                targetAmount = i * 2,
                rewardType = (i % 2 == 0) ? RewardType.Gem : RewardType.Gold,
                rewardAmount = i * 25
            });
        }

        achievements.Clear();
        for (int i = 1; i <= 10; i++)
        {
            achievements.Add(new Quest
            {
                id = "ach_" + i,
                title = $"Champion {i}",
                description = $"Slay {i * 5} Dragons",
                targetEnemyType = POIType.Dragon,
                targetAmount = i * 5,
                rewardType = RewardType.Gem,
                rewardAmount = i * 100
            });
        }
        
        OnQuestsUpdated?.Invoke();
        Debug.Log("[QuestManager] Generated 10 slayer quests and 10 champion achievements.");
    }

    public void NotifyEnemyKilled(POIType enemyType)
    {
        foreach (var q in activeQuests) q.OnEnemyKilled(enemyType);
        foreach (var a in achievements) a.OnEnemyKilled(enemyType);
        OnQuestsUpdated?.Invoke();
    }

    public void ClaimReward(Quest quest)
    {
        if (quest.isCompleted && !quest.isClaimed)
        {
            quest.isClaimed = true;
            if (quest.rewardType == RewardType.Gold)
                LootManager.Instance?.AddGold(quest.rewardAmount);
            else
                LootManager.Instance?.AddGems(quest.rewardAmount);
            
            OnQuestsUpdated?.Invoke();
        }
    }

    public Quest GetFirstActiveQuest()
    {
        return activeQuests.FirstOrDefault(q => !q.isClaimed);
    }

    public string GetTopQuestInfo()
    {
        var q = GetFirstActiveQuest();
        return q != null ? $"{q.title}: {q.currentProgress}/{q.targetAmount}" : "All quests completed!";
    }

    public bool HasClaimableRewards()
    {
        return GetClaimableRewardsCount() > 0;
    }

    public int GetClaimableRewardsCount()
    {
        return activeQuests.Count(q => q.isCompleted && !q.isClaimed) + 
               achievements.Count(a => a.isCompleted && !a.isClaimed);
    }
}



