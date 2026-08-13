using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates ArmorData / AppearanceData / WeaponData assets from one-mesh-per-file FBX models.
/// Select FBX files in the Project window, then hit Extract.
///
/// The slot comes from the filename prefix, which must match an enum member, and the
/// asset keeps the filename:
///     Arms_Type_1_Color_1.fbx -> ArmorSlot.Arms
///     Hair_Type_2_Color_3.fbx -> AppearanceSlot.Hair
///     Mainhand_Sword.fbx      -> WeaponSlot.MainHand
///
/// Re-running updates the visual fields only, so displayName, stats and hand-tuned
/// socket placements survive.
///
/// Skinned parts store no bone list — they share the rig's skeleton index for index.
/// A part that does not match the reference rig is reported and left unwritten, since
/// the mismatch would otherwise surface only as a silently deformed mesh.
/// </summary>
public class EquipmentDataExtractor : EditorWindow
{
    const string ReferenceRigKey = "EquipmentDataExtractor.ReferenceRig";

    string armorFolder = "Assets/Equipment/Armor/Data/Polyguy";
    string appearanceFolder = "Assets/Equipment/Appearance/Data/Polyguy";
    string weaponFolder = "Assets/Equipment/Weapon/Data";
    GameObject referenceRig;
    Vector2 scroll;

    // ---------------- Window ----------------

    [MenuItem("Tools/Equipment Data Extractor")]
    static void Open() => GetWindow<EquipmentDataExtractor>("Equipment Data Extractor");

    void OnEnable()
    {
        var guid = EditorPrefs.GetString(ReferenceRigKey, "");
        if (!string.IsNullOrEmpty(guid))
            referenceRig = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Select FBX models in the Project window.", EditorStyles.boldLabel);
        armorFolder = EditorGUILayout.TextField("Armor folder", armorFolder);
        appearanceFolder = EditorGUILayout.TextField("Appearance folder", appearanceFolder);
        weaponFolder = EditorGUILayout.TextField("Weapon folder", weaponFolder);

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        referenceRig = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Reference rig", "Character prefab holding the CharacterRig every skinned part is checked against."),
            referenceRig, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            var rigPath = AssetDatabase.GetAssetPath(referenceRig);
            EditorPrefs.SetString(ReferenceRigKey,
                string.IsNullOrEmpty(rigPath) ? "" : AssetDatabase.AssetPathToGUID(rigPath));
        }

        // Shallow check only — OnGUI repaints constantly and the full one walks the skeleton.
        if (referenceRig == null || referenceRig.GetComponentInChildren<CharacterRig>(true) == null)
            EditorGUILayout.HelpBox(
                "Assign a character prefab with a CharacterRig and its base body set. "
                + "Skinned parts are skipped until then; weapons extract either way.",
                MessageType.Warning);

        EditorGUILayout.Space();

