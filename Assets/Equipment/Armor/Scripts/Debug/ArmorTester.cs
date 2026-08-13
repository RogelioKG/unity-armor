#if UNITY_EDITOR
using UnityEngine.InputSystem;

public class ArmorTester : EquipmentTester<ArmorState, ArmorData, ArmorSlot>
{
    protected override Key NextKey => Key.Digit1;
    protected override Key ClearKey => Key.Digit0;

    protected override string Summary => $"armor {state.TotalArmor}";
}
#endif
