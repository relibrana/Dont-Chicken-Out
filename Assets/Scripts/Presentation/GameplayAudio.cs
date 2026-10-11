using UnityEngine;

/// <summary>
/// Capa de presentación del audio de gameplay: traduce <see cref="SimEvents"/> a sonidos del
/// <see cref="AudioManager"/>. Aquí es donde, con netcode, entra el "solo suena lo que ve mi cámara".
///
/// La UI y los menús siguen llamando al AudioManager directo: no se re-simulan.
/// </summary>
public static class GameplayAudio
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        SimEvents.Raised -= OnSimEvent;
        SimEvents.Raised += OnSimEvent;
    }

    private static void OnSimEvent(SimEventData e)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;

        // Every gameplay sound passes where it happens: AudioManager turns it
        // into the global ScreenPan that pans the Player / Items / Blocks buses
        // (Audio, oct 2026). Only the POW, a screen-wide effect, stays centred.
        switch (e.Type)
        {
            case SimEvent.PlayerJumped:       audio.PlaySoundAt("player_jump", e.Position);    break;
            case SimEvent.PlayerLanded:       audio.PlaySoundAt("player_land", e.Position);    break;
            case SimEvent.PlayerKicked:       audio.PlaySoundAt("player_kick", e.Position);    break;
            // Replaces the normal kick while the power-up lasts (design, oct 2026).
            case SimEvent.PlayerSuperKicked:  audio.PlaySoundAt("super_kick", e.Position);     break;
            // One glide loop per chicken, keyed on the chicken's GameObject.
            case SimEvent.PlayerGlideStarted: audio.PlaySoundFor("player_gliding", OwnerOf(e), e.Position); break;
            case SimEvent.PlayerGlideEnded:   audio.StopSoundFor("player_gliding", OwnerOf(e)); break;
            case SimEvent.PlayerTeleported:   audio.PlaySoundAt("teleport", e.Position);       break;
            case SimEvent.PlayerJoined:       audio.PlaySoundAt("player_spawn", e.Position);   break;
            case SimEvent.PlayerDied:
                audio.PlaySoundAt("player_death", e.Position);
                // Dying mid-glide never reaches the landing that ends the loop.
                audio.StopSoundFor("player_gliding", OwnerOf(e));
                break;
            case SimEvent.BlockPlaced:        audio.PlaySoundAt("block_placement", e.Position, e.Value); break;
            case SimEvent.BlockPlaceRejected: audio.PlaySoundAt("block_invalid", e.Position);  break;
            case SimEvent.HoldablePlaced:     audio.PlaySoundAt("block_placement", e.Position); break;
            case SimEvent.CapsuleHit:         audio.PlaySoundAt("egg_break", e.Position);      break;
            case SimEvent.TireBounced:        audio.PlaySoundAt("tire_bounce", e.Position);    break;
            // Cada bomba enciende y apaga sólo su mecha.
            case SimEvent.BombFuseLit:        audio.PlaySoundFor("bomb_lighter", e.Source, e.Position); break;
            case SimEvent.BombFuseOut:        audio.StopSoundFor("bomb_lighter", e.Source);    break;
            case SimEvent.BombExploded:       audio.PlaySoundAt("bomb_explosion", e.Position); break;
            // Same blast as the bomb (design, oct 2026). Give it its own id if
            // Audio authors a different sound for it.
            case SimEvent.HotPotatoExploded:  audio.PlaySoundAt("bomb_explosion", e.Position); break;
            // The event covers the whole 3-2-1-JUMP! sequence (Audio synced it in FMOD).
            case SimEvent.PowCountdownStarted: audio.PlaySound("pow");                         break;
            // Round ended mid-countdown: cut the sequence sound (the row is sustained for this).
            case SimEvent.PowCancelled:       audio.StopSound("pow");                          break;
            case SimEvent.AnvilFell:          audio.PlaySoundAt("anvil_fall", e.Position);     break;
            case SimEvent.AnvilHit:           audio.PlaySoundAt("anvil_hit", e.Position);      break;
        }
    }

    // Loops keyed per chicken must use the same key no matter which of its
    // components raised the event (PlayerMovement starts the glide,
    // GameManager reports the death with the PlayerController).
    private static Object OwnerOf(SimEventData e) =>
        e.Source is Component c ? c.gameObject : e.Source;
}
