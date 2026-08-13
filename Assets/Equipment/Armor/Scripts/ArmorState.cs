using System.Linq;

public class ArmorState : EquipmentState<ArmorData, ArmorSlot>
{
    /// <summary>Armor rating of everything currently worn.</summary>
    public int TotalArmor => Worn.Values.Sum(piece => piece.armor);
}