        var models = Selection.objects
            .Select(AssetDatabase.GetAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Where(p => AssetImporter.GetAtPath(p) is ModelImporter)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
        EditorGUILayout.LabelField($"Selected: {models.Count}");

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var path in models)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            EditorGUILayout.LabelField($"{fileName}   {Describe(fileName)}");
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(models.Count == 0))
            if (GUILayout.Button("Extract", GUILayout.Height(28))) Extract(models);
    }

    // ---------------- Extraction ----------------

    void Extract(List<string> modelPaths)
    {
        int created = 0, updated = 0, skipped = 0;
        var reference = ReferenceBones(referenceRig);

        foreach (var path in modelPaths)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);

            // null means nothing was written: no matching slot, a bad import, or a
            // skeleton that does not line up with the rig.
            bool? isNew = null;

            if (TryParseSlot(fileName, out ArmorSlot armor))
                isNew = ExtractSkinned<ArmorData, ArmorSlot>(armorFolder, path, fileName, armor, reference);
            else if (TryParseSlot(fileName, out AppearanceSlot appearance))
                isNew = ExtractSkinned<AppearanceData, AppearanceSlot>(appearanceFolder, path, fileName, appearance, reference);
            else if (TryParseSlot(fileName, out WeaponSlot weapon))
                isNew = ExtractWeapon(weaponFolder, path, fileName, weapon);
            else
                Debug.LogWarning($"Prefix on '{fileName}' matches no slot, skipped.");

            if (isNew == null) skipped++;
            else if (isNew.Value) created++;
            else updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Data — created {created}, updated {updated}, skipped {skipped}.");
    }

    // ---------------- Per-kind extraction ----------------

    /// <summary>Loads, validates and writes one skinned asset (armor or appearance).
    /// Null when nothing was written; otherwise whether the asset was newly created.</summary>
    static bool? ExtractSkinned<TData, TSlot>(string folder, string path, string fileName, TSlot slot, string[] reference)
        where TData : EquipmentData<TSlot>
        where TSlot : struct, Enum
    {
        var smr = LoadSkinned(path, fileName);
        if (smr == null || !MatchesReference(fileName, smr, reference)) return null;

        var def = LoadOrCreate<TData>(folder, fileName, out bool isNew);
        def.slot = slot;
        def.mesh = smr.sharedMesh;
        def.materials = smr.sharedMaterials;

        WarnOnMaterials(fileName, def.materials);
        Save(def, folder, fileName, isNew);
        return isNew;
    }

    /// <summary>Loads and writes one weapon asset.
    /// Null when nothing was written; otherwise whether the asset was newly created.</summary>
    static bool? ExtractWeapon(string folder, string path, string fileName, WeaponSlot slot)
    {
        var mf = LoadStatic(path, fileName);
        if (mf == null) return null;

        var def = LoadOrCreate<WeaponData>(folder, fileName, out bool isNew);

        if (isNew)
        {
            // Seeded once so a fresh weapon shows up in the right hand. Offsets stay zero
            // until tuned in the Scene view, and a re-run never touches them.
            def.drawn.socket = slot == WeaponSlot.OffHand ? SocketId.OffHandGrip : SocketId.MainHandGrip;
            def.holstered.socket = SocketId.BackMount;
        }

        def.slot = slot;
        def.mesh = mf.sharedMesh;
        def.materials = mf.TryGetComponent<MeshRenderer>(out var mr) ? mr.sharedMaterials : Array.Empty<Material>();

        WarnOnMaterials(fileName, def.materials);
        Save(def, folder, fileName, isNew);
        return isNew;
    }

    static T LoadOrCreate<T>(string folder, string fileName, out bool isNew) where T : ScriptableObject
    {
        Directory.CreateDirectory(folder);

        var def = AssetDatabase.LoadAssetAtPath<T>($"{folder}/{fileName}.asset");
        isNew = def == null;
        if (!isNew) return def;

        def = CreateInstance<T>();
        // Set once on creation, never overwritten by a re-run.
        if (def is EquipmentData equipment) equipment.displayName = fileName.Replace('_', ' ');
        return def;
    }

    static void Save(ScriptableObject def, string folder, string fileName, bool isNew)
    {
        if (isNew) AssetDatabase.CreateAsset(def, $"{folder}/{fileName}.asset");
        else EditorUtility.SetDirty(def);
    }

    // ---------------- Validation ----------------

    /// <summary>Bones of the rig's base body, or null when no usable rig is assigned.
    /// This is the very array EquipmentRenderer hands to every part at runtime.</summary>
    static string[] ReferenceBones(GameObject rigPrefab)
    {
        if (rigPrefab == null) return null;

        var rig = rigPrefab.GetComponentInChildren<CharacterRig>(true);
        if (rig == null) return null;

        // Reaching through SerializedObject for the private baseBody keeps the check
        // honest: it validates against what the renderer will actually use.
        var baseBody = new SerializedObject(rig).FindProperty("baseBody").objectReferenceValue as SkinnedMeshRenderer;
        return baseBody == null ? null : BoneNames(baseBody);
    }

    static string[] BoneNames(SkinnedMeshRenderer smr)
        => Array.ConvertAll(smr.bones, b => b != null ? b.name : null);

    /// <summary>True when the part's skeleton lines up with the reference index for index.
    /// Anything else is an error, not a warning: a mismatch deforms the mesh at runtime
    /// and raises nothing on its own.</summary>
    static bool MatchesReference(string fileName, SkinnedMeshRenderer smr, string[] reference)
    {
        const string Fix = "Re-export it against the shared armature, with the same vertex groups as the base mesh.";

        if (reference == null)
        {
            Debug.LogError($"'{fileName}' skipped: assign a reference rig in the Equipment Data Extractor first.");
            return false;
        }

        var bones = BoneNames(smr);
        if (bones.Length != reference.Length)
        {
            Debug.LogError($"'{fileName}' is skinned to {bones.Length} bones, the rig has {reference.Length}. {Fix}");
            return false;
        }

        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != reference[i])
            {
                Debug.LogError($"'{fileName}' bone {i} is '{bones[i]}', the rig has '{reference[i]}'. {Fix}");
                return false;
            }

        return true;
    }

    static void WarnOnMaterials(string fileName, Material[] materials)
    {
        if (materials.Length == 0)
            Debug.LogWarning($"'{fileName}' has no materials.");
        else if (materials.Any(m => m == null))
            Debug.LogWarning($"'{fileName}' has unassigned materials — remap them on the FBX importer.");
    }

    // ---------------- Model loading ----------------

    static SkinnedMeshRenderer LoadSkinned(string path, string fileName)
    {
        var smr = PickSingle<SkinnedMeshRenderer>(path, fileName, "check the Rig import setting.");
        return smr != null && HasMesh(smr.sharedMesh, fileName) ? smr : null;
    }

    static MeshFilter LoadStatic(string path, string fileName)
    {
        var mf = PickSingle<MeshFilter>(path, fileName, "a weapon must import with Rig: None.");
        return mf != null && HasMesh(mf.sharedMesh, fileName) ? mf : null;
    }

    static T PickSingle<T>(string path, string fileName, string hint) where T : Component
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root == null)
        {
            Debug.LogWarning($"Could not load '{path}', skipped.");
            return null;
        }

        var found = root.GetComponentsInChildren<T>(true);
        if (found.Length == 0)
        {
            Debug.LogWarning($"No {typeof(T).Name} in '{fileName}' — {hint}");
            return null;
        }
        if (found.Length > 1)
            Debug.LogWarning($"'{fileName}' holds {found.Length} meshes; only the first is used. Re-export one mesh per file.");

        return found[0];
    }

    static bool HasMesh(Mesh mesh, string fileName)
    {
        if (mesh != null) return true;
        Debug.LogWarning($"No mesh on '{fileName}', skipped.");
        return false;
    }

    // ---------------- Filename parsing ----------------

    /// <summary>Parses the filename prefix as a slot enum. Rejects numeric prefixes,
    /// which Enum.TryParse would otherwise accept as raw underlying values.</summary>
    static bool TryParseSlot<TSlot>(string fileName, out TSlot slot) where TSlot : struct, Enum
    {
        slot = default;
        var prefix = fileName.Split('_')[0];
        return prefix.Length > 0
            && char.IsLetter(prefix[0])
            && Enum.TryParse(prefix, ignoreCase: true, out slot);
    }

    static string Describe(string fileName) =>
          TryParseSlot(fileName, out ArmorSlot armor) ? $"Armor / {armor}"
        : TryParseSlot(fileName, out AppearanceSlot appearance) ? $"Appearance / {appearance}"
        : TryParseSlot(fileName, out WeaponSlot weapon) ? $"Weapon / {weapon}"
        : "UNMAPPED";
}
