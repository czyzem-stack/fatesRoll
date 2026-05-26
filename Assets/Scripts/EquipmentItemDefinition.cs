using UnityEngine;

[CreateAssetMenu(menuName = "FatesRoll/Equipment Item", fileName = "EquipmentItem")]
public class EquipmentItemDefinition : ScriptableObject
{
    [Tooltip("Unique id (usually prefab or body name).")]
    public string itemId;

    public string displayName;
    public EquipmentSlotType slot = EquipmentSlotType.MainHand;
    public EquipmentChestCategory chestCategory = EquipmentChestCategory.Weapon;

    [Header("Visual (optional — stat-only slots leave empty)")]
    [Tooltip("Instantiated under the rig socket (weapons, head pieces, capes).")]
    public GameObject visualPrefab;

    [Tooltip("For body armor: enable this child on the rig (e.g. Body07).")]
    public string rigChildName;

    [Tooltip("Use rig child toggle instead of spawning visualPrefab.")]
    public bool useRigChildToggle;

    public bool IsStatOnlySlot =>
        slot == EquipmentSlotType.Ring ||
        slot == EquipmentSlotType.Necklace ||
        slot == EquipmentSlotType.Boots ||
        slot == EquipmentSlotType.Gloves;

    public bool HasVisual => !IsStatOnlySlot && (visualPrefab != null || useRigChildToggle);
}
