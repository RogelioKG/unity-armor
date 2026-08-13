#if UNITY_EDITOR
using UnityEngine.InputSystem;

public class AppearanceTester : EquipmentTester<AppearanceState, AppearanceData, AppearanceSlot>
{
    protected override Key NextKey => Key.Digit4;
    protected override Key ClearKey => Key.Digit8;
}
#endif
