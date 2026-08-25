/// <summary>
/// Capsule reward applied to the player immediately on pickup (self-state items:
/// súper patada, doble salto, pollo metálico...). Counterpart of HoldableItem,
/// which replaces the held block instead. The pooled object is returned to the
/// pool right after Apply, so implementations must not rely on staying active.
/// </summary>
public interface IInstantItem
{
    void Apply(PlayerController player);
}
