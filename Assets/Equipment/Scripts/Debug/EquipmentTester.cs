#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Play-mode harness: cycle through named sets on a keypress. Subclasses only pick the
// keys, so the three systems cannot drift apart in behaviour.
//
// Editor-only, so keep testers off anything that ships: a component left on a built
// prefab or scene deserializes as a missing script once the class is gone. Park them on
// a debug object in the test scene instead — `state` is assigned by hand, not fetched
// off this GameObject, so they work fine from anywhere in the scene.
public abstract class EquipmentTester<TState, TData, TSlot> : MonoBehaviour
    where TState : EquipmentState<TData, TSlot>
    where TData : EquipmentData<TSlot>
    where TSlot : struct, Enum
{
    [SerializeField] protected TState state;
    [SerializeField] EquipmentSet<TData>[] sets;
    int index = -1;

    protected abstract Key NextKey { get; }
    protected abstract Key ClearKey { get; }

    /// <summary>Appended to the log line when a set goes on. Null for nothing extra.</summary>
    protected virtual string Summary => null;

    /// <summary>Anything beyond next/clear. Only weapons use it so far.</summary>
    protected virtual void ReadExtraKeys(Keyboard keyboard) { }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || state == null) return;

        if (keyboard[NextKey].wasPressedThisFrame) Next();
        if (keyboard[ClearKey].wasPressedThisFrame) state.Clear();
        ReadExtraKeys(keyboard);
    }

    void Next()
    {
        if (sets == null || sets.Length == 0) return;

        state.Clear();
        index = (index + 1) % sets.Length;

        var set = sets[index];
        foreach (var piece in set.pieces)
            state.Equip(piece);

        Debug.Log(Summary == null ? set.setName : $"{set.setName} — {Summary}");
    }
}
#endif
