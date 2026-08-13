using System;
using System.Collections.Generic;
using UnityEngine;

// View for the skinned attachment systems: one child SkinnedMeshRenderer per filled slot,
// driven by the character's shared skeleton. Owns no state — it mirrors whatever the
// matching EquipmentState on this GameObject says.
// e.g. AppearanceRenderer : EquipmentRenderer<AppearanceData, AppearanceSlot>
public abstract class EquipmentRenderer<TData, TSlot> : MonoBehaviour
    where TData : EquipmentData<TSlot>
    where TSlot : struct, Enum
{
    [SerializeField] protected CharacterRig rig;

    readonly Dictionary<TSlot, GameObject> spawned = new();
    EquipmentState<TData, TSlot> state;

    void Awake()
    {
        state = GetComponent<EquipmentState<TData, TSlot>>();
        if (state == null)
            Debug.LogError($"{name}: no EquipmentState<{typeof(TData).Name}> beside this renderer.", this);
    }

    void OnEnable()
    {
        if (state == null) return;

        state.Changed += Apply;

        // Catch up on anything equipped while this renderer was off.
        foreach (var entry in state.Worn)
            Apply(entry.Key, entry.Value);
    }

    void OnDisable()
    {
        if (state != null) state.Changed -= Apply;
    }

    void Apply(TSlot slot, TData item)
    {
        Despawn(slot);
        if (item != null && item.mesh != null) Spawn(slot, item);
    }

    void Spawn(TSlot slot, TData item)
    {
        var go = new GameObject($"{slot}_{item.name}");
        go.transform.SetParent(transform, false);

        var dst = go.AddComponent<SkinnedMeshRenderer>();
        dst.sharedMesh = item.mesh;
        dst.sharedMaterials = item.materials;

        // Parts are exported against this armature, so their bindposes line up with it
        // index for index. Unity copies the array on assignment, so sharing one is safe.
        dst.bones = rig.Bones;
        dst.rootBone = rig.BaseBody.rootBone;
        dst.localBounds = rig.BaseBody.localBounds;

        spawned[slot] = go;
    }

    void Despawn(TSlot slot)
    {
        if (spawned.TryGetValue(slot, out var go) && go != null) Destroy(go);
        spawned.Remove(slot);
    }
}
