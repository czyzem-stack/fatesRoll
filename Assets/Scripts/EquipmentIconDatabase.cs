using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FatesRoll/Equipment Icon Database", fileName = "EquipmentIconDatabase")]
public class EquipmentIconDatabase : ScriptableObject
{
    [System.Serializable]
    public class SlotIcons
    {
        public EquipmentSlotType slot;
        public Sprite[] icons = System.Array.Empty<Sprite>();
    }

    public SlotIcons[] iconsBySlot = System.Array.Empty<SlotIcons>();

    private Dictionary<EquipmentSlotType, Sprite[]> lookup;

    public Sprite PickRandomIcon(EquipmentSlotType slot)
    {
        EnsureLookup();
        if (!lookup.TryGetValue(slot, out Sprite[] pool) || pool == null || pool.Length == 0)
            return null;
        return pool[Random.Range(0, pool.Length)];
    }

    void EnsureLookup()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<EquipmentSlotType, Sprite[]>();
        if (iconsBySlot == null)
            return;

        foreach (var entry in iconsBySlot)
        {
            if (entry?.icons == null || entry.icons.Length == 0)
                continue;
            lookup[entry.slot] = entry.icons;
        }
    }

    public void SetIconsForSlot(EquipmentSlotType slot, Sprite[] sprites)
    {
        if (iconsBySlot == null)
            iconsBySlot = System.Array.Empty<SlotIcons>();

        for (int i = 0; i < iconsBySlot.Length; i++)
        {
            if (iconsBySlot[i].slot != slot)
                continue;
            iconsBySlot[i].icons = sprites ?? System.Array.Empty<Sprite>();
            lookup = null;
            return;
        }

        var list = new List<SlotIcons>(iconsBySlot);
        list.Add(new SlotIcons { slot = slot, icons = sprites ?? System.Array.Empty<Sprite>() });
        iconsBySlot = list.ToArray();
        lookup = null;
    }
}
