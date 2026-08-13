using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Rebuilds the scene ArmorTester's set list from every ArmorData in the project,
// so new armor shows up without hand-wiring the inspector.
public static class ArmorSetAutoFill
{
    // Tail of "Equip_Arm_Armor_Type_1_Color_1". Colour variants are not sets of their
    // own yet, so the pattern itself keeps only Color_1 and captures the type number.
    static readonly Regex Pattern = new(@"Type_(\d+)_Color_1$");

    [MenuItem("Tools/Auto Fill Armor Sets")]
    static void Fill()
    {
        var tester = Object.FindFirstObjectByType<ArmorTester>();
        if (tester == null) { Debug.LogError("No ArmorTester in the scene."); return; }

        var sets = AssetDatabase.FindAssets("t:ArmorData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ArmorData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(def => def != null)
            .Select(def => (def, match: Pattern.Match(def.name)))
            .Where(x => x.match.Success)
            .GroupBy(x => int.Parse(x.match.Groups[1].Value))
            .OrderBy(g => g.Key)
            .Select(g => (
                name: $"Type {g.Key}",
                pieces: g.Select(x => x.def).OrderBy(d => d.slot).ToArray()))
            .ToArray();

        // One SerializedObject pass: a single undo entry, and no reflection to go stale
        // the day the field is renamed. Assigning arraySize handles both grow and shrink,
        // and every field below is overwritten, so leftovers from the old list cannot survive.
        var so = new SerializedObject(tester);
        var setsProp = so.FindProperty("sets");
        setsProp.arraySize = sets.Length;

        for (int i = 0; i < sets.Length; i++)
        {
            var set = setsProp.GetArrayElementAtIndex(i);
            set.FindPropertyRelative("setName").stringValue = sets[i].name;

            var pieces = set.FindPropertyRelative("pieces");
            pieces.arraySize = sets[i].pieces.Length;
            for (int j = 0; j < sets[i].pieces.Length; j++)
                pieces.GetArrayElementAtIndex(j).objectReferenceValue = sets[i].pieces[j];
        }

        so.ApplyModifiedProperties();
        Debug.Log($"Filled {sets.Length} sets, {sets.Sum(s => s.pieces.Length)} pieces total.");
    }
}
