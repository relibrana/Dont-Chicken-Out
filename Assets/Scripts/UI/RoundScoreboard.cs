using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The between-rounds score: one row of eggs per player, filled egg = round
/// won. Fades in when a round ends and out when the next one starts; stays up
/// on the match win until the menu loads. One egg per round needed to win
/// (first-to-N from the lobby): rows with fewer eggs than N get the last egg
/// cloned (rows are HorizontalLayoutGroups) and every egg shrinks to keep the
/// row's width.
///
/// Each row shows the player's chicken as a live portrait (ChickenPortraitStudio,
/// nov 2026): the new rig in that player's colour, idling. It replaces the old
/// still image of the row, which stays in the scene but hidden.
///
/// Lives on the PlayersPoints panel of a level. Rows are in slot order (P1..P4).
/// Wired by Tools > Don't Chicken Out > Limpiar nivel.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class RoundScoreboard : MonoBehaviour
{
    [Serializable]
    public sealed class Row
    {
        [Tooltip("Shown only when that player is in the match.")]
        public GameObject root;
        public Image[]    eggs;

        [Tooltip("Old still image of the chicken. Empty = the first Image of the row that is not an egg.")]
        public Image portrait;
    }

    [SerializeField] private Row[]  rows = new Row[4];
    [SerializeField] private Sprite filledEgg;
    [SerializeField, Min(0f)] private float fade = 0.5f;

    private CanvasGroup _group;
    private Tween       _fadeTween;
    private Sprite[][]  _emptyEggs;
    private float[]     _eggSize;
    private bool        _initialized;

    private void Awake()
    {
        Init();
        _group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDestroy() => _fadeTween?.Kill();

    /// <summary>Shows everyone's rounds won (index = player slot).</summary>
    public void Show(PlayerController[] players, int roundsToWin)
    {
        Init();
        gameObject.SetActive(true);

        for (int r = 0; r < rows.Length; r++)
        {
            Row row = rows[r];
            if (row == null) continue;

            PlayerController player = players != null && r < players.Length ? players[r] : null;
            if (row.root != null) row.root.SetActive(player != null);
            if (player == null) continue;

            ShowPortrait(row, player, r);

            EnsureEggs(r, roundsToWin);
            for (int i = 0; i < row.eggs.Length; i++)
            {
                if (row.eggs[i] == null) continue;
                row.eggs[i].sprite = i < player.roundsWon ? filledEgg : _emptyEggs[r][i];
            }
        }

        ChickenPortraitStudio.SetLive(true);

        _fadeTween?.Kill();
        _fadeTween = _group.DOFade(1f, fade).SetUpdate(true);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;

        _fadeTween?.Kill();
        _fadeTween = _group.DOFade(0f, fade).SetUpdate(true)
                           .OnComplete(() =>
                           {
                               gameObject.SetActive(false);
                               ChickenPortraitStudio.SetLive(false);
                           });
    }

    /// <summary>
    /// Hides the row's old still chicken and shows a live portrait of the new
    /// rig in its place (a RawImage child filling the same rect).
    /// </summary>
    private void ShowPortrait(Row row, PlayerController player, int slot)
    {
        Image still = row.portrait != null ? row.portrait : FindStill(row);
        if (still == null) return;

        RenderTexture texture = ChickenPortraitStudio.PortraitOf(player, slot);
        if (texture == null) return;

        still.enabled = false;

        Transform existing = still.transform.Find("ChickenPortrait");
        RawImage raw = existing != null ? existing.GetComponent<RawImage>() : null;
        if (raw == null)
        {
            var go = new GameObject("ChickenPortrait", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(still.transform, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
        }
        raw.texture = texture;
    }

    private static Image FindStill(Row row)
    {
        if (row.root == null) return null;
        foreach (Image image in row.root.GetComponentsInChildren<Image>(true))
            if (row.eggs == null || Array.IndexOf(row.eggs, image) < 0) return image;
        return null;
    }

    // Also from Show(): the panel may start inactive in the scene, so Awake
    // has not run the first time it is shown.
    private void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _group     = GetComponent<CanvasGroup>();
        _emptyEggs = new Sprite[rows.Length][];
        _eggSize   = new float[rows.Length];
        for (int r = 0; r < rows.Length; r++)
        {
            Image[] eggs = rows[r]?.eggs ?? Array.Empty<Image>();
            _emptyEggs[r] = new Sprite[eggs.Length];
            for (int i = 0; i < eggs.Length; i++)
                _emptyEggs[r][i] = eggs[i] != null ? eggs[i].sprite : null;
            _eggSize[r] = eggs.Length > 0 && eggs[0] != null ? eggs[0].rectTransform.sizeDelta.x : 0f;
        }
    }

    private void EnsureEggs(int r, int count)
    {
        Row row = rows[r];
        if (row.eggs == null || row.eggs.Length == 0) return;

        if (count > row.eggs.Length)
        {
            int old = row.eggs.Length;
            Image last = row.eggs[old - 1];
            Array.Resize(ref row.eggs, count);
            Array.Resize(ref _emptyEggs[r], count);
            for (int i = old; i < count; i++)
            {
                row.eggs[i] = Instantiate(last, last.transform.parent);
                row.eggs[i].name = $"Egg{i + 1}Image";
                _emptyEggs[r][i] = _emptyEggs[r][old - 1];
            }
        }

        float rowWidth = row.eggs[0].rectTransform.parent is RectTransform parent ? parent.rect.width : 0f;
        float size = rowWidth > 0f ? Mathf.Min(_eggSize[r], rowWidth / Mathf.Max(1, count)) : _eggSize[r];

        for (int i = 0; i < row.eggs.Length; i++)
        {
            if (row.eggs[i] == null) continue;
            row.eggs[i].gameObject.SetActive(i < count);
            row.eggs[i].rectTransform.sizeDelta = new Vector2(size, size);
        }
    }
}
