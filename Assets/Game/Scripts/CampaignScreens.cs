using System;
using System.Collections;
using System.Collections.Generic;
using Airships.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Airships.Game
{
    // Paints a character's head once into a small texture, so comics and comms have faces.
    public static class Portraits
    {
        const int Size = 192;
        const int PortraitLayer = 31;
        static readonly Vector3 Studio = new Vector3(0f, -4000f, 0f);
        static readonly Dictionary<string, Texture> Cache = new Dictionary<string, Texture>();

        public static Texture Get(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return null;
            if (Cache.TryGetValue(classId, out var cached) && cached != null) return cached;

            var root = new GameObject("Portrait Studio");
            root.transform.position = Studio;
            var model = Models.CreateCharacter("Portrait", classId, root.transform);
            model.Root.transform.localRotation = Quaternion.Euler(0f, 160f, 0f);
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PortraitLayer;

            var head = KitArt.Socket(model.Root, "head_top");
            var headAt = head != null ? head.position : Studio + Vector3.up * 2f;
            var cameraObject = new GameObject("Portrait Camera");
            cameraObject.transform.position = headAt + new Vector3(0.15f, -0.25f, -1.6f);
            cameraObject.transform.LookAt(headAt + Vector3.down * 0.3f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.PanelLight;
            camera.cullingMask = 1 << PortraitLayer;
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;

            var texture = new RenderTexture(Size, Size, 16) { name = "Portrait " + classId };
            camera.targetTexture = texture;
            camera.Render();
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(root);
            Cache[classId] = texture;
            return texture;
        }
    }

    // A round framed portrait, shared by the comms bar and the comic panels.
    public class PortraitView
    {
        public RectTransform Root;
        RawImage face;
        Image bust;
        TextMeshProUGUI initial;

        public static PortraitView Create(Transform parent, float size)
        {
            var view = new PortraitView();
            var frame = UIKit.Panel("Portrait", parent, Palette.Accent);
            view.Root = frame.rectTransform;
            view.Root.sizeDelta = Vector2.one * size;
            view.face = new GameObject("Face", typeof(RectTransform)).AddComponent<RawImage>();
            view.face.transform.SetParent(frame.transform, false);
            view.face.raycastTarget = false;
            UIKit.Fill(view.face.rectTransform);
            view.face.rectTransform.offsetMin = Vector2.one * 6f;
            view.face.rectTransform.offsetMax = Vector2.one * -6f;
            var bustImage = new GameObject("Bust", typeof(RectTransform)).AddComponent<Image>();
            bustImage.transform.SetParent(frame.transform, false);
            bustImage.raycastTarget = false;
            UIKit.Fill(bustImage.rectTransform);
            bustImage.rectTransform.offsetMin = Vector2.one * 6f;
            bustImage.rectTransform.offsetMax = Vector2.one * -6f;
            view.bust = bustImage;
            view.initial = UIKit.Text("Initial", frame.transform, "", size * 0.5f, Palette.TextLight);
            UIKit.Fill(view.initial.rectTransform);
            return view;
        }

        public void Show(SpeakerDef speaker, string fallbackName)
        {
            // The kit paints the story's main cast. Everyone else gets a head shot of their model.
            var lookId = speaker != null ? speaker.look : null;
            var look = !string.IsNullOrEmpty(lookId) ? GameDirector.Instance.Content.Look(lookId) : null;
            var painted = !string.IsNullOrEmpty(speaker?.portrait) ? KitArt.Portrait(speaker.portrait) : look != null ? KitArt.Portrait(look.portrait) : null;
            bust.sprite = painted;
            bust.enabled = painted != null;
            var texture = painted == null && speaker != null ? Portraits.Get(!string.IsNullOrEmpty(lookId) ? lookId : speaker.classId) : null;
            face.texture = texture;
            face.enabled = texture != null;
            initial.text = texture == null && painted == null && !string.IsNullOrEmpty(fallbackName) ? fallbackName.Substring(0, 1) : "";
        }
    }

    // Crew and enemy barks across the top of the screen, so the story plays during the action. In a live
    // dogfight they time out, because both thumbs are busy. Everywhere else each line waits for a tap on the bar.
    public class CommsBar : MonoBehaviour, IPointerClickHandler
    {
        const float SecondsPerCharacter = 0.05f;
        const float MinimumSeconds = 2.4f;

        readonly Queue<CommsLine> queue = new Queue<CommsLine>();
        CampaignDef campaign;
        RectTransform panel;
        PortraitView portrait;
        TextMeshProUGUI speaker;
        TextMeshProUGUI line;
        TextMeshProUGUI tapHint;
        bool waitsForTap;
        float showing;
        float slide;
        Vector2 restPosition;

        public static CommsBar Create(RectTransform safe, CampaignDef campaign, float y, bool tapToAdvance = true)
        {
            var image = UIKit.Panel("Comms", safe, Palette.WithAlpha(Palette.Panel, 0.98f), tapToAdvance);
            UIKit.Place(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(960f, 128f));
            var bar = image.gameObject.AddComponent<CommsBar>();
            bar.campaign = campaign;
            bar.waitsForTap = tapToAdvance && !GameDirector.Autoplay;
            bar.panel = image.rectTransform;
            bar.restPosition = image.rectTransform.anchoredPosition;
            bar.portrait = PortraitView.Create(image.transform, 108f);
            UIKit.Place(bar.portrait.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), Vector2.one * 108f);
            var commsIcon = UIKit.Icon("Comms Icon", image.transform, "Hud_Comms");
            UIKit.Place(commsIcon.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -4f), Vector2.one * 34f);
            bar.speaker = UIKit.Text("Speaker", image.transform, "", 26f, Palette.Accent, TextAlignmentOptions.Left);
            UIKit.Place(bar.speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(134f, -6f), new Vector2(810f, 34f));
            bar.line = UIKit.Text("Line", image.transform, "", 27f, Palette.TextLight, TextAlignmentOptions.TopLeft, false);
            bar.line.textWrappingMode = TextWrappingModes.Normal;
            bar.line.characterSpacing = 0f;
            UIKit.Place(bar.line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(134f, -42f), new Vector2(812f, 82f));
            bar.tapHint = UIKit.Text("Tap Hint", image.transform, "TAP", 20f, Palette.Accent, TextAlignmentOptions.Right);
            UIKit.Place(bar.tapHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 4f), new Vector2(80f, 26f));
            bar.tapHint.enabled = bar.waitsForTap;
            image.rectTransform.anchoredPosition = bar.restPosition + Vector2.up * 260f;
            return bar;
        }

        public void Say(CommsLine next)
        {
            if (next != null && (waitsForTap || queue.Count < 4)) queue.Enqueue(next);
        }

        void Update()
        {
            if (showing > 0f)
            {
                if (!waitsForTap) showing -= Time.unscaledDeltaTime;
            }
            else if (queue.Count > 0)
            {
                Present(queue.Dequeue());
            }
            var target = showing > 0f ? 1f : 0f;
            slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime * 5f);
            panel.anchoredPosition = restPosition + Vector2.up * (260f * (1f - Ease.OutCubic(slide)));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!waitsForTap || showing <= 0f) return;
            showing = 0f;
            AudioBank.Play(Sfx.Click);
        }

        void Present(CommsLine next)
        {
            var who = campaign?.Speaker(next.speaker);
            var name = who != null ? who.name : next.speaker;
            speaker.text = name;
            line.text = next.text;
            portrait.Show(who, name);
            showing = Mathf.Max(MinimumSeconds, next.text.Length * SecondsPerCharacter) * (queue.Count > 1 ? 0.7f : 1f);
            AudioBank.Play(Sfx.Click, 0.35f);
        }
    }

    // Comic panels: a face, a name and a line, one tap each. Never more than a few, and always skippable.
    public class ComicPanel : MonoBehaviour, IPointerClickHandler
    {
        const float RevealCharactersPerSecond = 70f;

        CommsLine[] lines;
        Action finished;
        Action<string> onSpeaker;
        CampaignDef campaign;
        PortraitView portrait;
        TextMeshProUGUI speaker;
        TextMeshProUGUI text;
        int index = -1;
        float revealed;
        bool done;

        public static ComicPanel Create(Transform parent, CampaignDef campaign, string title, CommsLine[] lines, Action finished, Action<string> onSpeaker = null)
        {
            var scrim = UIKit.Solid("Comic", parent, Palette.WithAlpha(Palette.Ink, 0.55f), true);
            UIKit.Fill(scrim.rectTransform);
            var comic = scrim.gameObject.AddComponent<ComicPanel>();
            comic.lines = lines ?? new CommsLine[0];
            comic.finished = finished;
            comic.onSpeaker = onSpeaker;
            comic.campaign = campaign;

            var safe = UIKit.SafeArea(scrim.transform);
            if (!string.IsNullOrEmpty(title))
            {
                var heading = UIKit.Text("Title", safe, title.ToUpperInvariant(), 58f, Palette.Accent);
                heading.outlineWidth = 0.2f;
                heading.outlineColor = Palette.Ink;
                UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1500f, 90f));
            }
            var panel = UIKit.Panel("Panel", safe, Palette.WithAlpha(Palette.Panel, 0.97f));
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1560f, 400f));
            comic.portrait = PortraitView.Create(panel.transform, 340f);
            UIKit.Place(comic.portrait.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), Vector2.one * 340f);
            comic.speaker = UIKit.Text("Speaker", panel.transform, "", 44f, Palette.Accent, TextAlignmentOptions.Left);
            UIKit.Place(comic.speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, -26f), new Vector2(1120f, 60f));
            comic.text = UIKit.Text("Text", panel.transform, "", 40f, Palette.TextLight, TextAlignmentOptions.TopLeft, false);
            comic.text.textWrappingMode = TextWrappingModes.Normal;
            comic.text.characterSpacing = 0f;
            UIKit.Place(comic.text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, -96f), new Vector2(1120f, 270f));

            var skip = UIKit.Button("Skip", safe, "SKIP", 34f, Palette.PanelLight, comic.Finish);
            UIKit.Place((RectTransform)skip.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(200f, 90f));
            comic.Next();
            if (GameDirector.Autoplay) comic.StartCoroutine(comic.AutoAdvance());
            return comic;
        }

        IEnumerator AutoAdvance()
        {
            while (!done)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                revealed = 9999f;
                Next();
            }
        }

        void Update()
        {
            if (done || index < 0 || index >= lines.Length) return;
            revealed += RevealCharactersPerSecond * Time.unscaledDeltaTime;
            text.maxVisibleCharacters = (int)revealed;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (index >= 0 && index < lines.Length && revealed < lines[index].text.Length)
            {
                revealed = lines[index].text.Length;
                text.maxVisibleCharacters = (int)revealed;
                return;
            }
            AudioBank.Play(Sfx.Click);
            Next();
        }

        void Next()
        {
            if (done) return;
            index++;
            if (index >= lines.Length)
            {
                Finish();
                return;
            }
            var line = lines[index];
            var who = campaign?.Speaker(line.speaker);
            var name = who != null ? who.name : line.speaker;
            speaker.text = name;
            text.text = line.text;
            text.maxVisibleCharacters = 0;
            revealed = 0f;
            portrait.Show(who, name);
            onSpeaker?.Invoke(line.speaker);
        }

        void Finish()
        {
            if (done) return;
            done = true;
            finished?.Invoke();
        }
    }

    // A story beat: comic panels over a staged 3D scene when the phase has one, or over the ship in flight.
    public class StoryScreen : MonoBehaviour
    {
        readonly Dictionary<string, List<CharacterModel>> actors = new Dictionary<string, List<CharacterModel>>();
        string speaking;

        public void Setup(PhaseDef phase)
        {
            var director = GameDirector.Instance;
            if (phase.shots != null && phase.shots.Length > 0)
            {
                gameObject.AddComponent<Cutscene>().Play(phase.shots, director.AdvancePhase);
                return;
            }
            var weather = !string.IsNullOrEmpty(phase.weather) ? phase.weather : CampaignLook.WeatherFor(director.Run.Mission);
            director.Stage.ApplyWeather(director.Content.Weather(weather));
            if (weather == "storm" || weather == "maelstrom" || weather == "cyclone") AudioBank.SetDanger(1f);

            if (!string.IsNullOrEmpty(phase.arena)) BuildStage(phase, director);
            else
            {
                OrbitBackdrop.Create(transform, director.Stage, new Vector3(0f, 8f, -62f), new Vector3(0f, 8f, 0f), 42f, !string.IsNullOrEmpty(phase.ship) ? phase.ship : director.DisplayShip());
                SkyScenery.BuildStoryBackdrop(phase.backdrop, transform);
            }
            var canvas = UIKit.CreateCanvas("Story", transform, 10);
            ComicPanel.Create(canvas.transform, director.Content.campaign, phase.title, phase.lines, director.AdvancePhase, Speak);
        }

        void BuildStage(PhaseDef phase, GameDirector director)
        {
            var deck = Models.CreateDeck(transform, phase.arenaFaction, phase.arena);
            var rig = new GameObject("Stage Camera Rig").transform;
            rig.SetParent(transform, false);
            var look = new GameObject("Stage Look").transform;
            look.SetParent(transform, false);
            look.position = new Vector3(0f, 0.1f, 0f);
            director.Stage.FrameTarget(rig, look, new Vector3(0f, 1.7f, -8.8f), 46f, 0f);

            if (phase.actors == null) return;
            foreach (var actor in phase.actors)
            {
                var model = Models.CreateCharacter(actor.speaker, actor.look, deck.Root.transform);
                var mark = KitArt.Socket(deck.Kit, actor.mark);
                if (mark != null) model.Root.transform.position = mark.position;
                model.Root.transform.localRotation = Quaternion.Euler(0f, actor.yaw, 0f);
                model.Animation.Loop(string.IsNullOrEmpty(actor.clip) ? "Idle" : actor.clip, UnityEngine.Random.value);
                if (!actors.TryGetValue(actor.speaker, out var list))
                {
                    list = new List<CharacterModel>();
                    actors[actor.speaker] = list;
                }
                list.Add(model);
            }
        }

        // Whoever's line it is talks, and the last speaker settles back to idle.
        void Speak(string speaker)
        {
            if (speaking != null && actors.TryGetValue(speaking, out var previous))
            {
                foreach (var model in previous) model.Animation.Loop("Idle", 0f);
            }
            speaking = speaker;
            if (actors.TryGetValue(speaker, out var current))
            {
                foreach (var model in current) model.Animation.Loop("Talk", 0f);
            }
        }
    }

    public static class CampaignLook
    {
        // A sky to match each act when no fight has set one.
        public static string WeatherFor(MissionDef mission)
        {
            switch (mission.act)
            {
                case 0: return "heist";
                case 1: return "scrapwinds";
                case 2: return "jetstream";
                case 3: return "citadel";
                default: return "storm";
            }
        }

        public static string ActName(GameContent content, int act)
        {
            var found = Array.Find(content.campaign.acts, a => a.number == act);
            return found != null ? $"{found.name}: {found.setting}" : "";
        }

        public static void ScrollList(Transform parent, Vector2 size, Vector2 position, Vector2 anchor, out RectTransform content)
        {
            var viewport = UIKit.Panel("Viewport", parent, Color.white, true);
            UIKit.Place(viewport.rectTransform, anchor, anchor, position, size);
            viewport.color = new Color(1f, 1f, 1f, 0.004f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UIKit.Node("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }
    }

    // After a captain falls: the freeze-frame choice that blends story and progression.
    public class ChoiceScreen : MonoBehaviour
    {
        PhaseDef phase;
        GameDirector director;
        CharacterModel captain;
        Transform sceneRoot;
        bool chosen;

        public void Setup(PhaseDef choicePhase) => phase = choicePhase;

        void Start()
        {
            director = GameDirector.Instance;
            var content = director.Content;
            var ship = content.Ship(phase.shipId);
            BuildScene();

            var canvas = UIKit.CreateCanvas("Davy Jones", transform, 10);
            var safe = UIKit.SafeArea(canvas.transform);
            var headingBack = UIKit.Panel("Heading Back", safe, Palette.WithAlpha(Palette.Panel, 0.8f));
            UIKit.Place(headingBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(1560f, 164f));
            var heading = UIKit.Text("Heading", safe, "DAVY JONES' CHOICE", 72f, Palette.Accent);
            heading.outlineWidth = 0.2f;
            heading.outlineColor = Palette.Ink;
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1500f, 90f));
            var sub = UIKit.Text("Captain", safe, $"{phase.captain} is beaten, and {ship.name} is yours to settle.", 36f, Palette.TextLight);
            sub.outlineWidth = 0.2f;
            sub.outlineColor = Palette.Ink;
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(1500f, 50f));

            var officer = !string.IsNullOrEmpty(phase.officer) ? content.Crew(phase.officer) : null;
            var officerLine = officer == null ? "Spare the captain for repairs." : director.Save.crew.Find(c => c.id == officer.id) != null ? $"{officer.name} is already aboard." : $"{officer.name} joins your crew.";
            var module = !string.IsNullOrEmpty(phase.module) ? content.Module(phase.module) : null;
            var signature = !string.IsNullOrEmpty(phase.signature) ? content.Signature(phase.signature) : null;

            AddCard(safe, -1, "SEIZE THE HELM", "Choice_SeizeHelm", new[] { $"Make {ship.name} your ship.", $"Hull {ship.hull}, armor {ship.armor}, speed {ship.speed}.", "Your old ship sails home with a prize crew." }, Palette.PlayerPaint, DavyJonesChoice.SeizeTheHelm);
            AddCard(safe, 0, "PRESS-GANG THE CREW", "Choice_PressGang", new[] { officerLine, "Repairs half your ship's damage.", "The captain lives." }, Palette.Herb, DavyJonesChoice.PressGang);
            AddCard(safe, 1, "SCUTTLE & PLUNDER", "Choice_ScuttlePlunder", new[] { $"Take {phase.gold} gold.", module != null ? $"Strip the {module.name}: {module.description}." : "Strip what's left for parts.", "The captain gets a lifeboat." }, Palette.Accent, DavyJonesChoice.ScuttleAndPlunder);

            if (signature != null)
            {
                var prize = UIKit.Panel("Signature", safe, Palette.WithAlpha(Palette.Panel, 0.94f));
                UIKit.Place(prize.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1500f, 64f));
                var label = UIKit.Text("Text", prize.transform, $"Whatever you choose, you keep their weapon. {signature.name}: {signature.description}", 27f, Palette.Accent, TextAlignmentOptions.Center, false);
                label.characterSpacing = 0f;
                UIKit.Fill(label.rectTransform);
            }

            if (phase.lines != null && phase.lines.Length > 0)
            {
                var bar = CommsBar.Create(safe, content.campaign, -160f);
                bar.Say(phase.lines[0]);
            }
            if (GameDirector.Autoplay) StartCoroutine(AutoChoose());
        }

        // The captain surrenders on their own deck, with Vane standing over them.
        void BuildScene()
        {
            var deck = Models.CreateDeck(transform, director.Content.Ship(phase.shipId).faction);
            var rig = new GameObject("Choice Camera Rig").transform;
            rig.SetParent(transform, false);
            var look = new GameObject("Choice Look").transform;
            look.SetParent(transform, false);
            look.position = new Vector3(0f, 1.9f, 0f);
            director.Stage.FrameTarget(rig, look, new Vector3(0f, 2.1f, -7.4f), 44f, 0f);

            captain = Models.CreateCharacter(phase.captain, !string.IsNullOrEmpty(phase.captainLook) ? phase.captainLook : "pirate_captain", deck.Root.transform);
            captain.Root.transform.localPosition = new Vector3(1.6f, 0f, 0.4f);
            captain.Root.transform.localRotation = Quaternion.Euler(0f, -130f, 0f);
            captain.Animation.Play("Surrender");
            var vane = Models.CreateCharacter("Vane", "vane", deck.Root.transform);
            vane.Root.transform.localPosition = new Vector3(-1.5f, 0f, 0.4f);
            vane.Root.transform.localRotation = Quaternion.Euler(0f, 125f, 0f);
            vane.Animation.Loop("Taunt");
            sceneRoot = deck.Root.transform;
        }

        IEnumerator AutoChoose()
        {
            yield return new WaitForSecondsRealtime(1f);
            Choose(DavyJonesChoice.SeizeTheHelm);
        }

        void AddCard(RectTransform safe, int column, string title, string icon, string[] lines, Color color, DavyJonesChoice choice)
        {
            var card = UIKit.Panel(title, safe, Palette.WithAlpha(Palette.Panel, 0.93f));
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(column * 620f, 88f), new Vector2(600f, 400f));
            var image = UIKit.Icon("Icon", card.transform, icon);
            UIKit.Place(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), Vector2.one * 96f);
            for (var i = 0; i < lines.Length; i++)
            {
                var text = UIKit.Text("Line", card.transform, lines[i], 26f, Palette.TextLight, TextAlignmentOptions.TopLeft, false);
                text.textWrappingMode = TextWrappingModes.Normal;
                text.characterSpacing = 0f;
                var indent = i == 0 ? 120f : 18f;
                UIKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(indent, i == 0 ? -24f : -34f - i * 76f), new Vector2(580f - indent, 80f));
            }
            var button = UIKit.Button(title, card.transform, title, 32f, color, () => Choose(choice));
            button.ClickSound = Sfx.Confirm;
            UIKit.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(570f, 92f));
        }

        void Choose(DavyJonesChoice choice)
        {
            if (chosen) return;
            chosen = true;
            Debug.Log($"[Flow] Davy Jones' choice: {choice}");
            Campaign.ApplyChoice(director.Content, director.Save, phase, choice);
            director.SaveGame();
            director.ScreenFx.Flash(Palette.Accent, 0.3f);
            AudioBank.Play(Sfx.Sting, 0.8f);
            StartCoroutine(Outcome(choice));
        }

        // A last look at what the choice means, before the story moves on.
        IEnumerator Outcome(DavyJonesChoice choice)
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
            switch (choice)
            {
                case DavyJonesChoice.SeizeTheHelm:
                    director.ScreenFx.Milestone("THE HELM IS YOURS!", Palette.Accent);
                    break;
                case DavyJonesChoice.PressGang:
                    captain.Animation.Loop("Idle");
                    director.ScreenFx.Milestone("WELCOME ABOARD!", Palette.Heal);
                    break;
                default:
                    var boat = KitArt.Spawn("SM_Ship_Lifeboat", sceneRoot);
                    KitArt.Paint(boat, KitArt.NewMaterial("Lifeboat", KitArt.Crimson));
                    boat.transform.localPosition = new Vector3(3.6f, 0.6f, -1.2f);
                    boat.AddComponent<FallingChunk>().Velocity = new Vector3(6f, 9f, 0f);
                    Effects.Play("CannonSmoke", boat.transform.position, 2f);
                    Effects.Play("MortarImpact", sceneRoot.position + new Vector3(-5f, 1f, 2f));
                    director.ScreenFx.Milestone("SCUTTLED!", Palette.Danger);
                    break;
            }
            yield return new WaitForSecondsRealtime(GameDirector.Autoplay ? 0.4f : 1.7f);
            director.AdvancePhase();
        }
    }

    // Splits the crew into who boards and who stays to hold the ship.
    public class AssignScreen : MonoBehaviour
    {
        PhaseDef phase;
        GameDirector director;
        readonly List<Action> refreshers = new List<Action>();
        GameButton confirm;
        TextMeshProUGUI status;

        public void Setup(PhaseDef assignPhase) => phase = assignPhase;

        void Start()
        {
            director = GameDirector.Instance;
            OrbitBackdrop.Create(transform, director.Stage, new Vector3(0f, 8f, -62f), new Vector3(24f, 6f, 0f), 40f, director.DisplayShip());
            var canvas = UIKit.CreateCanvas("Assign", transform, 10);
            var safe = UIKit.SafeArea(canvas.transform);
            var panel = UIKit.Panel("Panel", safe, Palette.WithAlpha(Palette.Panel, 0.95f));
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1300f, 880f));
            var title = UIKit.Text("Title", panel.transform, (phase.title ?? "Divide the Crew").ToUpperInvariant(), 56f, Palette.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1200f, 76f));
            status = UIKit.Text("Status", panel.transform, "", 30f, Palette.TextLight, TextAlignmentOptions.Center, false);
            UIKit.Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(1200f, 44f));

            CampaignLook.ScrollList(panel.transform, new Vector2(1240f, 580f), new Vector2(0f, -146f), new Vector2(0.5f, 1f), out var list);
            ((RectTransform)list.parent).pivot = new Vector2(0.5f, 1f);
            ((RectTransform)list.parent).anchoredPosition = new Vector2(0f, -146f);
            var crew = director.Save.crew;
            list.sizeDelta = new Vector2(0f, crew.Count * 112f);
            for (var i = 0; i < crew.Count; i++) AddRow(list, crew[i], i);

            confirm = UIKit.Button("Confirm", panel.transform, "READY", 46f, Palette.PlayerPaint, Confirm);
            confirm.ClickSound = Sfx.Confirm;
            UIKit.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(480f, 110f));

            Refresh();
            if (phase.lines != null)
            {
                var bar = CommsBar.Create(safe, director.Content.campaign, -176f);
                foreach (var line in phase.lines) bar.Say(line);
            }
            if (crew.Count < 2 || GameDirector.Autoplay) StartCoroutine(AutoConfirm());
        }

        IEnumerator AutoConfirm()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            if (director.Save.crew.Count > 1 && director.Save.crew.Count == director.Save.party.Count) director.Save.party.RemoveAt(director.Save.party.Count - 1);
            Confirm();
        }

        void AddRow(RectTransform list, CrewProgress progress, int index)
        {
            var member = director.Content.Crew(progress.id);
            var cls = director.Content.Class(member.classId);
            var row = UIKit.Panel(member.name, list, Palette.WithAlpha(Palette.PanelLight, 0.8f));
            UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * 112f), new Vector2(1200f, 102f));
            var portrait = PortraitView.Create(row.transform, 86f);
            UIKit.Place(portrait.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), Vector2.one * 86f);
            portrait.Show(new SpeakerDef { classId = member.classId, look = member.look }, member.name);
            var name = UIKit.Text("Name", row.transform, $"{member.name}  LV {progress.level}", 34f, Palette.TextLight, TextAlignmentOptions.Left);
            UIKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 16f), new Vector2(660f, 46f));
            var role = UIKit.Text("Role", row.transform, cls.role, 25f, Palette.TextMuted, TextAlignmentOptions.Left, false);
            UIKit.Place(role.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, -22f), new Vector2(660f, 36f));

            GameButton toggle = null;
            toggle = UIKit.Button("Toggle", row.transform, "", 28f, Palette.Panel, () => Toggle(progress.id));
            UIKit.Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(380f, 84f));
            toggle.Label.rectTransform.offsetMin = new Vector2(68f, 4f);
            var roleIcon = UIKit.Icon("Role Icon", toggle.transform, "Hud_Board");
            UIKit.Place(roleIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), Vector2.one * 62f);
            refreshers.Add(() =>
            {
                var boarding = director.Save.IsInParty(progress.id);
                toggle.Label.text = progress.id == Campaign.Protagonist ? "BOARDING: ALWAYS" : boarding ? "BOARDING" : "HOLDING THE HELM";
                toggle.Label.color = boarding ? Palette.Accent : Palette.Heal;
                roleIcon.sprite = KitArt.Icon(boarding ? "Hud_Board" : "Hud_Defend");
            });
        }

        void Toggle(string id)
        {
            if (id == Campaign.Protagonist) return;
            var party = director.Save.party;
            if (party.Contains(id)) party.Remove(id);
            else if (party.Count < BattleSetup.MaxPartySize) party.Add(id);
            Refresh();
        }

        void Refresh()
        {
            foreach (var refresh in refreshers) refresh();
            var save = director.Save;
            var defenders = save.crew.Count - save.party.Count;
            var exposure = Mathf.RoundToInt(100f * Campaign.BoardingExposure(director.Content, save));
            var needs = Mathf.Min(phase.minDefenders, save.crew.Count - 1);
            status.text = defenders < needs ? "Someone has to stay and hold the helm." : $"{save.party.Count} boarding, {defenders} on the helm. Hull at risk while you're away: {exposure}%.";
            status.color = defenders < needs ? Palette.Danger : Palette.TextLight;
            if (confirm != null) confirm.Interactable = defenders >= needs;
        }

        void Confirm()
        {
            director.SaveGame();
            director.AdvancePhase();
        }
    }

    // The main menu is the deck of the ship you command. Crew stand around it and can be talked to.
    public class QuarterdeckScreen : MonoBehaviour
    {
        const int MaxCrewOnDeck = 7;
        static readonly Vector3 HelmStep = new Vector3(-1.3f, 0f, -0.4f); // Beside the wheel, not hidden behind it.
        static readonly Vector3 CompanionHubStep = new Vector3(-1.2f, 0f, -2f); // Forward of its owner, where it can be seen.

        // The crew gather below the helm, close enough to read as a crew rather than six strangers.
        static readonly Dictionary<string, Vector2> DeckSpots = new Dictionary<string, Vector2>
        {
            { "sable", new Vector2(-5.5f, 0.9f) },
            { "lyra", new Vector2(-2.7f, 0.6f) },
            { "morrow", new Vector2(-3f, -1.6f) },
            { "rook", new Vector2(0.9f, 0.5f) },
            { "thorne", new Vector2(2.6f, -0.8f) },
        };
        const float CardWidth = 760f;
        const float CardMargin = 28f;
        const float ClearMargin = 40f; // Between the crew and the screen's edge or the card.
        const float PersonHeight = 2.2f;
        const float PersonHalfWidth = 0.7f;
        const float HubFov = 46f;
        const float TagStagger = 44f; // Neighbors' name tags sit at different heights, so they never overlap.

        GameDirector director;
        Canvas canvas;
        RectTransform safe;
        DeckModel deck;
        TextMeshProUGUI goldText;
        GameObject overlay;
        GameObject settings;
        readonly List<GameObject> tags = new List<GameObject>();
        readonly Dictionary<string, CharacterModel> modelByCrew = new Dictionary<string, CharacterModel>();
        bool dressed;
        float time;
        Transform cameraRig;
        Transform cameraLook;

        void Start()
        {
            director = GameDirector.Instance;
            var content = director.Content;
            var save = director.Save;
            var shipDef = content.Ship(string.IsNullOrEmpty(save.activeShip) ? content.campaign.startingShip : save.activeShip);
            deck = Models.CreateDeck(transform, string.IsNullOrEmpty(shipDef.faction) ? KitArt.Crimson : shipDef.faction, "SM_Env_QuarterdeckHub");
            cameraRig = new GameObject("Hub Camera Rig").transform;
            cameraRig.SetParent(transform, false);
            cameraLook = new GameObject("Hub Look").transform;
            cameraLook.SetParent(transform, false);

            canvas = UIKit.CreateCanvas("Quarterdeck", transform, 10);
            safe = UIKit.SafeArea(canvas.transform);
            BuildHeader(shipDef);
            BuildMenu();
            StartCoroutine(BuildCrew());
            if (GameDirector.Autoplay) StartCoroutine(AutoPlay());
        }

        void BuildHeader(ShipDef shipDef)
        {
            var panel = UIKit.Panel("Ship", safe, Palette.WithAlpha(Palette.Panel, 0.92f));
            UIKit.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(760f, 130f));
            var name = UIKit.Text("Ship Name", panel.transform, shipDef.name, 42f, Palette.Accent, TextAlignmentOptions.Left);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(720f, 56f));
            goldText = UIKit.Text("Gold", panel.transform, "", 32f, Palette.TextLight, TextAlignmentOptions.Left);
            UIKit.Place(goldText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, -70f), new Vector2(660f, 46f));
            var coin = UIKit.Icon("Gold Icon", panel.transform, "Item_Gold");
            UIKit.Place(coin.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -64f), Vector2.one * 56f);
            RefreshHeader();
        }

        void RefreshHeader()
        {
            var save = director.Save;
            var damage = Campaign.ShipDamage(save, save.activeShip);
            goldText.text = damage > 0f ? $"{save.gold} GOLD   HULL DAMAGE {Mathf.RoundToInt(damage)}" : $"{save.gold} GOLD";
        }

        void BuildMenu()
        {
            var content = director.Content;
            var save = director.Save;
            var next = Campaign.NextStoryMission(save, content);

            var card = UIKit.Panel("Next Mission", safe, Palette.WithAlpha(Palette.Panel, 0.94f));
            UIKit.Place(card.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -20f), new Vector2(760f, 440f));
            if (next != null)
            {
                AddCardText(card.transform, CampaignLook.ActName(content, next.act), 26f, Palette.TextMuted, -16f, 40f);
                AddCardText(card.transform, next.title, 42f, Palette.Accent, -58f, 110f);
                var blurb = AddCardText(card.transform, next.blurb, 29f, Palette.TextLight, -168f, 130f);
                blurb.fontStyle = FontStyles.Normal;
                var resuming = save.campaign.activeMission == next.id && save.campaign.activePhase > 0;
                var play = UIKit.Button("Play", card.transform, resuming ? "RESUME" : "SET SAIL", 50f, Palette.PlayerPaint, () => director.PlayMission(next, resuming ? save.campaign.activePhase : 0));
                play.ClickSound = Sfx.Confirm;
                UIKit.Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(700f, 116f));
            }
            else
            {
                AddCardText(card.transform, content.campaign.title, 26f, Palette.TextMuted, -16f, 40f);
                AddCardText(card.transform, "THE STORY IS DONE", 44f, Palette.Accent, -58f, 70f);
                var ending = AddCardText(card.transform, content.campaign.ending, 30f, Palette.TextLight, -136f, 150f);
                ending.fontStyle = FontStyles.Normal;
            }

            var y = 16f;
            AddMenuButton("BOUNTY BOARD", "Story_Bounty", ref y, ShowBoard, Campaign.PrologueDone(save, content), BoardCount() > 0 ? Palette.Danger : Palette.PanelLight);
            AddMenuButton("VOYAGE LOG", "Hud_Map", ref y, ShowLog, Campaign.PrologueDone(save, content), Palette.PanelLight);
            AddMenuButton("FLEET", "Ship_RustWing", ref y, ShowFleet, save.fleet.Count > 0, Palette.PanelLight);
            AddMenuButton("CREW & SHIPWRIGHT", "Hud_Crew", ref y, director.ShowPort, Campaign.PrologueDone(save, content), Palette.PanelLight);
            AddMenuButton("SETTINGS", "Hud_Settings", ref y, ToggleSettings, true, Palette.Panel);
            BuildCiphers(card.transform);
        }

        // During Act II, the keys you've taken so far.
        void BuildCiphers(Transform card)
        {
            var found = Campaign.CiphersFound(director.Save, director.Content);
            var total = Campaign.CiphersTotal(director.Content);
            if (found == 0 && director.Save.campaign.completed.Find(m => m == "a2_lyra") == null) return;
            if (found >= total) return;
            for (var i = 0; i < total; i++)
            {
                var key = UIKit.Icon("Cipher", card, "Item_CipherKey");
                UIKit.Place(key.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f - i * 56f, -8f), Vector2.one * 52f);
                key.color = total - 1 - i < found ? Color.white : new Color(0.3f, 0.3f, 0.4f, 0.7f);
            }
        }

        void ShowLog()
        {
            BuildOverlay("VOYAGE LOG", out var panel);
            var content = director.Content;
            var save = director.Save;
            CampaignLook.ScrollList(panel, new Vector2(1440f, 830f), new Vector2(0f, 24f), new Vector2(0.5f, 0f), out var list);
            ((RectTransform)list.parent).pivot = new Vector2(0.5f, 0f);
            var y = 0f;
            var next = Campaign.NextStoryMission(save, content);
            foreach (var act in content.campaign.acts)
            {
                var heading = UIKit.Text("Act", list, $"{act.name}: {act.setting}", 34f, Palette.Accent, TextAlignmentOptions.Left);
                UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1380f, 50f));
                y += 56f;
                foreach (var definition in content.campaign.missions)
                {
                    if (definition.act != act.number || !definition.IsStory) continue;
                    var mission = definition;
                    var done = Campaign.IsDone(save, mission.id);
                    var row = UIKit.Panel(mission.title, list, Palette.WithAlpha(mission == next ? Palette.PlayerPaint : Palette.PanelLight, done || mission == next ? 0.8f : 0.3f));
                    UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1380f, 76f));
                    var label = done || mission == next ? mission.title : "???";
                    var text = UIKit.Text("Name", row.transform, label, 30f, done || mission == next ? Palette.TextLight : Palette.TextMuted, TextAlignmentOptions.Left);
                    UIKit.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(1000f, 60f));
                    var status = UIKit.Icon("Status", row.transform, done ? "Item_Treasure" : mission == next ? "Marker_Objective" : "Hud_Locked");
                    UIKit.Place(status.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), Vector2.one * 60f);
                    if (done)
                    {
                        var replay = UIKit.Button("Replay", row.transform, "REPLAY", 26f, Palette.Panel, () => director.PlayMission(mission, 0));
                        UIKit.Place((RectTransform)replay.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(180f, 60f));
                    }
                    y += 84f;
                }
                y += 10f;
            }
            list.sizeDelta = new Vector2(0f, y);
        }

        int BoardCount() => Campaign.SideMissions(director.Save, director.Content).Count + director.Save.campaign.nemeses.Count;

        TextMeshProUGUI AddCardText(Transform parent, string value, float size, Color color, float y, float height)
        {
            var text = UIKit.Text("Text", parent, value, size, color, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.characterSpacing = 0f;
            UIKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(704f, height));
            return text;
        }

        void AddMenuButton(string label, string icon, ref float y, Action action, bool enabled, Color color)
        {
            var button = UIKit.Button(label, safe, label, 30f, color, action);
            UIKit.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, y), new Vector2(460f, 86f));
            button.Label.rectTransform.offsetMin = new Vector2(70f, 4f);
            button.Label.enableAutoSizing = true;
            button.Label.fontSizeMin = 18f;
            button.Label.fontSizeMax = 30f;
            button.Interactable = enabled;
            var image = UIKit.Icon("Icon", button.transform, icon);
            UIKit.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), Vector2.one * 66f);
            y += 96f;
        }

        // A frame late, once the camera has settled, because name tags are placed from where it sees crew.
        IEnumerator BuildCrew()
        {
            var crew = director.Save.crew;
            var models = new List<CharacterModel>();
            var onDeck = Mathf.Min(crew.Count, MaxCrewOnDeck);
            for (var i = 0; i < onDeck; i++)
            {
                var member = director.Content.Crew(crew[i].id);
                var model = Models.CreateCharacter(member.name, member.look, deck.Root.transform);

                // Named crew have their own places on the deck, and everyone else gathers round.
                var station = KitArt.Socket(deck.Kit, crew[i].id == Campaign.Protagonist ? "station_helm" : "station_" + crew[i].id);
                var spare = KitArt.Socket(deck.Kit, "party_slot_" + Mathf.Clamp(i, 1, 4));
                var spot = station != null ? station : spare;
                model.Root.transform.position = spot != null ? spot.position : deck.Root.transform.position;
                if (crew[i].id == Campaign.Protagonist) model.Root.transform.position += HelmStep;
                if (DeckSpots.TryGetValue(crew[i].id, out var deckSpot))
                {
                    model.Root.transform.position = new Vector3(deckSpot.x, model.Root.transform.position.y, deckSpot.y);
                }
                var toDeck = new Vector3(0f, 0f, -12f) - model.Root.transform.position;
                toDeck.y = 0f;
                model.Root.transform.rotation = Quaternion.LookRotation(toDeck.sqrMagnitude > 0.01f ? toDeck : Vector3.back);
                if (model.Companion != null) model.Companion.Root.transform.position = model.Root.transform.position + CompanionHubStep;
                var toCamera = new Vector3(0f, 0f, -12f) - model.Root.transform.position;
                toCamera.y = 0f;
                model.Root.transform.rotation = Quaternion.LookRotation(toCamera.sqrMagnitude > 0.01f ? toCamera : Vector3.back);
                model.Animation.Loop(crew[i].id == Campaign.Protagonist ? "Idle" : "Work", i * 0.37f);
                models.Add(model);
                modelByCrew[crew[i].id] = model;
            }
            AddSetDressing();
            FrameCrew(models);
            yield return null;
            yield return null;

            var camera = director.Stage.MainCamera;
            var scale = UIKit.ReferenceHeight / Mathf.Max(1f, Screen.height);
            for (var i = 0; i < onDeck; i++)
            {
                var progress = crew[i];
                var talks = Campaign.AvailableTalks(director.Save, director.Content, progress.id);
                if (talks.Count == 0 && progress.id == Campaign.Protagonist) continue;
                var world = models[i].Root.transform.position;
                var screen = camera.WorldToScreenPoint(world);
                var member = director.Content.Crew(progress.id);
                var label = member.name;
                var tag = UIKit.Button("Crew Tag", canvas.transform, label, 26f, talks.Count > 0 ? Palette.WithAlpha(Palette.PlayerPaint, 0.95f) : Palette.WithAlpha(Palette.Panel, 0.8f), () => Talk(progress.id));
                tag.Label.fontStyle = FontStyles.Normal;
                tag.Label.characterSpacing = 0f;
                var rect = (RectTransform)tag.transform;
                var width = Mathf.Clamp(label.Length * 15f + 40f, 120f, 220f);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                var tagX = Mathf.Clamp(screen.x * scale, ClearLeftEdge(scale) + width * 0.5f, ClearRightEdge(scale) - width * 0.5f);
                rect.anchoredPosition = new Vector2(tagX, screen.y * scale - 34f - (i % 2) * TagStagger);
                rect.sizeDelta = new Vector2(width, 58f);
                tags.Add(tag.gameObject);
            }
        }

        // Canvas units from the left of the screen. The menu column fills everything right of the card's edge.
        static float ClearLeftEdge(float scale) => Screen.safeArea.xMin * scale + ClearMargin;
        static float ClearRightEdge(float scale) => Screen.safeArea.xMax * scale - CardMargin - CardWidth - ClearMargin;

        // Fits everyone on deck into the part of this phone's screen the menus leave clear.
        void FrameCrew(List<CharacterModel> models)
        {
            var points = new List<HubPoint>();
            foreach (var model in models)
            {
                var at = model.Root.transform.position;
                points.Add(new HubPoint(at.x - PersonHalfWidth, at.y, at.z));
                points.Add(new HubPoint(at.x + PersonHalfWidth, at.y + PersonHeight, at.z));
            }
            var scale = UIKit.ReferenceHeight / Mathf.Max(1f, Screen.height);
            var canvasWidth = Screen.width * scale;
            var area = new HubClearArea(ClearLeftEdge(scale) / canvasWidth, ClearRightEdge(scale) / canvasWidth, HubFraming.ClearBottom, HubFraming.ClearTop);
            var frame = HubFraming.Fit(points, Screen.width / Mathf.Max(1f, (float)Screen.height), HubFov, area);
            cameraLook.position = new Vector3(frame.LookX, frame.LookY, frame.LookZ);
            director.Stage.FrameTarget(cameraRig, cameraLook, new Vector3(frame.CameraX, frame.CameraY, frame.CameraZ), HubFov, 0f);
        }

        void AddSetDressing()
        {
            if (dressed) return;
            dressed = true;
            var faction = KitArt.Crimson;
            PropAt("SM_Prop_HelmWheel", faction, "station_helm", new Vector3(0f, 0f, -0.9f), 1f);
            PropAt("SM_Prop_SkyChartTable", faction, "station_lyra_table", Vector3.zero, 1f);
            PropAt("SM_Prop_BountyBoard", faction, "station_bounty_board", Vector3.zero, 1f);
            PropAt("SM_Prop_HangingLantern", faction, "lantern_0", Vector3.zero, 1f);
            PropAt("SM_Prop_HangingLantern", faction, "lantern_1", Vector3.zero, 1f);
            PropAt("SM_Prop_AetherComm", faction, "station_thorne", new Vector3(1.4f, 0f, -0.6f), 1f);
            PropAt("SM_Prop_RopeCoil", faction, "station_sable", new Vector3(1.6f, 0f, 0.8f), 1f);
            PropAt("SM_Prop_Cannon", faction, "station_rook", new Vector3(-1.2f, 0f, 0f), 1.1f);
            PropAt("SM_Prop_WeaponModule", faction, "station_rook", new Vector3(1f, 0f, 0.8f), 1f);
            PropAt("SM_Prop_LiftCore", faction, "station_thorne", new Vector3(1.4f, 0f, 0.8f), 0.6f);
            PropAt("SM_Prop_RepairKit", faction, "station_thorne", new Vector3(-1.2f, 0f, 0.6f), 1f);
            PropAt("SM_Prop_SaboteurCharge", faction, "station_sable", new Vector3(-0.9f, 0f, 0.5f), 1f);
        }

        void PropAt(string asset, string faction, string socket, Vector3 offset, float scale)
        {
            var mark = KitArt.Socket(deck.Kit, socket);
            if (mark == null) return;
            var prop = Models.CreateProp(asset, faction, deck.Root.transform);
            prop.transform.position = mark.position + offset;
            prop.transform.localScale = Vector3.one * scale;
        }

        void Update()
        {
            time += Time.deltaTime;
            deck.Root.transform.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.6f) * 0.5f, 0f, Mathf.Sin(time * 0.45f) * 0.8f);
        }

        void Talk(string crewId)
        {
            var talks = Campaign.AvailableTalks(director.Save, director.Content, crewId);
            if (talks.Count == 0)
            {
                AudioBank.Play(Sfx.Denied, 0.6f);
                return;
            }
            var talk = talks[0];
            if (overlay != null) Destroy(overlay);
            if (modelByCrew.TryGetValue(crewId, out var talker)) talker.Animation.Loop("Talk");
            var title = $"{director.Content.Crew(crewId).name}, {talk.activity}";
            var comic = ComicPanel.Create(canvas.transform, director.Content.campaign, title, talk.lines, () => FinishTalk(talk));
            overlay = comic.gameObject;
        }

        void FinishTalk(TalkDef talk)
        {
            var perk = Campaign.CompleteTalk(director.Save, director.Content, talk);
            director.SaveGame();
            if (overlay != null) Destroy(overlay);
            var loyalty = Campaign.Loyalty(director.Save, talk.crew);
            var name = director.Content.Crew(talk.crew).name;
            if (perk != null) director.ScreenFx.Milestone($"{perk.name}: {perk.description}", Palette.Accent);
            else director.ScreenFx.Milestone($"{name}'s loyalty: {loyalty}", Palette.Heal);
            foreach (var tag in tags) Destroy(tag);
            tags.Clear();
            RefreshTags();
        }

        void RefreshTags()
        {
            foreach (var model in modelByCrew.Values) Destroy(model.Root);
            modelByCrew.Clear();
            StartCoroutine(BuildCrew());
        }

        void ToggleSettings()
        {
            if (settings != null)
            {
                Destroy(settings);
                return;
            }
            settings = SettingsPanel.Build(canvas.transform, director, () => settings = null);
        }

        GameObject BuildOverlay(string title, out RectTransform panelRect)
        {
            if (overlay != null) Destroy(overlay);
            var scrim = UIKit.Solid("Overlay", canvas.transform, Palette.WithAlpha(Palette.Ink, 0.7f), true);
            UIKit.Fill(scrim.rectTransform);
            overlay = scrim.gameObject;
            var panel = UIKit.Panel("Panel", scrim.transform, Palette.Panel);
            UIKit.Place(panel.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(1500f, 960f));
            panelRect = panel.rectTransform;
            var heading = UIKit.Text("Title", panel.transform, title, 58f, Palette.Accent);
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1200f, 80f));
            var close = UIKit.Button("Close", panel.transform, "", 34f, Palette.PanelLight, CloseOverlay);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(90f, 80f));
            var closeIcon = UIKit.Icon("Close Icon", close.transform, "Hud_Close");
            UIKit.Fill(closeIcon.rectTransform);
            closeIcon.rectTransform.offsetMin = Vector2.one * 12f;
            closeIcon.rectTransform.offsetMax = Vector2.one * -12f;
            return scrim.gameObject;
        }

        void CloseOverlay()
        {
            if (overlay != null) Destroy(overlay);
            RefreshHeader();
        }

        void ShowBoard()
        {
            BuildOverlay("BOUNTY BOARD", out var panel);
            CampaignLook.ScrollList(panel, new Vector2(1440f, 830f), new Vector2(0f, 24f), new Vector2(0.5f, 0f), out var list);
            ((RectTransform)list.parent).pivot = new Vector2(0.5f, 0f);
            var y = 0f;
            foreach (var nemesis in director.Save.campaign.nemeses)
            {
                var mission = Campaign.BountyMission(director.Content, nemesis);
                AddBoardRow(list, ref y, "Story_Nemesis", $"WANTED: {nemesis.name}", $"Survived you {nemesis.stacks} time(s). Heavier armor each time. Bounty: {mission.gold} gold.", Palette.Danger, () => director.PlayMission(mission, 0));
            }
            foreach (var mission in Campaign.SideMissions(director.Save, director.Content))
            {
                var chosen = mission;
                AddBoardRow(list, ref y, "Story_Loyalty", mission.title, mission.blurb, Palette.PlayerPaint, () => director.PlayMission(chosen, 0));
            }
            if (y <= 0f)
            {
                var empty = UIKit.Text("Empty", list, "No bounties posted. Talk to your crew, or let a captain get away.", 34f, Palette.TextMuted);
                UIKit.Place(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1400f, 60f));
            }
            list.sizeDelta = new Vector2(0f, y);
        }

        void AddBoardRow(RectTransform list, ref float y, string icon, string title, string detail, Color color, Action play)
        {
            var row = UIKit.Panel(title, list, Palette.WithAlpha(Palette.PanelLight, 0.8f));
            UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1400f, 138f));
            var image = UIKit.Icon("Icon", row.transform, icon);
            UIKit.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), Vector2.one * 110f);
            var name = UIKit.Text("Name", row.transform, title, 38f, Palette.Accent, TextAlignmentOptions.Left);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -10f), new Vector2(960f, 50f));
            var text = UIKit.Text("Detail", row.transform, detail, 27f, Palette.TextLight, TextAlignmentOptions.TopLeft, false);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.characterSpacing = 0f;
            UIKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -60f), new Vector2(960f, 70f));
            var go = UIKit.Button("Hunt", row.transform, "SAIL", 36f, color, play);
            go.ClickSound = Sfx.Confirm;
            UIKit.Place((RectTransform)go.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(230f, 104f));
            y += 150f;
        }

        void ShowFleet()
        {
            BuildOverlay("FLEET", out var panel);
            var save = director.Save;
            var content = director.Content;
            CampaignLook.ScrollList(panel, new Vector2(1440f, 830f), new Vector2(0f, 24f), new Vector2(0.5f, 0f), out var list);
            ((RectTransform)list.parent).pivot = new Vector2(0.5f, 0f);
            var y = 0f;
            foreach (var entry in save.fleet)
            {
                var progress = entry;
                var ship = content.Ship(progress.id);
                var loadout = Encounter.PlayerLoadout(content, save, progress.id);
                var active = save.activeShip == progress.id;
                var row = UIKit.Panel(ship.name, list, Palette.WithAlpha(active ? Palette.PlayerPaint : Palette.PanelLight, 0.8f));
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1400f, 150f));
                var shipIcon = UIKit.Icon("Ship Icon", row.transform, ship.icon);
                UIKit.Place(shipIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), Vector2.one * 130f);
                var name = UIKit.Text("Name", row.transform, active ? $"{ship.name}  (FLYING)" : ship.name, 34f, Palette.Accent, TextAlignmentOptions.Left);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -8f), new Vector2(760f, 46f));
                var info = UIKit.Text("Stats", row.transform, $"Hull {Mathf.RoundToInt(loadout.Hull - progress.damage)}/{Mathf.RoundToInt(loadout.Hull)}   Armor {loadout.Armor:0}   Speed {loadout.Speed:0}   Turn {loadout.TurnDegrees:0}\n{ship.description}", 25f, Palette.TextLight, TextAlignmentOptions.TopLeft, false);
                info.textWrappingMode = TextWrappingModes.Normal;
                info.characterSpacing = 0f;
                UIKit.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -56f), new Vector2(760f, 90f));

                var cost = Campaign.RepairCost(content, save, progress.id);
                var repair = UIKit.Button("Repair", row.transform, cost > 0 ? $"REPAIR {cost}G" : "REPAIRED", 26f, Palette.Herb, () =>
                {
                    if (Campaign.Repair(content, save, progress.id)) director.SaveGame();
                    ShowFleet();
                });
                repair.Interactable = cost > 0 && save.gold > 0;
                UIKit.Place((RectTransform)repair.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(230f, 104f));
                var fly = UIKit.Button("Fly", row.transform, "FLY", 34f, Palette.PanelLight, () =>
                {
                    save.activeShip = progress.id;
                    director.SaveGame();
                    director.ShowHub();
                });
                fly.Interactable = !active;
                UIKit.Place((RectTransform)fly.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(220f, 104f));
                y += 162f;
            }

            foreach (var id in save.campaign.signatures)
            {
                var signature = content.Signature(id);
                var equipped = save.campaign.equippedSignature == id;
                var row = UIKit.Panel(signature.name, list, Palette.WithAlpha(equipped ? Palette.PlayerPaint : Palette.PanelLight, 0.65f));
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1400f, 92f));
                var signatureIcon = UIKit.Icon("Weapon Icon", row.transform, signature.kind == "harpoon" ? "Module_HarpoonWinch" : signature.kind == "afterburner" ? "Module_Afterburner" : signature.kind == "ram" ? "Ship_Bastion" : "Module_SiegeCannon");
                UIKit.Place(signatureIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), Vector2.one * 76f);
                var text = UIKit.Text("Text", row.transform, $"{signature.name}: {signature.description}", 27f, Palette.TextLight, TextAlignmentOptions.Left, false);
                text.characterSpacing = 0f;
                UIKit.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(1040f, 80f));
                var equip = UIKit.Button("Equip", row.transform, equipped ? "EQUIPPED" : "EQUIP", 26f, Palette.PanelLight, () =>
                {
                    save.campaign.equippedSignature = id;
                    director.SaveGame();
                    ShowFleet();
                });
                equip.Interactable = !equipped;
                UIKit.Place((RectTransform)equip.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(220f, 76f));
                y += 102f;
            }

            foreach (var id in save.campaign.modules)
            {
                var module = content.Module(id);
                var moduleIcon = UIKit.Icon("Module Icon", list, "Item_WeaponModule");
                UIKit.Place(moduleIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-660f, -y), Vector2.one * 42f);
                var text = UIKit.Text("Module", list, $"{module.name}: {module.description}", 27f, Palette.TextMuted, TextAlignmentOptions.Left, false);
                text.characterSpacing = 0f;
                UIKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(30f, -y), new Vector2(1300f, 44f));
                y += 48f;
            }
            list.sizeDelta = new Vector2(0f, y);
        }

        // Automated runs talk to everyone, patch the ship, spend gold, then take the next mission.
        IEnumerator AutoPlay()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            var save = director.Save;
            var content = director.Content;
            foreach (var member in save.crew)
            {
                foreach (var talk in Campaign.AvailableTalks(save, content, member.id)) Campaign.CompleteTalk(save, content, talk);
            }
            Campaign.Repair(content, save, save.activeShip);
            foreach (var upgrade in content.upgrades)
            {
                if (save.gold > 60 && Shop.Buy(save, upgrade)) Debug.Log($"[Flow] Autoplay bought {upgrade.name}");
            }
            director.SaveGame();

            var side = Campaign.SideMissions(save, content);
            if (director.AutoplaySideMissions && side.Count > 0)
            {
                director.PlayMission(side[0], 0);
                yield break;
            }
            var next = Campaign.NextStoryMission(save, content);
            if (next != null) director.PlayMission(next, 0);
            else Debug.Log("[Flow] Campaign complete");
        }
    }
}
