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

        switch (e.Type)
        {
            case SimEvent.PlayerJumped:       audio.PlaySound("player_jump");                  break;
            case SimEvent.PlayerLanded:       audio.PlaySound("player_land");                  break;
            case SimEvent.PlayerKicked:       audio.PlaySound("player_kick");                  break;
            case SimEvent.PlayerJoined:       audio.MakeJoinSound();                           break;
            case SimEvent.PlayerDied:         audio.MakeDeathSound();                          break;
            case SimEvent.BlockPlaced:        audio.PlaySound("block_placement", e.Value);     break;
            case SimEvent.BlockPlaceRejected: audio.PlaySound("block_invalid");                break;
            case SimEvent.HoldablePlaced:     audio.PlaySound("block_placement");              break;
            case SimEvent.CapsuleHit:         audio.PlaySound("egg_break");                    break;
            case SimEvent.TireBounced:        audio.PlaySound("tire_bounce");                  break;
            // Cada bomba enciende y apaga sólo su mecha.
            case SimEvent.BombFuseLit:        audio.PlaySoundFor("bomb_lighter", e.Source);    break;
            case SimEvent.BombFuseOut:        audio.StopSoundFor("bomb_lighter", e.Source);    break;
            // Paneado por su posición en pantalla: una bomba que estalla a la izquierda suena por
            // el altavoz izquierdo.
            case SimEvent.BombExploded:       audio.PlaySoundAt("bomb_explosion", e.Position); break;
        }
    }
}
