using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Smash-style map grid built from <see cref="MapCatalogSO"/>: one tile per
/// real map, then locked "?" tiles. The tile template is a disabled child in
/// the scene so Arte can restyle it without touching code.
/// </summary>
public sealed class MapSelectScreen : LobbyScreen
{
    [SerializeField] private MapCatalogSO catalog;
    [SerializeField] private MapCard      cardTemplate;
    [SerializeField] private Transform    grid;

    [Tooltip("Big label with the focused map's name.")]
    [SerializeField] private TextMeshProUGUI focusedName;
    [SerializeField] private string lockedName = "???  ·  Próximamente";

    public event Action<MapDefinitionSO> MapChosen;
    public event Action                  BackRequested;

    private readonly List<MapCard> _cards = new();

    private void Awake()
    {
        cardTemplate.gameObject.SetActive(false);

        if (catalog != null)
        {
            foreach (MapDefinitionSO map in catalog.Maps)
                if (map != null) AddCard(map);
            for (int i = 0; i < catalog.ComingSoonSlots; i++)
                AddCard(null);
        }

        if (_cards.Count > 0) firstSelected = _cards[0].gameObject;
    }

    public override void Enter()
    {
        // Come back focused on the map that is already picked.
        foreach (MapCard card in _cards)
            if (card.Map != null && card.Map == MatchSetup.Map) { firstSelected = card.gameObject; break; }

        base.Enter();
    }

    private void AddCard(MapDefinitionSO map)
    {
        MapCard card = Instantiate(cardTemplate, grid);
        card.name = map != null ? $"Map_{map.Id}" : "Map_Locked";
        card.gameObject.SetActive(true);
        card.Bind(map);
        card.Focused += OnFocused;
        card.Clicked += OnClicked;
        _cards.Add(card);
    }

    private void OnFocused(MapCard card)
    {
        // Hover/press sounds come from the card's UIButtonSFX.
        if (focusedName != null) focusedName.text = card.Locked ? lockedName : card.Map.DisplayName;
    }

    private void OnClicked(MapCard card)
    {
        if (card.Locked) { LockedFeedback.Play(card.transform); return; }
        MapChosen?.Invoke(card.Map);
    }

    public override void OnCancel() => BackRequested?.Invoke();
}
