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
    // A story beat told in film shots: moving cameras, ships and people, with captions between letterbox bars.
    // Every shot comes from the campaign content. A shot with words waits for a tap, and SKIP ends the whole scene.
    public class Cutscene : MonoBehaviour, IPointerClickHandler
    {
        const float BarHeight = 150f;
        const float FadeSeconds = 0.3f;
        const float RevealCharactersPerSecond = 55f;
        const float ReadingCharactersPerSecond = 17f;
        const float ReadingPadSeconds = 1.6f;
        const float CardSlideSeconds = 0.35f;
        const float WalkSpeed = 2.6f;
        const float RunSpeed = 6f;
        const float ShipBobHeight = 0.6f;
        const float PropellerDegreesPerSecond = 500f;

        // Automated runs don't read, but a capture tool can slow them down to look at each shot.
        public static float AutoplaySeconds = 0.4f;
        public static int ShotIndex { get; private set; } = -1;
        public static float ShotProgress { get; private set; }

        static readonly HashSet<string> OneShotClips = new HashSet<string> { "AttackMelee", "AttackRanged", "Hit", "Block", "Death", "Surrender", "Taunt" };

        GameDirector director;
        ShotDef[] shots;
        Action finished;
        Transform rig;
        Transform look;
        Transform set;
        readonly List<ShipRun> ships = new List<ShipRun>();
        Image blackout;
        RectTransform card;
        TextMeshProUGUI cardName;
        TextMeshProUGUI cardRole;
        TextMeshProUGUI title;
        TextMeshProUGUI subtitle;
        TextMeshProUGUI speaker;
        TextMeshProUGUI line;
        TextMeshProUGUI tapHint;
        PortraitView portrait;
        float revealed;
        bool advance;
        bool done;

        class ShipRun
        {
            public ShipModel Model;
            public Vector3 From;
            public Vector3 To;
            public float Yaw;
            public float Bob;
        }

        public void Play(ShotDef[] sceneShots, Action onFinished)
        {
            director = GameDirector.Instance;
            shots = sceneShots;
            finished = onFinished;
            rig = new GameObject("Cutscene Camera").transform;
            rig.SetParent(transform, false);
            look = new GameObject("Cutscene Look").transform;
            look.SetParent(transform, false);
            BuildInterface();
            StartCoroutine(RunShots());
        }

        void BuildInterface()
        {
            var canvas = UIKit.CreateCanvas("Cutscene", transform, 10);
            var catcher = UIKit.Solid("Tap Catcher", canvas.transform, Color.clear, true);
            UIKit.Fill(catcher.rectTransform);
            var relay = catcher.gameObject.AddComponent<TapRelay>();
            relay.Tapped = OnTap;

            var top = UIKit.Solid("Top Bar", canvas.transform, Color.black);
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.sizeDelta = new Vector2(0f, BarHeight);
            var bottom = UIKit.Solid("Bottom Bar", canvas.transform, Color.black);
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            bottom.rectTransform.pivot = new Vector2(0.5f, 0f);
            bottom.rectTransform.sizeDelta = new Vector2(0f, BarHeight * 1.5f);

            var safe = UIKit.SafeArea(canvas.transform);
            portrait = PortraitView.Create(safe, 190f);
            UIKit.Place(portrait.Root, Vector2.zero, Vector2.zero, new Vector2(40f, 16f), Vector2.one * 190f);
            speaker = UIKit.Text("Speaker", safe, "", 36f, Palette.Accent, TextAlignmentOptions.Left);
            UIKit.Place(speaker.rectTransform, Vector2.zero, Vector2.zero, new Vector2(250f, 158f), new Vector2(1400f, 50f));
            line = UIKit.Text("Line", safe, "", 40f, Palette.TextLight, TextAlignmentOptions.TopLeft, false);
            line.textWrappingMode = TextWrappingModes.Normal;
            line.characterSpacing = 0f;
            UIKit.Place(line.rectTransform, Vector2.zero, Vector2.zero, new Vector2(250f, 14f), new Vector2(1480f, 144f));

            tapHint = UIKit.Text("Tap Hint", safe, "TAP TO CONTINUE", 26f, Palette.Accent, TextAlignmentOptions.Right);
            UIKit.Place(tapHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 6f), new Vector2(420f, 36f));
            tapHint.enabled = false;

            var skip = UIKit.Button("Skip", safe, "SKIP", 32f, Palette.PanelLight, Finish);
            UIKit.Place((RectTransform)skip.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(180f, 90f));

            var cardPanel = UIKit.Panel("Name Card", safe, Palette.WithAlpha(Palette.Panel, 0.94f));
            card = cardPanel.rectTransform;
            UIKit.Place(card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 90f), new Vector2(640f, 150f));
            cardName = UIKit.Text("Name", card, "", 54f, Palette.Accent, TextAlignmentOptions.Left);
            UIKit.Place(cardName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(600f, 74f));
            cardRole = UIKit.Text("Role", card, "", 30f, Palette.TextLight, TextAlignmentOptions.Left, false);
            cardRole.characterSpacing = 0f;
            UIKit.Place(cardRole.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -86f), new Vector2(600f, 50f));
            card.gameObject.SetActive(false);

            title = UIKit.Text("Title", safe, "", 120f, Palette.Accent);
            title.enableAutoSizing = true;
            title.fontSizeMin = 50f;
            title.fontSizeMax = 120f;
            title.outlineWidth = 0.2f;
            title.outlineColor = Palette.Ink;
            UIKit.Place(title.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0f, 70f), new Vector2(1700f, 170f));
            subtitle = UIKit.Text("Subtitle", safe, "", 44f, Palette.Ink);
            subtitle.outlineWidth = 0.2f;
            subtitle.outlineColor = Palette.Ink;
            UIKit.Place(subtitle.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0f, -40f), new Vector2(1700f, 70f));

            blackout = UIKit.Solid("Blackout", canvas.transform, Color.black);
            UIKit.Fill(blackout.rectTransform);
            skip.transform.SetParent(canvas.transform, true);
        }

        IEnumerator RunShots()
        {
            for (ShotIndex = 0; ShotIndex < shots.Length && !done; ShotIndex++)
            {
                var shot = shots[ShotIndex];
                BuildShot(shot);
                yield return Fade(1f, 0f);
                yield return Hold(shot);
                if (done) yield break;
                yield return Fade(0f, 1f);
                ClearShot();
            }
            ShotIndex = -1;
            Finish();
        }

        IEnumerator Fade(float from, float to)
        {
            for (var t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                SetBlackout(Mathf.Lerp(from, to, t / FadeSeconds));
                yield return null;
            }
            SetBlackout(to);
        }

        void SetBlackout(float alpha) => blackout.color = new Color(0f, 0f, 0f, alpha);

        IEnumerator Hold(ShotDef shot)
        {
            advance = false;
            var text = shot.line ?? "";
            var seconds = Mathf.Max(shot.seconds, text.Length / ReadingCharactersPerSecond + ReadingPadSeconds);
            if (GameDirector.Autoplay) seconds = AutoplaySeconds;

            // Only a shot with nothing to read moves on by itself. The camera finishes its move and holds.
            var waitsForTap = !GameDirector.Autoplay && HasWords(shot);
            for (var t = 0f; (t < seconds || waitsForTap) && !advance && !done; t += Time.unscaledDeltaTime)
            {
                ShotProgress = Mathf.Clamp01(t / seconds);
                MoveShot(shot, ShotProgress);
                revealed += RevealCharactersPerSecond * Time.unscaledDeltaTime;
                line.maxVisibleCharacters = (int)revealed;
                tapHint.enabled = waitsForTap && revealed >= text.Length;
                if (card.gameObject.activeSelf)
                {
                    var slide = Mathf.Clamp01(t / CardSlideSeconds);
                    card.anchoredPosition = new Vector2(Mathf.Lerp(-card.sizeDelta.x, 40f, Ease.OutBack(slide)), card.anchoredPosition.y);
                }
                yield return null;
            }
            tapHint.enabled = false;
        }

        static bool HasWords(ShotDef shot) => !string.IsNullOrEmpty(shot.line) || !string.IsNullOrEmpty(shot.title) || !string.IsNullOrEmpty(shot.subtitle) || !string.IsNullOrEmpty(shot.cardName);

        void OnTap()
        {
            if (done) return;
            var length = shots != null && ShotIndex >= 0 && ShotIndex < shots.Length ? (shots[ShotIndex].line ?? "").Length : 0;
            if (revealed < length)
            {
                revealed = length;
                line.maxVisibleCharacters = length;
                return;
            }
            AudioBank.Play(Sfx.Click);
            advance = true;
        }

        public void OnPointerClick(PointerEventData eventData) => OnTap();

        void BuildShot(ShotDef shot)
        {
            set = new GameObject("Shot").transform;
            set.SetParent(transform, false);
            ships.Clear();
            ShotProgress = 0f;
            if (!string.IsNullOrEmpty(shot.music)) AudioBank.PlayMusic(shot.music);
            AudioBank.SetDanger(shot.danger ? 1f : 0f);
            if (!string.IsNullOrEmpty(shot.weather)) director.Stage.ApplyWeather(director.Content.Weather(shot.weather));

            DeckModel deck = null;
            if (!string.IsNullOrEmpty(shot.arena)) deck = Models.CreateDeck(set, shot.faction, shot.arena);

            if (shot.props != null)
            {
                foreach (var prop in shot.props) PlaceProp(prop, deck);
            }
            if (shot.ships != null)
            {
                foreach (var ship in shot.ships) PlaceShip(ship);
            }
            if (shot.actors != null)
            {
                foreach (var actor in shot.actors) PlaceActor(actor, deck);
            }
            if (shot.effects != null)
            {
                foreach (var effect in shot.effects) StartCoroutineOnSet(PlayEffect(effect));
            }

            var who = string.IsNullOrEmpty(shot.speaker) ? null : director.Content.campaign.Speaker(shot.speaker);
            portrait.Root.gameObject.SetActive(who != null);
            if (who != null) portrait.Show(who, who.name);
            speaker.text = who != null ? who.name : "";
            line.text = shot.line ?? "";
            line.fontStyle = who != null ? FontStyles.Normal : FontStyles.Italic;
            var left = who != null ? 250f : 120f;
            line.rectTransform.anchoredPosition = new Vector2(left, line.rectTransform.anchoredPosition.y);
            line.rectTransform.sizeDelta = new Vector2(who != null ? 1480f : 1680f, line.rectTransform.sizeDelta.y);
            line.maxVisibleCharacters = 0;
            revealed = 0f;

            card.gameObject.SetActive(!string.IsNullOrEmpty(shot.cardName));
            cardName.text = shot.cardName ?? "";
            cardRole.text = shot.cardRole ?? "";
            title.text = shot.title ?? "";
            subtitle.text = shot.subtitle ?? "";

            director.Stage.FrameTarget(rig, look, Vector3.zero, shot.fov > 0f ? shot.fov : 46f, 0f);
            MoveShot(shot, 0f);
        }

        void ClearShot()
        {
            Effects.Clear();
            Fx.Clear();
            if (set != null) Destroy(set.gameObject);
        }

        void MoveShot(ShotDef shot, float progress)
        {
            var eased = Mathf.SmoothStep(0f, 1f, progress);
            rig.position = Vector3.Lerp(ToVector(shot.cam), ToVector(shot.camTo, ToVector(shot.cam)), eased);
            look.position = Vector3.Lerp(ToVector(shot.look), ToVector(shot.lookTo, ToVector(shot.look)), eased);
            foreach (var run in ships)
            {
                run.Bob += Time.unscaledDeltaTime;
                run.Model.Root.transform.position = Vector3.Lerp(run.From, run.To, progress) + Vector3.up * (Mathf.Sin(run.Bob * 0.9f) * ShipBobHeight);
                foreach (var propeller in run.Model.Propellers) propeller.Rotate(0f, 0f, PropellerDegreesPerSecond * Time.unscaledDeltaTime, Space.Self);
            }
        }

        void PlaceProp(ShotProp def, DeckModel deck)
        {
            var prop = Models.CreateProp(def.asset, string.IsNullOrEmpty(def.faction) ? KitArt.Crimson : def.faction, set);
            prop.transform.position = Spot(def.mark, def.at, deck);
            prop.transform.rotation = Quaternion.Euler(0f, def.yaw, 0f);
            prop.transform.localScale = Vector3.one * (def.scale > 0f ? def.scale : 1f);
        }

        void PlaceShip(ShotShip def)
        {
            var model = Models.CreateShip(def.ship, director.Content.Ship(def.ship), set);
            var from = ToVector(def.at);
            ships.Add(new ShipRun { Model = model, From = from, To = ToVector(def.to, from), Yaw = def.yaw, Bob = UnityEngine.Random.value * 6f });
            model.Root.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, def.yaw, 0f));
            model.Body.localRotation = Quaternion.Euler(def.pitch, 0f, def.bank);
            if (!def.burning) return;
            foreach (var preset in new[] { "HullFire", "EmberDrift", "DamageSmoke" })
            {
                foreach (var emitter in Effects.AttachAll(preset, model.Kit, model.Scale)) emitter.Active = true;
            }
        }

        void PlaceActor(ShotActor def, DeckModel deck)
        {
            var model = Models.CreateCharacter(def.look, def.look, set);
            model.Root.transform.position = Spot(def.mark, def.at, deck);
            model.Root.transform.rotation = Quaternion.Euler(0f, def.yaw, 0f);
            model.Animation.Loop("Idle", UnityEngine.Random.value);
            StartCoroutineOnSet(ActOut(def, model, deck));
        }

        // Coroutines run on the shot's own object, so they end with the shot.
        void StartCoroutineOnSet(IEnumerator routine)
        {
            var host = set.GetComponent<ShotHost>();
            if (host == null) host = set.gameObject.AddComponent<ShotHost>();
            host.StartCoroutine(routine);
        }

        IEnumerator ActOut(ShotActor def, CharacterModel model, DeckModel deck)
        {
            if (def.delay > 0f) yield return new WaitForSecondsRealtime(def.delay);
            if (!string.IsNullOrEmpty(def.toMark) || def.to != null && def.to.Length == 3)
            {
                var target = Spot(def.toMark, def.to, deck);
                var running = def.run;
                model.Animation.Loop(running ? "Run" : "Walk", 0f);
                var speed = running ? RunSpeed : WalkSpeed;
                var transformRoot = model.Root.transform;
                while (Vector3.Distance(transformRoot.position, target) > 0.05f)
                {
                    var flat = target - transformRoot.position;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.01f) transformRoot.rotation = Quaternion.Slerp(transformRoot.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                    transformRoot.position = Vector3.MoveTowards(transformRoot.position, target, speed * Time.unscaledDeltaTime);
                    yield return null;
                }
                transformRoot.rotation = Quaternion.Euler(0f, def.endYaw != 0f ? def.endYaw : def.yaw, 0f);
                model.Animation.Loop("Idle", 0f);
            }
            PerformClip(model, def.clip);
            if (string.IsNullOrEmpty(def.clip2)) yield break;
            yield return new WaitForSecondsRealtime(def.clip2Delay);
            PerformClip(model, def.clip2);
        }

        static void PerformClip(CharacterModel model, string clip)
        {
            if (string.IsNullOrEmpty(clip)) return;
            if (OneShotClips.Contains(clip)) model.Animation.Play(clip);
            else model.Animation.Loop(clip, 0f);
        }

        IEnumerator PlayEffect(ShotEffect def)
        {
            if (def.delay > 0f) yield return new WaitForSecondsRealtime(def.delay);
            Effects.Play(def.preset, ToVector(def.at), def.scale > 0f ? def.scale : 1f);
            if (def.shake > 0f) director.Stage.Shake(def.shake);
            if (Enum.TryParse<Sfx>(def.sfx, out var sound)) AudioBank.Play(sound, 1f, 0.05f);
        }

        static Vector3 Spot(string mark, float[] at, DeckModel deck)
        {
            var offset = ToVector(at);
            if (string.IsNullOrEmpty(mark) || deck == null) return offset;
            var socket = KitArt.Socket(deck.Kit, mark);
            return socket != null ? socket.position + offset : offset;
        }

        static Vector3 ToVector(float[] value) => ToVector(value, Vector3.zero);

        static Vector3 ToVector(float[] value, Vector3 fallback) => value != null && value.Length == 3 ? new Vector3(value[0], value[1], value[2]) : fallback;

        void Finish()
        {
            if (done) return;
            done = true;
            ShotIndex = -1;
            AudioBank.SetDanger(0f);
            ClearShot();
            finished?.Invoke();
        }

        class ShotHost : MonoBehaviour
        {
        }

        class TapRelay : MonoBehaviour, IPointerClickHandler
        {
            public Action Tapped;

            public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke();
        }
    }
}
