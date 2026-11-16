using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One step of the pre-match flow (mode → map → rules → hen house). The
/// screens only say what the player chose; <see cref="LobbyFlow"/> decides
/// where that leads.
/// </summary>
public abstract class LobbyScreen : MonoBehaviour
{
    [Tooltip("Focused when the screen opens. Gamepad/keyboard navigation starts here.")]
    [SerializeField] protected GameObject firstSelected;

    /// <summary>False for screens that read each player's own input (the hen house).</summary>
    public virtual bool UsesMenuCancel => true;

    private GameObject _lastSelected;

    public virtual void Enter()
    {
        gameObject.SetActive(true);
        StartCoroutine(SelectNextFrame(_lastSelected != null ? _lastSelected : firstSelected));
    }

    public virtual void Exit()
    {
        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (current != null && current.transform.IsChildOf(transform))
            _lastSelected = current;

        gameObject.SetActive(false);
    }

    /// <summary>Menu "Cancel" while this screen is open.</summary>
    public virtual void OnCancel() { }

    /// <summary>Called by LobbyFlow every frame the screen is open with no focus, so a mouse click on empty space does not strand the gamepad.</summary>
    public void EnsureSelection()
    {
        EventSystem es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject != null) return;
        GameObject target = _lastSelected != null ? _lastSelected : firstSelected;
        if (target != null && target.activeInHierarchy) es.SetSelectedGameObject(target);
    }

    private static IEnumerator SelectNextFrame(GameObject target)
    {
        // One frame so layout and the screen's own OnEnable run first.
        yield return null;
        EventSystem es = EventSystem.current;
        if (es == null) yield break;
        es.SetSelectedGameObject(null);
        if (target != null && target.activeInHierarchy) es.SetSelectedGameObject(target);
    }

    protected static void PlayUISound(string id)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(id);
    }
}
