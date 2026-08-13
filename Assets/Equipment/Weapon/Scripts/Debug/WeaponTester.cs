#if UNITY_EDITOR
using UnityEngine.InputSystem;

public class WeaponTester : EquipmentTester<WeaponState, WeaponData, WeaponSlot>
{
    protected override Key NextKey => Key.Digit2;
    protected override Key ClearKey => Key.Digit9;

    // 3 draws or holsters everything at once.
    protected override void ReadExtraKeys(Keyboard keyboard)
    {
        if (keyboard[Key.Digit3].wasPressedThisFrame) state.ToggleAll();
    }
}
#endif
