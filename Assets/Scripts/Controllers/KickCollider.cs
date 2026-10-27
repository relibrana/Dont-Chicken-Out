using UnityEngine;

public class KickCollider : MonoBehaviour
{
    public Vector2 forceDirection;
    public PlayerController playerController;

    private void OnTriggerEnter2D(Collider2D other)
    {
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
            damageable.TakeDamage(superActive ? SuperKickState.BlockDamage : 1, playerController);
        }
    }
}
