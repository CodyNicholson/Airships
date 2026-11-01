using System.Collections.Generic;
using Airships.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Airships.Game
{
    // Draws a timing pattern of taps, swipes and drags, each with a ring that closes at its own speed.
    // TimingSequence does the grading, and this class feeds it pointer input and shows the feedback.
    public class TimingPrompt : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float DialSize = 210f;
        const float StartScale = 2.3f;
        const float FeedbackSeconds = 0.45f;
        const float HitFlashSeconds = 0.18f;
        const float PathThickness = 26f;

        // Scripted timing offsets for automated runs, cycling through each grade: Perfect, Good, then Miss.
        static readonly float[] AutoplayOffsets = { 0.01f, 0.08f, -1f };
        const float AutoplaySwipeLead = 0.05f;

        static Sprite ringSprite;
        static Sprite arrowSprite;

        class Dial
        {
            public RectTransform Root;
            public Image Face;
            public Image Target;
            public Image Closing;
            public Image Arrow;
            public TextMeshProUGUI Number;
            public TextMeshProUGUI Hint;
            public Image Path;
            public RectTransform EndMark;
            public float FlashLeft;
        }

        readonly List<Dial> dials = new List<Dial>();
        TimingDef def;
        RectTransform area;
        Image catcher;
        GameObject titleBacking;
        TextMeshProUGUI title;
        TimingSequence sequence;
        Color faceColor;
        float feedbackLeft;
        int autoplayCount;
        int shownStep;
        int activePointer = NoPointer;
        const int NoPointer = int.MinValue;

        public bool Done => sequence == null || sequence.Done;
        public TimingGrade Grade => sequence != null ? sequence.Grade : TimingGrade.Miss;
        public TimingSequence Sequence => sequence;
        public int PerfectCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }

        public static TimingPrompt Create(RectTransform safe, TimingDef def)
        {
            var catcher = UIKit.Solid("Timing Catcher", safe, Color.clear, false);
            UIKit.Fill(catcher.rectTransform);
            var prompt = catcher.gameObject.AddComponent<TimingPrompt>();
            prompt.def = def;
            prompt.catcher = catcher;
            prompt.area = catcher.rectTransform;

            var backing = UIKit.Panel("Prompt Title Backing", catcher.transform, Palette.WithAlpha(Palette.Panel, 0.92f));
            UIKit.Place(backing.rectTransform, new Vector2(0.5f, 0.74f), Vector2.one * 0.5f, Vector2.zero, new Vector2(420f, 84f));
            prompt.titleBacking = backing.gameObject;
            prompt.title = UIKit.Text("Prompt Title", backing.transform, "", 52f, Palette.TextLight);
            UIKit.Fill(prompt.title.rectTransform);
            prompt.titleBacking.SetActive(false);
            return prompt;
        }

        Dial NewDial()
        {
            var dial = new Dial();

            dial.Path = UIKit.Solid("Drag Path", area, Palette.WithAlpha(Palette.TextLight, 0.55f));
            dial.Path.rectTransform.anchorMin = Vector2.zero;
            dial.Path.rectTransform.anchorMax = Vector2.zero;
            dial.Path.rectTransform.pivot = new Vector2(0f, 0.5f);
            dial.EndMark = UIKit.Node("Drag End", area);
            dial.EndMark.anchorMin = Vector2.zero;
            dial.EndMark.anchorMax = Vector2.zero;
            dial.EndMark.sizeDelta = Vector2.one * DialSize * 0.6f;
            NewCircle("Ring", dial.EndMark, Ring()).color = Palette.TextLight;

            dial.Root = UIKit.Node("Dial", area);
            dial.Root.anchorMin = Vector2.zero;
            dial.Root.anchorMax = Vector2.zero;
            dial.Root.sizeDelta = Vector2.one * DialSize;
            dial.Face = NewCircle("Face", dial.Root, UIKit.Circle);
            dial.Target = NewCircle("Target", dial.Root, Ring());
            dial.Closing = NewCircle("Closing Ring", dial.Root, Ring());

            dial.Arrow = NewCircle("Swipe Arrow", dial.Root, Arrow());
            dial.Arrow.color = Palette.TextLight;
            dial.Arrow.rectTransform.anchorMin = Vector2.one * 0.5f;
            dial.Arrow.rectTransform.anchorMax = Vector2.one * 0.5f;
            dial.Arrow.rectTransform.sizeDelta = Vector2.one * DialSize * 0.42f;

            dial.Number = UIKit.Text("Number", dial.Root, "", 44f, Palette.TextLight);
            dial.Number.outlineWidth = 0.25f;
            dial.Number.outlineColor = Palette.Ink;
            UIKit.Fill(dial.Number.rectTransform);

            dial.Hint = UIKit.Text("Hint", dial.Root, "", 26f, Palette.TextLight);
            dial.Hint.outlineWidth = 0.25f;
            dial.Hint.outlineColor = Palette.Ink;
            UIKit.Place(dial.Hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(DialSize, 36f));
            return dial;
        }

        static Image NewCircle(string name, RectTransform parent, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            UIKit.Fill(image.rectTransform);
            return image;
        }

        static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI Ring", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    var alpha = Mathf.Clamp01(center - distance) * Mathf.Clamp01(distance - (center - 14f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f);
            return ringSprite;
        }

        // A chevron pointing up, rotated to show a swipe's direction.
        static Sprite Arrow()
        {
            if (arrowSprite != null) return arrowSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI Arrow", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = Mathf.Abs(x - (size - 1) * 0.5f) / (size * 0.5f);
                    var v = y / (size - 1f);

                    // Two sloped bars that meet at the top center.
                    var line = 0.9f - u * 0.65f;
                    var inside = v <= line && v >= line - 0.32f && u < 0.92f;
                    pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f);
            return arrowSprite;
        }

        public void Begin(TimingPattern pattern, string text, Color color)
        {
            sequence = new TimingSequence(pattern, def, area.rect.width, area.rect.height);
            feedbackLeft = 0f;
            shownStep = 0;
            activePointer = NoPointer;
            faceColor = Palette.WithAlpha(color, 0.6f);
            title.text = text;
            title.color = Palette.TextLight;
            titleBacking.SetActive(true);
            catcher.raycastTarget = true;

            while (dials.Count < pattern.beats.Length) dials.Add(NewDial());
            for (var i = 0; i < dials.Count; i++)
            {
                HideDial(dials[i]);
                dials[i].FlashLeft = 0f;
                if (i < pattern.beats.Length) SetUpDial(dials[i], pattern.beats[i], i);
            }

            // Earlier steps draw on top of later ones when they share a spot.
            for (var i = pattern.beats.Length - 1; i >= 0; i--)
            {
                dials[i].Path.rectTransform.SetAsLastSibling();
                dials[i].EndMark.SetAsLastSibling();
                dials[i].Root.SetAsLastSibling();
            }
        }

        void SetUpDial(Dial dial, TimingBeat beat, int index)
        {
            var at = LocalPoint(beat.x, beat.y);
            dial.Root.anchoredPosition = at;
            dial.Number.text = (index + 1).ToString();
            dial.Number.rectTransform.anchorMin = beat.IsSwipe ? new Vector2(0.5f, 1f) : Vector2.zero;
            dial.Number.rectTransform.anchorMax = beat.IsSwipe ? new Vector2(0.5f, 1f) : Vector2.one;
            if (beat.IsSwipe)
            {
                dial.Number.rectTransform.sizeDelta = new Vector2(70f, 50f);
                dial.Number.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            }
            else
            {
                dial.Number.rectTransform.offsetMin = Vector2.zero;
                dial.Number.rectTransform.offsetMax = Vector2.zero;
            }
            dial.Hint.text = beat.IsSwipe ? "SWIPE" : beat.IsDrag ? "HOLD" : "TAP";
            dial.Face.color = beat.IsSwipe ? Palette.WithAlpha(Palette.Storm, 0.7f) : beat.IsDrag ? Palette.WithAlpha(Palette.Heal, 0.7f) : faceColor;
            dial.Target.color = Palette.TextLight;
            dial.Closing.gameObject.SetActive(true);
            dial.Closing.color = Palette.Accent;
            dial.Closing.rectTransform.localScale = Vector3.one * StartScale;

            dial.Arrow.gameObject.SetActive(beat.IsSwipe);
            if (beat.IsSwipe)
            {
                var degrees = beat.dir == "right" ? -90f : beat.dir == "down" ? 180f : beat.dir == "left" ? 90f : 0f;
                dial.Arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
                dial.Arrow.rectTransform.anchoredPosition = Vector2.zero;
            }

            if (!beat.IsDrag) return;
            var end = LocalPoint(beat.endX, beat.endY);
            var along = end - at;
            dial.Path.rectTransform.anchoredPosition = at;
            dial.Path.rectTransform.sizeDelta = new Vector2(along.magnitude, PathThickness);
            dial.Path.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            dial.EndMark.anchoredPosition = end;
        }

        Vector2 LocalPoint(float x, float y) => new Vector2(x * area.rect.width, y * area.rect.height);

        static void HideDial(Dial dial)
        {
            dial.Root.gameObject.SetActive(false);
            dial.Path.gameObject.SetActive(false);
            dial.EndMark.gameObject.SetActive(false);
        }

        void Update()
        {
            if (sequence == null) return;

            for (var i = 0; i < sequence.Pattern.beats.Length; i++)
            {
                var dial = dials[i];
                if (dial.FlashLeft <= 0f) continue;
                dial.FlashLeft -= Time.unscaledDeltaTime;
                if (dial.FlashLeft <= 0f) HideDial(dial);
            }

            if (sequence.Done)
            {
                if (feedbackLeft > 0f)
                {
                    feedbackLeft -= Time.unscaledDeltaTime;
                    if (feedbackLeft <= 0f) titleBacking.SetActive(false);
                }
                return;
            }

            if (GameDirector.Autoplay) Autoplay();
            else ReadKeyboard();
            sequence.Advance(Time.deltaTime);
            RefreshAfterInput();
            if (!sequence.Done) UpdateDials();
        }

        // Space stands in for a tap at the target, as a development aid.
        void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame) return;
            var beat = sequence.Pattern.beats[sequence.NextBeat];
            var at = LocalPoint(beat.x, beat.y);
            sequence.PointerDown(at.x, at.y);
        }

        void Autoplay()
        {
            var offset = AutoplayOffsets[autoplayCount % AutoplayOffsets.Length];
            if (offset < 0f) return;
            var beat = sequence.Pattern.beats[sequence.NextBeat];
            var at = LocalPoint(beat.x, beat.y);
            if (sequence.IsDragging)
            {
                sequence.CurrentMarker(out var markerX, out var markerY);
                sequence.PointerMove(markerX, markerY);
            }
            else if (sequence.IsSwipeArmed)
            {
                if (sequence.Elapsed >= beat.at - offset) sequence.PointerMove(at.x + beat.DirX * def.swipeDistance * 1.5f, at.y + beat.DirY * def.swipeDistance * 1.5f);
            }
            else if (sequence.Elapsed >= beat.at - offset - (beat.IsSwipe ? AutoplaySwipeLead : 0f))
            {
                sequence.PointerDown(at.x, at.y);
            }
        }

        void UpdateDials()
        {
            var beats = sequence.Pattern.beats;
            for (var i = sequence.NextBeat; i < beats.Length; i++)
            {
                var beat = beats[i];
                var left = beat.at - sequence.Elapsed;
                var visible = left <= beat.ring;
                var dial = dials[i];
                dial.Root.gameObject.SetActive(visible);
                dial.Path.gameObject.SetActive(visible && beat.IsDrag);
                dial.EndMark.gameObject.SetActive(visible && beat.IsDrag);
                if (!visible) continue;

                dial.Closing.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, StartScale, Mathf.Clamp01(left / beat.ring));

                if (i == sequence.NextBeat && sequence.IsDragging)
                {
                    sequence.CurrentMarker(out var x, out var y);
                    dial.Root.anchoredPosition = new Vector2(x, y);
                    dial.Closing.gameObject.SetActive(false);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (sequence == null || sequence.Done) return;
            activePointer = eventData.pointerId;
            var at = ToLocal(eventData.position);
            sequence.PointerDown(at.x, at.y);
            RefreshAfterInput();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (sequence == null || sequence.Done || eventData.pointerId != activePointer) return;
            var at = ToLocal(eventData.position);
            sequence.PointerMove(at.x, at.y);
            RefreshAfterInput();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (sequence == null || sequence.Done || eventData.pointerId != activePointer) return;
            activePointer = NoPointer;
            sequence.PointerUp();
            RefreshAfterInput();
        }

        Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            return local - area.rect.min;
        }

        // Converts a point in the prompt's units to screen pixels, for scripted input.
        public Vector2 ToScreen(float x, float y) => RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(new Vector3(x, y, 0f) + (Vector3)area.rect.min));

        public float AreaWidth => area.rect.width;
        public float AreaHeight => area.rect.height;

        void RefreshAfterInput()
        {
            while (shownStep < sequence.NextBeat)
            {
                var dial = dials[shownStep];
                dial.Closing.gameObject.SetActive(false);
                dial.Path.gameObject.SetActive(false);
                dial.EndMark.gameObject.SetActive(false);
                dial.Face.color = sequence.LastStepGrade == TimingGrade.Perfect ? Palette.Accent : Palette.Heal;
                dial.FlashLeft = HitFlashSeconds;
                AudioBank.Play(Sfx.Click, 1f, 0f);
                shownStep++;
            }
            if (sequence.Done && feedbackLeft <= 0f) ShowResult();
        }

        void ShowResult()
        {
            autoplayCount++;
            catcher.raycastTarget = false;
            feedbackLeft = FeedbackSeconds;
            foreach (var dial in dials)
            {
                if (dial.FlashLeft <= 0f) HideDial(dial);
            }

            switch (sequence.Grade)
            {
                case TimingGrade.Perfect:
                    PerfectCount++;
                    title.text = "PERFECT!";
                    title.color = Palette.Accent;
                    AudioBank.Play(Sfx.Sting, 0.6f, 0f);
                    break;
                case TimingGrade.Good:
                    GoodCount++;
                    title.text = "GOOD";
                    title.color = Palette.Heal;
                    AudioBank.Play(Sfx.Confirm, 0.8f);
                    break;
                default:
                    MissCount++;
                    title.text = "MISS";
                    title.color = Palette.Danger;
                    AudioBank.Play(Sfx.Denied, 0.5f);
                    break;
            }
        }
    }
}
