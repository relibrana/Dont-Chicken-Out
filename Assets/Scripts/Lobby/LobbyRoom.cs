using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One room of the hen house. Empty: closed door with the sleeping sheep and
/// a "press to join" hint. Taken: door gone, the chicken in its colour with a
/// P-badge, the locked customize option and the ready hint. Ready: stamp.
///
/// Pure view: <see cref="CoopLobby"/> owns the seats and calls in here.
/// </summary>
public sealed class LobbyRoom : MonoBehaviour
{
    [Header("World")]
    [Tooltip("Closed door covering the room while nobody is in it.")]
    [SerializeField] private SpriteRenderer door;
    [Tooltip("Where the chicken stands (feet).")]
    [SerializeField] private Transform chickenAnchor;

    [Header("UI")]
    [SerializeField] private Image           badge;
    [SerializeField] private TextMeshProUGUI badgeLabel;
    [SerializeField] private TextMeshProUGUI hintLabel;
    [Tooltip("Shown/hidden with the hint (label + its backdrop). Empty = the label itself.")]
    [SerializeField] private GameObject      hintRoot;
    [SerializeField] private GameObject      readyStamp;
    [SerializeField] private Image           readyStampImage;
    [Tooltip("\"Personalizar\" with a lock: visible, not usable yet.")]
    [SerializeField] private GameObject      customizeLocked;
    [SerializeField] private GameObject      hostTag;

    private GameObject _chicken;
    private Tween      _doorTween;

    public Transform ChickenAnchor => chickenAnchor;

    // ── States ────────────────────────────────────────────────────────────────

    public void ShowEmpty(string joinHint)
    {
        ClearChicken();
        SetDoor(closed: true);

        SetActive(badge, false);
        SetActive(readyStamp, false);
        SetActive(customizeLocked, false);
        SetActive(hostTag, false);
        SetHint(joinHint);
    }

    public void ShowTaken(int slot, Color color, bool isHost, string hint, GameObject chickenVisual)
    {
        SetDoor(closed: false);

        if (badge != null)
        {
            badge.gameObject.SetActive(true);
            badge.color = color;
            badge.transform.DOKill(complete: true);
            badge.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.5f);
        }
        if (badgeLabel != null) badgeLabel.text = $"P{slot + 1}";

        SetActive(customizeLocked, true);
        SetActive(hostTag, isHost);
        SetActive(readyStamp, false);
        SetHint(hint);

        if (chickenVisual != null && chickenVisual != _chicken)
        {
            ClearChicken();
            _chicken = chickenVisual;
            Transform t = _chicken.transform;
            Vector3 targetScale = t.localScale;
            t.localScale = Vector3.zero;
            t.DOScale(targetScale, 0.35f).SetEase(Ease.OutBack);
        }
    }

    public void SetReady(bool ready, Color color, string hint)
    {
        if (readyStamp != null)
        {
            readyStamp.SetActive(ready);
            if (ready)
            {
                if (readyStampImage != null) readyStampImage.color = color;
                readyStamp.transform.DOKill(complete: true);
                readyStamp.transform.localScale = Vector3.one * 1.6f;
                readyStamp.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
            }
        }

        if (ready && _chicken != null)
        {
            _chicken.transform.DOKill(complete: true);
            _chicken.transform.DOPunchPosition(Vector3.up * 0.35f, 0.3f, 4, 0.5f);
        }

        SetHint(hint);
    }

    public void SetHost(bool isHost) => SetActive(hostTag, isHost);

    public void SetHint(string text)
    {
        if (hintLabel == null) return;
        hintLabel.text = text;
        (hintRoot != null ? hintRoot : hintLabel.gameObject).SetActive(!string.IsNullOrEmpty(text));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetDoor(bool closed)
    {
        if (door == null) return;

        _doorTween?.Kill();
        door.gameObject.SetActive(true);
        _doorTween = door.DOFade(closed ? 1f : 0f, 0.25f)
                         .OnComplete(() => door.gameObject.SetActive(closed));
    }

    private void ClearChicken()
    {
        if (_chicken == null) return;
        _chicken.transform.DOKill();
        Destroy(_chicken);
        _chicken = null;
    }

    private static void SetActive(Component c, bool on)
    {
        if (c != null) c.gameObject.SetActive(on);
    }

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }

    private void OnDestroy()
    {
        _doorTween?.Kill();
        if (badge != null)      badge.transform.DOKill();
        if (readyStamp != null) readyStamp.transform.DOKill();
    }
}
