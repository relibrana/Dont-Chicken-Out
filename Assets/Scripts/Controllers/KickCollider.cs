using UnityEngine;

public class KickCollider : MonoBehaviour
{
    public Vector2 forceDirection;
    public PlayerController playerController;

    // A kick damages a piece once, however many of its blocks it touches (GDD).
    // The collider is switched on per kick by the animation, so this resets then.
    private readonly System.Collections.Generic.HashSet<Object> _damagedThisKick = new();

    private void OnEnable() => _damagedThisKick.Clear();

    private void OnTriggerEnter2D(Collider2D other)
    {
        // A mucous piece catches whoever kicks it and absorbs that kick: no
        // damage, no push (GDD). Once per kick.
        if (other.attachedRigidbody != null
            && other.attachedRigidbody.TryGetComponent(out MocoPiece moco) && moco.IsActive)
        {
            if (_damagedThisKick.Add(moco)) moco.Catch(playerController);
            return;
        }

        SuperKickState superKick = null;
        bool superActive = playerController != null
            && playerController.TryGetComponent(out superKick)
            && superKick.IsActive;

        MetalChickenState metal = null;
        bool metalActive = playerController != null
            && playerController.TryGetComponent(out metal)
            && metal.IsActive;

        if(other.attachedRigidbody && other.attachedRigidbody.TryGetComponent(out IKickable kickable))
        {
            PlayerController victim = kickable as PlayerController;
            bool validVictim = victim != null && victim != playerController && victim.isOnGame;

            // Stuck in moco: another chicken’s kick frees them without moving them (GDD).
            if (validVictim && victim.TryGetComponent(out MocoStuckState stuck) && stuck.IsActive)
            {
                stuck.Release();
                return;
            }

            if (superActive && validVictim)
            {
                victim.OnDeath();
            }
            else
            {
                Vector2 impulseDirection = forceDirection;
                impulseDirection.x *= transform.lossyScale.x;

                if (superActive)
                    impulseDirection *= superKick.ImpulseMultiplier;

                if (metalActive)
                    impulseDirection *= metal.KickMultiplier;

                kickable.ReceiveKick(impulseDirection);

                // La papa caliente se pasa pateando, no por contacto de colliders
                // (diseño, sep 2026). Se resuelve aquí y no dentro de ReceiveKick
                // porque el pollo metálico ignora el empuje pero sí debe recibirla.
                if (validVictim
                    && playerController != null
                    && playerController.TryGetComponent(out HotPotatoState potato))
                {
                    potato.TryTransferByKick(victim);
                }
            }
        }
        if(other.TryGetComponent(out IDamageable damageable))
        {
            Object target = damageable is BlockDamageable block && block.Piece != null
                ? block.Piece
                : damageable as Object;
            if (target != null && !_damagedThisKick.Add(target)) return;

            damageable.TakeDamage(superActive ? SuperKickState.BlockDamage : 1, playerController);
        }
    }
}
