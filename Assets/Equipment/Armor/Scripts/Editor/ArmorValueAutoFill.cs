using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Fills combat values from the armor asset naming convention while preserving color variants.
public static class ArmorValueAutoFill
{
    private static readonly Regex Pattern = new(@"Type_(\d+)_Color_\d+$");
    private static readonly Dictionary<ArmorSlot, float> SlotWeights = new()
    {
        [ArmorSlot.Chest] = 0.30f,
        [ArmorSlot.Legs] = 0.25f,
        [ArmorSlot.Head] = 0.20f,
        [ArmorSlot.Arms] = 0.10f,
        [ArmorSlot.Feet] = 0.10f,
        [ArmorSlot.Belt] = 0.05f
    };

    private static readonly int[] TierTotals = { 10, 22, 38, 58, 82, 110 };

    [MenuItem("Tools/Auto Fill Armor Values")]
    private static void Fill()
    {
        var armorData = AssetDatabase.FindAssets("t:ArmorData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ArmorData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(data => data != null)
            .Select(data => (data, match: Pattern.Match(data.name)))
            .Where(x => x.match.Success)
            .ToArray();

        int updated = 0;
        foreach (var group in armorData.GroupBy(x => int.Parse(x.match.Groups[1].Value)))
        {
            if (group.Key < 1 || group.Key > TierTotals.Length)
            {
                Debug.LogWarning($"Skipping armor tier Type_{group.Key}: no configured total.");
                continue;
            }

            int tierTotal = TierTotals[group.Key - 1];
            int remaining = tierTotal;
            var slots = group.GroupBy(x => x.data.slot)
                .OrderByDescending(x => SlotWeights[x.Key])
                .ToArray();
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                int value = i == slots.Length - 1
                    ? Mathf.Max(0, remaining)
                    : Mathf.RoundToInt(tierTotal * SlotWeights[slot.Key]);
                remaining -= value;

                foreach (var entry in slot)
                {
                    var so = new SerializedObject(entry.data);
                    so.FindProperty("armor").intValue = value;
                    so.ApplyModifiedProperties();
                    updated++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Filled armor values for {updated} assets.");
    }
}