using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cameli.Game
{
    /// <summary>
    /// The clear sequence over the play screen (Phase 2 spec §5): the board fades back, a camellia in the target
    /// color blooms and petals in its color fall, then the title, the stars one by one, the restart count, the star-cap
    /// reason, and the buttons. A tap during the sequence jumps to the end. The buttons only take input once everything
    /// is shown, so the skipping tap can never press one, and a moment longer after a skip, so neither can the second
    /// tap of a double tap.
    /// </summary>
    public sealed class ClearView : MonoBehaviour, IPointerClickHandler
    {
        const float VeilAlpha = 0.9f;
        const float VeilSeconds = 0.25f;
        const float BloomSeconds = 0.8f;
        const float BloomStartScale = 0.2f;
        const float BloomStartAngle = -70f;
        const float FadeSeconds = 0.2f;
        const float StarPopSeconds = 0.22f;
        const float StarGapSeconds = 0.1f;
        const float SkipGuardSeconds = 0.25f;

        Image veil;
        RectTransform bloom;
        RectTransform flower;
        PetalFall petals;
        Color petalColor;
        bool petalsDropped;
        CanvasGroup heading;
        Image[] stars;
        CanvasGroup details;
        Text detail;
        Text reason;
        CanvasGroup buttons;
        Button nextButton;
        Button gardenButton;
        Button lastButton;
        Coroutine sequence;
        int earned;
        float buttonsFrom = -1f;

        public bool IsVisible => gameObject.activeSelf;

        /// <summary>True while the sequence runs (a tap skips it).</summary>
        public bool IsAnimating => sequence != null;

        /// <summary>Builds the view as a full-size child of <paramref name="parent"/>, hidden.</summary>
        internal static ClearView Create(RectTransform parent, Action replay, Action garden, Action next)
        {
            RectTransform root = Widgets.Layer(parent, "Clear View");
            var view = root.gameObject.AddComponent<ClearView>();
            view.Build(root, replay, garden, next);
            root.gameObject.SetActive(false);
            return view;
        }

        void Build(RectTransform root, Action replay, Action garden, Action next)
        {
            veil = Ui.NewImage("Veil", root, null, Theme.Background);
            veil.raycastTarget = true; // takes the taps that skip, and keeps them off the board
            Ui.Stretch(veil.rectTransform, -Widgets.Bleed, -Widgets.Bleed, -Widgets.Bleed, -Widgets.Bleed);

            bloom = Ui.Box(Ui.NewRect("Bloom", root), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(470f, 470f));
            bloom.pivot = new Vector2(0.5f, 0.5f);
            flower = Ui.Stretch(Blossoms.NewFlower(bloom, Color.white));
            petals = PetalFall.Create(root); // over the flower, under the text, stars, and buttons

            RectTransform headingRect = Ui.Box(Ui.NewRect("Heading", root), new Vector2(0.5f, 0.46f), Vector2.zero, new Vector2(1000f, 110f));
            heading = headingRect.gameObject.AddComponent<CanvasGroup>();
            Ui.Stretch(Widgets.Label(headingRect, "Title", "동백꽃이 피었어요", 70, Theme.Ink).rectTransform);

            RectTransform starRow = Ui.Box(Ui.NewRect("Stars", root), new Vector2(0.5f, 0.375f), Vector2.zero, new Vector2(600f, 150f));
            stars = Widgets.Stars(starRow, 3, 140f, 30f);

            RectTransform detailRect = Ui.Box(Ui.NewRect("Details", root), new Vector2(0.5f, 0.27f), Vector2.zero, new Vector2(1000f, 140f));
            details = detailRect.gameObject.AddComponent<CanvasGroup>();
            detail = Widgets.Label(detailRect, "Restarts", "", 44, Theme.Muted);
            Ui.TopStrip(detail.rectTransform, 0f, 70f);
            reason = Widgets.Label(detailRect, "Cap Reason", "", 38, Theme.Primary);
            Ui.TopStrip(reason.rectTransform, 74f, 60f);

            RectTransform buttonRow = Ui.BottomStrip(Ui.NewRect("Buttons", root), 60f, 140f, 48f);
            buttons = buttonRow.gameObject.AddComponent<CanvasGroup>();
            Button replayButton = Widgets.Button(buttonRow, "Replay", "다시", 44, false, replay, out _, SpriteFactory.RestartIcon);
            gardenButton = Widgets.Button(buttonRow, "Garden", "정원", 44, false, garden, out _, SpriteFactory.GridIcon);
            nextButton = Widgets.Button(buttonRow, "Next", "다음", 44, true, next, out _, SpriteFactory.PlayIcon, iconAfter: true);
            lastButton = Widgets.Button(buttonRow, "To Garden", "정원으로", 44, true, garden, out _, SpriteFactory.GridIcon);
            Third(replayButton, 0);
            Third(gardenButton, 1);
            Third(nextButton, 2);
            // On the last stage: [다시] [정원으로], the garden button taking the space of the other two.
            var lastRect = lastButton.GetComponent<RectTransform>();
            lastRect.anchorMin = new Vector2(1f / 3f, 0f);
            lastRect.anchorMax = Vector2.one;
            lastRect.offsetMin = new Vector2(12f, 0f);
            lastRect.offsetMax = Vector2.zero;
        }

        /// <param name="reasonLine">Why the stars were capped, or "" when they were not.</param>
        /// <param name="hasNext">False on the last stage: [정원으로] replaces [정원] and [다음].</param>
        /// <param name="restarts">Restarts in this visit, the count that set the stars.</param>
        public void Show(Color targetColor, int stars, int restarts, string reasonLine, bool hasNext)
        {
            earned = stars;
            Blossoms.SetFlowerColor(flower, targetColor);
            petalColor = targetColor;
            detail.text = restarts == 0 ? "한 번에 깼어요" : $"다시 하기 {restarts}번";
            reason.text = reasonLine;
            gardenButton.gameObject.SetActive(hasNext);
            nextButton.gameObject.SetActive(hasNext);
            lastButton.gameObject.SetActive(!hasNext);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            StopSequence();
            petals.Stop();
            petalsDropped = false;
            sequence = StartCoroutine(Play());
        }

        public void Hide()
        {
            StopSequence();
            petals.Stop();
            buttonsFrom = -1f;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Jumps to the end state; does nothing once the sequence has finished. The buttons show at once but take
        /// taps only after <see cref="SkipGuardSeconds"/> (unscaled), so a double tap can't skip and press. Petals that
        /// haven't fallen yet fall from now.
        /// </summary>
        public void Skip()
        {
            if (sequence == null) return;
            StopSequence();
            ShowFinal();
            DropPetals();
            buttons.blocksRaycasts = false; // taps meanwhile land on the veil, where a skip does nothing
            buttonsFrom = Time.unscaledTime + SkipGuardSeconds;
        }

        void Update()
        {
            if (buttonsFrom < 0f || Time.unscaledTime < buttonsFrom) return;
            buttonsFrom = -1f;
            buttons.blocksRaycasts = true;
        }

        public void OnPointerClick(PointerEventData eventData) => Skip();

        void OnDisable() => sequence = null; // Unity stops coroutines on deactivation

        void StopSequence()
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
        }

        IEnumerator Play()
        {
            buttonsFrom = -1f;
            SetVeil(0f);
            SetBloom(0f);
            heading.alpha = 0f;
            foreach (Image star in stars) star.rectTransform.localScale = Vector3.zero;
            details.alpha = 0f;
            SetButtons(false);

            for (float t = 0f; t < VeilSeconds; t += Time.unscaledDeltaTime)
            {
                SetVeil(t / VeilSeconds);
                yield return null;
            }
            SetVeil(1f);

            Sfx.Play(SfxKind.Bloom);
            for (float t = 0f; t < BloomSeconds; t += Time.unscaledDeltaTime)
            {
                SetBloom(t / BloomSeconds);
                yield return null;
            }
            SetBloom(1f);
            DropPetals();
            Sfx.Play(SfxKind.Clear);

            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                heading.alpha = t / FadeSeconds;
                yield return null;
            }
            heading.alpha = 1f;

            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].color = i < earned ? Theme.StarOn : Theme.StarOff;
                for (float t = 0f; t < StarPopSeconds; t += Time.unscaledDeltaTime)
                {
                    stars[i].rectTransform.localScale = Vector3.one * Overshoot(t / StarPopSeconds, 3f);
                    yield return null;
                }
                stars[i].rectTransform.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(StarGapSeconds);
            }

            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                details.alpha = t / FadeSeconds;
                buttons.alpha = t / FadeSeconds;
                yield return null;
            }
            sequence = null;
            ShowFinal();
        }

        void ShowFinal()
        {
            SetVeil(1f);
            SetBloom(1f);
            heading.alpha = 1f;
            Widgets.SetStars(stars, earned);
            foreach (Image star in stars) star.rectTransform.localScale = Vector3.one;
            details.alpha = 1f;
            SetButtons(true);
        }

        /// <summary>Starts the petals once per showing: when the flower is open, or at a skip before that.</summary>
        void DropPetals()
        {
            if (petalsDropped) return;
            petalsDropped = true;
            petals.Drop(petalColor);
        }

        void SetVeil(float k)
        {
            Color color = veil.color;
            color.a = VeilAlpha * Mathf.Clamp01(k);
            veil.color = color;
        }

        /// <summary>Scale 0.2 → 1 with a small overshoot, rotation -70° → 0°.</summary>
        void SetBloom(float k)
        {
            k = Mathf.Clamp01(k);
            bloom.localScale = Vector3.one * Mathf.LerpUnclamped(BloomStartScale, 1f, Overshoot(k, 1.7f));
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            bloom.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(BloomStartAngle, 0f, ease));
        }

        void SetButtons(bool on)
        {
            buttons.alpha = on ? 1f : 0f;
            buttons.interactable = on;
            buttons.blocksRaycasts = on;
        }

        /// <summary>Ease-out-back: 0 → 1, passing 1 a little before settling.</summary>
        static float Overshoot(float t, float amount)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((amount + 1f) * t + amount);
        }

        static void Third(Button button, int index)
        {
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(index / 3f, 0f);
            rt.anchorMax = new Vector2((index + 1) / 3f, 1f);
            rt.offsetMin = new Vector2(index == 0 ? 0f : 12f, 0f);
            rt.offsetMax = new Vector2(index == 2 ? 0f : -12f, 0f);
        }
    }
}
