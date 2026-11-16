using UnityEngine;

/// <summary>
/// Catches the chicken's animation events in the lobby. The clips call
/// PlayerAnimController (kick collider timing, step sounds), which is
/// stripped from the lobby copy; without a receiver Unity logs an error
/// every loop. The lobby chickens only idle, so the events do nothing here.
/// </summary>
public sealed class LobbyAnimEventSink : MonoBehaviour
{
    public void PlayAnimEvent(int id) { }
    public void playAnimEvent(int id) { } // a few clips use this spelling
    public void PlaySFX(string sfxName) { }
    public void PlayBGM(string bgmName) { }
}
