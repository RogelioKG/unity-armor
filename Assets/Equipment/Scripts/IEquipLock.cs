/// <summary>
/// Implemented by anything that has to freeze what the character is wearing while it runs —
/// a swing, a dodge, a cast. EquipmentState polls these rather than them setting a flag on it,
/// so two components cannot unlock each other's window. A refused change is dropped, not queued.
/// </summary>
public interface IEquipLock
{
    bool BlocksEquip { get; }
}
