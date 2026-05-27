using UnityEngine;

/// <summary>Stable entry point for UI buttons after DiceSpawner moved to Bootstrap (scene refs break).</summary>
public static class DiceRollGateway
{
    public static void Roll()
    {
        if (!GameServices.TryGet(out DiceSpawner spawner))
        {
            Debug.LogError("DiceRollGateway: DiceSpawner not registered with GameServices.");
            return;
        }

        spawner.RollDice();
    }

    public static void ToggleAutoRoll()
    {
        if (!GameServices.TryGet(out DiceSpawner spawner))
        {
            Debug.LogError("DiceRollGateway: DiceSpawner not registered with GameServices.");
            return;
        }

        spawner.ToggleAutoRoll();
    }
}
