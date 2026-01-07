using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class LoadingScreenController : MonoBehaviour
{
    public readonly struct Payload
    {
        public readonly string Title;
        public readonly string Description;
        public readonly Sprite Image;
        public readonly VideoClip Video;

        public Payload(string title, string description, Sprite image, VideoClip video)
        {
            Title = title;
            Description = description;
            Image = image;
            Video = video;
        }
    }

    [Header("Canvas")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text (TMP)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Image Preview (optional)")]
    [SerializeField] private GameObject imageRoot;
    [SerializeField] private Image previewImage;

    [Header("Video Preview (RenderTexture)")]
    [SerializeField] private GameObject videoRoot;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoOutput;

    [Header("Fade")]
    [Min(0f)]
    [SerializeField] private float fadeDuration = 0.25f;

    public float FadeDuration => fadeDuration;

    private Coroutine fadeRoutine;

    public bool IsFading { get; private set; }

    private void Awake()
    {
        SetVisibleImmediate(false);
        StopVideo();
    }

    public void Show(in Payload payload)
    {
        ApplyPayload(payload);
        FadeTo(1f, true);
    }

    public void Hide()
    {
        FadeTo(0f, false);
    }

    public void FadeOnly()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        StopVideo();
        FadeTo(1f, true);
    }

    private void ApplyPayload(in Payload payload)
    {
        if (titleText != null) titleText.text = payload.Title;
        if (descriptionText != null) descriptionText.text = payload.Description;

        bool hasVideo = payload.Video != null && videoPlayer != null && videoOutput != null;
        bool hasImage = payload.Image != null && previewImage != null;

        if (videoRoot != null) videoRoot.SetActive(hasVideo);
        if (imageRoot != null) imageRoot.SetActive(!hasVideo && hasImage);

        if (hasVideo)
        {
            previewImage.enabled = false;

            videoPlayer.Stop();
            videoPlayer.clip = payload.Video;
            videoPlayer.isLooping = true;
            videoPlayer.Play();
        }
        else
        {
            StopVideo();

            if (hasImage)
            {
                previewImage.sprite = payload.Image;
                previewImage.enabled = true;
            }
            else
            {
                if (previewImage != null)
                {
                    previewImage.sprite = null;
                    previewImage.enabled = false;
                }
            }
        }
    }

    private void StopVideo()
    {
        if (videoPlayer == null) return;

        if (videoPlayer.isPlaying) videoPlayer.Stop();
        videoPlayer.clip = null;
    }

    private void FadeTo(float targetAlpha, bool blockInput)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, blockInput));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool blockInput)
    {
        IsFading = true;

        if (canvasGroup == null)
        {
            fadeRoutine = null;
            yield break;
        }

        float startAlpha = canvasGroup.alpha;

        canvasGroup.blocksRaycasts = blockInput;
        canvasGroup.interactable = blockInput;

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;

            if (targetAlpha <= 0.001f)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                StopVideo();
            }

            fadeRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, k);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0.001f)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            StopVideo();
        }

        fadeRoutine = null;

        IsFading = false;
    }

    private void SetVisibleImmediate(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }
}
