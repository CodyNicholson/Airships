using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Airships.Game
{
    // Builds all game UI from code in the art bible's palette. Layout is authored against a
    // 1080-unit-high canvas with the height fixed, so wide phones simply gain horizontal room.
    public static class UIKit
    {
        public const float ReferenceHeight = 1080f;

        static Sprite rounded;
        static Sprite white;
        static Sprite circle;

        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(64, 18);
        public static Sprite White => white != null ? white : white = MakeSolid();
        public static Sprite Circle => circle != null ? circle : circle = MakeCircle(128);

        public static Canvas CreateCanvas(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("Event System");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // A full-stretch container kept inside the phone's safe area (camera cutouts, gesture bars).
        public static RectTransform SafeArea(Transform canvas, string name = "Safe Area")
        {
            var rect = Stretch(name, canvas);
            rect.gameObject.AddComponent<SafeAreaFitter>();
            return rect;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rect = Node(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = false)
        {
            var rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image Solid(string name, Transform parent, Color color, bool raycast = false)
        {
            var rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = White;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool bold = true)
        {
            var rect = Node(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.characterSpacing = 3f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        // A kit icon. It stays blank if the kit has no such icon, so a missing one can't break a screen.
        public static Image Icon(string name, Transform parent, string icon)
        {
            var rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = KitArt.Icon(icon);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = image.sprite != null;
            return image;
        }

        public static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static GameButton Button(string name, Transform parent, string label, float fontSize, Color? color = null, Action onClick = null)
        {
            var image = Panel(name, parent, color ?? Palette.PanelLight, true);
            var text = Text("Label", image.transform, label, fontSize, Palette.TextLight);
            Fill(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 4f);
            text.rectTransform.offsetMax = new Vector2(-16f, -4f);

            var button = image.gameObject.AddComponent<GameButton>();
            button.Setup(image, text, color ?? Palette.PanelLight, onClick);
            return button;
        }

        public static Bar Bar(string name, Transform parent, Color color)
        {
            var back = Panel(name, parent, Palette.WithAlpha(Palette.Ink, 0.85f));
            var fill = Solid("Fill", back.transform, color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            Fill(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(4f, 4f);
            fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
            var bar = back.gameObject.AddComponent<Bar>();
            bar.Fill = fill;
            return bar;
        }

        static Sprite MakeSolid()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "UI White" };
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI Rounded", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    var dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    var alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * (radius + 2));
        }

        static Sprite MakeCircle(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI Circle", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    var alpha = Mathf.Clamp01(center - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        }
    }

    public class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        RectTransform rect;

        void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != applied) Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rect.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rect.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
        }
    }

    public class Bar : MonoBehaviour
    {
        public Image Fill;
        Coroutine tween;
        float goal = -1f;

        public void Set(float value, bool instant = false)
        {
            value = Mathf.Clamp01(value);
            if (instant || !isActiveAndEnabled)
            {
                if (tween != null) StopCoroutine(tween);
                goal = value;
                Fill.fillAmount = value;
                return;
            }

            // HUDs call this every frame. Restarting for the same value would pin the bar where it is.
            if (Mathf.Approximately(value, goal)) return;
            goal = value;
            if (tween != null) StopCoroutine(tween);
            var start = Fill.fillAmount;
            tween = StartCoroutine(Ease.Tween(0.3f, t => Fill.fillAmount = Mathf.Lerp(start, value, t)));
        }
    }

    public class GameButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        Image image;
        Color color;
        Action click;
        Coroutine pop;
        bool interactable = true;

        public TextMeshProUGUI Label { get; private set; }
        public Sfx ClickSound = Sfx.Click;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                image.color = value ? color : Color.Lerp(color, Palette.Ink, 0.55f);
                Label.color = value ? Palette.TextLight : Palette.TextMuted;
            }
        }

        public void Setup(Image background, TextMeshProUGUI label, Color baseColor, Action onClick)
        {
            image = background;
            Label = label;
            color = baseColor;
            click = onClick;
        }

        public void SetAction(Action onClick) => click = onClick;

        // Runs the click from code, used by the automated playthrough.
        public void Press()
        {
            if (interactable) click?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (interactable) AnimateScale(0.94f);
        }

        public void OnPointerUp(PointerEventData eventData) => AnimateScale(1f);
        public void OnPointerExit(PointerEventData eventData) => AnimateScale(1f);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable)
            {
                AudioBank.Play(Sfx.Denied, 0.7f);
                return;
            }
            AudioBank.Play(ClickSound);
            click?.Invoke();
        }

        void AnimateScale(float target)
        {
            if (!isActiveAndEnabled) return;
            if (pop != null) StopCoroutine(pop);
            var start = transform.localScale.x;
            pop = StartCoroutine(Ease.Tween(0.08f, t => transform.localScale = Vector3.one * Mathf.LerpUnclamped(start, target, t)));
        }
    }

    // Floating thumb stick. Pressing anywhere in its zone sets the stick's center.
    public class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float Travel = 120f;

        const float CenterTapRadius = 75f; // The middle of the pad, in canvas units.
        const float CenterTapSeconds = 0.25f;
        const float CenterTapMaxMove = 30f;

        public Vector2 Value { get; private set; }

        bool pressedCenter;
        bool centerTapped;
        float pressedAt;
        TextMeshProUGUI knobLabel;

        // A quick tap on the middle of the pad, as opposed to steering with it.
        public bool ConsumeCenterTap()
        {
            var tapped = centerTapped;
            centerTapped = false;
            return tapped;
        }

        public void SetKnobLabel(string label, Color knobColor)
        {
            knobLabel.text = label;
            knob.GetComponent<Image>().color = knobColor;
        }

        RectTransform baseRing;
        RectTransform knob;
        Vector2 restPosition;
        Canvas canvas;

        public static VirtualStick Create(RectTransform zone)
        {
            var stick = zone.gameObject.AddComponent<VirtualStick>();
            var catcher = zone.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            catcher.raycastTarget = true;

            var ring = new GameObject("Stick Base", typeof(RectTransform)).AddComponent<Image>();
            ring.transform.SetParent(zone, false);
            ring.sprite = UIKit.Circle;
            ring.color = Palette.WithAlpha(Palette.Panel, 0.55f);
            ring.raycastTarget = false;
            stick.baseRing = UIKit.Place(ring.rectTransform, Vector2.zero, Vector2.one * 0.5f, new Vector2(220f, 220f), Vector2.one * 300f);

            var knobImage = new GameObject("Stick Knob", typeof(RectTransform)).AddComponent<Image>();
            knobImage.transform.SetParent(ring.transform, false);
            knobImage.sprite = UIKit.Circle;
            knobImage.color = Palette.WithAlpha(Palette.Accent, 0.9f);
            knobImage.raycastTarget = false;
            stick.knob = UIKit.Place(knobImage.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 120f);

            stick.knobLabel = UIKit.Text("Knob Label", knobImage.transform, "", 24f, Palette.TextLight);
            stick.knobLabel.outlineWidth = 0.2f;
            stick.knobLabel.outlineColor = Palette.Ink;
            UIKit.Place(stick.knobLabel.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(120f, 40f));

            stick.restPosition = stick.baseRing.anchoredPosition;
            stick.canvas = zone.GetComponentInParent<Canvas>();
            return stick;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, null, out var local);
            var rect = ((RectTransform)transform).rect;
            var pressed = local - rect.min;
            pressedCenter = (pressed - restPosition).magnitude <= CenterTapRadius;
            pressedAt = Time.unscaledTime;
            baseRing.anchoredPosition = pressed;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRing, eventData.position, null, out var local);
            if (local.magnitude > CenterTapMaxMove) pressedCenter = false;
            var offset = Vector2.ClampMagnitude(local, Travel);
            knob.anchoredPosition = offset;
            Value = offset / Travel;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressedCenter && Time.unscaledTime - pressedAt <= CenterTapSeconds) centerTapped = true;
            pressedCenter = false;
            Value = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
            baseRing.anchoredPosition = restPosition;
        }
    }

    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Held { get; private set; }
        public bool Tapped { get; private set; }

        public bool ConsumeTap()
        {
            var tapped = Tapped;
            Tapped = false;
            return tapped;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Held = true;
            Tapped = true;
        }

        public void OnPointerUp(PointerEventData eventData) => Held = false;
    }

    // Screen-wide fades, flashes and transient banners, all on unscaled time.
    public class ScreenFx : MonoBehaviour
    {
        Image fade;
        Image flash;
        RectTransform floating;
        Coroutine bannerRoutine;
        GameObject banner;

        public static ScreenFx Create(Transform parent)
        {
            var canvas = UIKit.CreateCanvas("Screen Fx", parent, 100);
            var fx = canvas.gameObject.AddComponent<ScreenFx>();
            fx.floating = UIKit.Stretch("Floating Text", canvas.transform);
            fx.flash = UIKit.Solid("Flash", canvas.transform, Palette.WithAlpha(Palette.Accent, 0f));
            UIKit.Fill(fx.flash.rectTransform);
            fx.fade = UIKit.Solid("Fade", canvas.transform, Palette.WithAlpha(Palette.Ink, 0f));
            UIKit.Fill(fx.fade.rectTransform);
            return fx;
        }

        public IEnumerator FadeOut(float seconds = 0.3f)
        {
            fade.raycastTarget = true;
            yield return Ease.Tween(seconds, t => fade.color = Palette.WithAlpha(Palette.Ink, t), Ease.Linear);
        }

        public IEnumerator FadeIn(float seconds = 0.3f)
        {
            yield return Ease.Tween(seconds, t => fade.color = Palette.WithAlpha(Palette.Ink, 1f - t), Ease.Linear);
            fade.raycastTarget = false;
        }

        public void Flash(Color color, float alpha = 0.6f, float seconds = 0.12f)
        {
            StartCoroutine(Ease.Tween(seconds, t => flash.color = Palette.WithAlpha(color, alpha * (1f - t))));
        }

        public void FloatingNumber(Vector3 world, Camera camera, string value, Color color, float size)
        {
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            var text = UIKit.Text("Number", floating, value, size, color);
            text.outlineWidth = 0.2f;
            text.outlineColor = Palette.Ink;
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(400f, 100f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(floating, screen, null, out var local);
            var start = local - floating.rect.min;
            rect.anchoredPosition = start;
            StartCoroutine(FloatRoutine(text, start));
        }

        IEnumerator FloatRoutine(TextMeshProUGUI text, Vector2 start)
        {
            var rect = text.rectTransform;
            yield return Ease.Tween(0.75f, t =>
            {
                rect.anchoredPosition = start + Vector2.up * 90f * t;
                rect.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, Mathf.Min(1f, t * 4f));
                text.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            });
            Destroy(text.gameObject);
        }

        // The big moment: flash, sting, then a banner that pops in and leaves. Bad news gets a darker sting.
        public void Milestone(string message, Color color, Sfx? sound = null)
        {
            Flash(Palette.Accent);
            AudioBank.Play(sound ?? (color == Palette.Danger ? Sfx.DangerSting : Sfx.Sting), 1f, 0f);
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            if (banner != null) Destroy(banner);
            bannerRoutine = StartCoroutine(BannerRoutine(message, color));
        }

        IEnumerator BannerRoutine(string message, Color color)
        {
            var text = UIKit.Text("Banner", floating, message, 104f, color);
            banner = text.gameObject;
            text.enableAutoSizing = true;
            text.fontSizeMin = 40f;
            text.fontSizeMax = 104f;
            text.outlineWidth = 0.22f;
            text.outlineColor = Palette.Ink;
            var rect = text.rectTransform;
            UIKit.Place(rect, new Vector2(0.5f, 0.62f), Vector2.one * 0.5f, Vector2.zero, new Vector2(1600f, 160f));
            rect.localScale = Vector3.zero;
            yield return new WaitForSecondsRealtime(0.05f);
            yield return Ease.Tween(0.35f, t => rect.localScale = Vector3.one * t, Ease.OutBack);
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Ease.Tween(0.2f, t => rect.localScale = Vector3.one * (1f - t));
            Destroy(text.gameObject);
        }
    }
}
