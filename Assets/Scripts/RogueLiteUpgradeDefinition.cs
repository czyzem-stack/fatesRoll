/// <summary>Runtime offer pair entry (stat + config snapshot).</summary>
public struct RogueLiteOffer
{
    public RogueLiteUpgradeType upgradeType;
    public RogueLiteStatConfig config;

    public RogueLiteOffer(RogueLiteUpgradeType type, RogueLiteStatConfig statConfig)
    {
        upgradeType = type;
        config = statConfig;
    }

    public float GetNextBonusAmount(int timesAlreadyChosen) =>
        config != null ? config.GetScaledAmount(timesAlreadyChosen) : 0f;
}
