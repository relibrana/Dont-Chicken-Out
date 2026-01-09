using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerAnimController : MonoBehaviour
{
    public List<UnityEvent> animationEvents = new List<UnityEvent>();

    public void playAnimEvent(int id)
    {
        animationEvents[id].Invoke();
    }

    public void PlaySFX(string sfxName)
    {
        AudioManager.Instance.PlaySound(sfxName);
    }
}
