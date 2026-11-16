using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A row of mutually exclusive options ("1 · 3 · 5"). The chosen one is
/// painted in the accent colour. Moving the focus onto an option chooses it
/// (so left/right on a pad changes the rule directly); clicking does too.
/// </summary>
public sealed class OptionGroup : MonoBehaviour
{
    [SerializeField] private Button[] options;
    [SerializeField] private int[]    values = { 1, 3, 5 };
    [SerializeField] private Color    chosenColor = new(1f, 0.78f, 0.2f, 1f);
    [SerializeField] private Color    idleColor   = new(0.25f, 0.21f, 0.33f, 1f);
    [SerializeField] private Color    chosenText  = new(0.12f, 0.10f, 0.18f, 1f);
    [SerializeField] private Color    idleText    = Color.white;

    public event Action<int> ValueChanged;

    private int _index;

    public int Value => values.Length > 0 ? values[Mathf.Clamp(_index, 0, values.Length - 1)] : 0;

    private void Awake()
    {
        for (int i = 0; i < options.Length; i++)
        {
            int captured = i;
            options[i].onClick.AddListener(() => Choose(captured));

            // Focus = choose, without needing a separate component per button.
            var trigger = options[i].gameObject.AddComponent<EventTrigger>();
            var onSelect = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            onSelect.callback.AddListener(_ => Choose(captured));
            trigger.triggers.Add(onSelect);
        }
        Refresh();
    }

    /// <summary>Chooses the option equal to <paramref name="value"/>, or the closest one. Silent.</summary>
    public void SetValue(int value)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (Mathf.Abs(values[i] - value) < Mathf.Abs(values[best] - value)) best = i;
        _index = best;
        Refresh();
    }

    /// <summary>The button of the chosen option (to put the focus back on it).</summary>
    public GameObject ChosenButton => options.Length > 0 ? options[Mathf.Clamp(_index, 0, options.Length - 1)].gameObject : null;

    private void Choose(int i)
    {
        if (i == _index) return;
        _index = i;
        Refresh();
        // No sound here: the buttons' UIButtonSFX already plays hover/press.
        ValueChanged?.Invoke(Value);
    }

    private void Refresh()
    {
        for (int i = 0; i < options.Length; i++)
        {
            bool chosen = i == _index;
            if (options[i].targetGraphic != null) options[i].targetGraphic.color = chosen ? chosenColor : idleColor;
            TextMeshProUGUI label = options[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = chosen ? chosenText : idleText;
        }
    }
}
