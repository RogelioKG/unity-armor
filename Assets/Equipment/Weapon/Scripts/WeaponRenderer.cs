using System.Collections.Generic;
using UnityEngine;

// View for WeaponState: parents a static mesh to the socket the current hold state calls
// for. Owns no state — it mirrors the model on this GameObject. Two events, because the
// two things that can change need different work: a new weapon respawns, a holster move
// only re-parents.
public class WeaponRenderer : MonoBehaviour
{
    [SerializeField] CharacterRig rig;

    readonly Dictionary<WeaponSlot, GameObject> spawned = new();
    WeaponState state;

    void Awake()
    {
        state = GetComponent<WeaponState>();
        if (state == null)
            Debug.LogError($"{name}: no WeaponState beside this renderer.", this);
    }

    void OnEnable()
    {
        if (state == null) return;

        state.Changed += Spawn;
        state.HoldChanged += Reposition;

        // Catch up on anything equipped while this renderer was off.
        foreach (var entry in state.Worn)
            Spawn(entry.Key, entry.Value);
    }

    void OnDisable()
    {
        if (state == null) return;

        state.Changed -= Spawn;
        state.HoldChanged -= Reposition;
    }

    void Spawn(WeaponSlot slot, WeaponData def)
    {
        Despawn(slot);
        if (def == null || def.mesh == null) return;

        var go = new GameObject($"{slot}_{def.name}");
        if (!TryPlace(go, def, def.PlacementFor(state.GetHold(slot)))) return;

        go.AddComponent<MeshFilter>().sharedMesh = def.mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = def.materials;
        spawned[slot] = go;
    }

    void Reposition(WeaponSlot slot, WeaponHoldState hold)
    {
        var def = state.Get(slot);
        if (def == null || !spawned.TryGetValue(slot, out var go) || go == null) return;

        // TryPlace already destroyed it on failure, so only the bookkeeping is left.
        if (!TryPlace(go, def, def.PlacementFor(hold))) spawned.Remove(slot);
    }

    /// <summary>Parents the object to its socket. Destroys it and fails when the socket is
    /// missing: the model has already changed, so a stale visual would contradict it.</summary>
    bool TryPlace(GameObject go, WeaponData def, SocketPlacement placement)
    {
        var socket = rig.ResolveSocketOrWarn(placement.socket, def.name);
        if (socket == null)
        {
            Destroy(go);
            return false;
        }

        var t = go.transform;
        t.SetParent(socket, false);
        t.SetLocalPositionAndRotation(
            placement.positionOffset,
            Quaternion.Euler(placement.rotationEulerOffset));
        t.localScale = Vector3.one;
        return true;
    }

    void Despawn(WeaponSlot slot)
    {
        if (spawned.TryGetValue(slot, out var go) && go != null) Destroy(go);
        spawned.Remove(slot);
    }
}
