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
    public class TitleScreen : MonoBehaviour, IPointerClickHandler
    {
        TextMeshProUGUI prompt;
        float time;
        bool leaving;

        void Start()
        {
            var director = GameDirector.Instance;
            OrbitBackdrop.Create(transform, director.Stage, new Vector3(0f, 7f, -62f), new Vector3(22f, 6f, 0f), 42f);

            var canvas = UIKit.CreateCanvas("Title", transform, 10);
            var safe = UIKit.SafeArea(canvas.transform);

            // A full-screen tap catcher, routed to this component.
            var catcher = UIKit.Solid("Tap", canvas.transform, Color.clear, true);
            UIKit.Fill(catcher.rectTransform);
            catcher.gameObject.AddComponent<TapRelay>().Target = this;

            var scrim = UIKit.Panel("Title Scrim", safe, Palette.WithAlpha(Palette.Ink, 0.72f));
            UIKit.Place(scrim.rectTransform, new Vector2(0.73f, 0.6f), Vector2.one * 0.5f, Vector2.zero, new Vector2(860f, 380f));

            var title = UIKit.Text("Game Name", scrim.transform, "RANSOM\nSKIES", 150f, Palette.Accent);
            UIKit.Fill(title.rectTransform);
            title.characterSpacing = 8f;
            title.lineSpacing = -20f;
            title.outlineWidth = 0.18f;
            title.outlineColor = Palette.Ink;

            prompt = UIKit.Text("Prompt", safe, "TAP TO SET SAIL", 40f, Palette.TextLight);
            prompt.characterSpacing = 14f;
            prompt.outlineWidth = 0.2f;
            prompt.outlineColor = Palette.Ink;
            UIKit.Place(prompt.rectTransform, new Vector2(0.73f, 0.33f), Vector2.one * 0.5f, Vector2.zero, new Vector2(880f, 80f));

            if (GameDirector.Autoplay) StartCoroutine(AutoLeave());
        }

        IEnumerator AutoLeave()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            Leave();
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            prompt.alpha = 0.75f + 0.25f * Mathf.Sin(time * Mathf.PI * 2f / 1.6f);
        }

        public void OnPointerClick(PointerEventData eventData) => Leave();

        void Leave()
        {
            if (leaving) return;
            leaving = true;
            AudioBank.Play(Sfx.Confirm);
            GameDirector.Instance.LeaveTitle();
        }
    }

    public class TapRelay : MonoBehaviour, IPointerClickHandler
    {
        public IPointerClickHandler Target;
        public void OnPointerClick(PointerEventData eventData) => Target.OnPointerClick(eventData);
    }

    public class PortScreen : MonoBehaviour
    {
        GameDirector director;
        TextMeshProUGUI goldText;
        readonly List<Action> refreshers = new List<Action>();
        GameObject settingsPanel;
        Canvas canvas;

        void Start()
        {
            director = GameDirector.Instance;
            OrbitBackdrop.Create(transform, director.Stage, new Vector3(0f, 6f, -58f), new Vector3(0f, 5f, 0f), 40f);

            canvas = UIKit.CreateCanvas("Port", transform, 10);
            var safe = UIKit.SafeArea(canvas.transform);

            BuildHeader(safe);
            BuildCrew(safe);
            BuildShop(safe);
            BuildChandler(safe);

            var sail = UIKit.Button("Free Skies", safe, "FREE SKIES", 50f, Palette.PlayerPaint, SetSail);
            sail.ClickSound = Sfx.Confirm;
            UIKit.Place((RectTransform)sail.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(520f, 130f));
            var back = UIKit.Button("Back", safe, "QUARTERDECK", 34f, Palette.PanelLight, director.ShowHub);
            UIKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(400f, 86f));

            Refresh();
            if (GameDirector.Autoplay) StartCoroutine(AutoPlay());
        }

        IEnumerator AutoPlay()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            foreach (var upgrade in director.Content.upgrades)
            {
                if (Shop.Buy(director.Save, upgrade)) Debug.Log($"[Flow] Autoplay bought {upgrade.name}");
            }
            director.SaveGame();
            Refresh();
            yield return new WaitForSecondsRealtime(0.4f);
            director.ShowHub();
        }

        void SetSail()
        {
            director.SaveGame();
            director.StartDogfight();
        }

        void BuildHeader(RectTransform safe)
        {
            var gold = UIKit.Panel("Gold", safe, Palette.WithAlpha(Palette.Panel, 0.92f));
            UIKit.Place(gold.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(400f, 86f));
            goldText = UIKit.Text("Amount", gold.transform, "", 44f, Palette.Accent);
            UIKit.Fill(goldText.rectTransform);

            var settings = UIKit.Button("Settings", safe, "SETTINGS", 30f, Palette.PanelLight, ToggleSettings);
            UIKit.Place((RectTransform)settings.transform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(218f, -22f), new Vector2(220f, 86f));
        }

        void BuildCrew(RectTransform safe)
        {
            var panel = UIKit.Panel("Crew", safe, Palette.WithAlpha(Palette.Panel, 0.92f));
            UIKit.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 30f), new Vector2(600f, 860f));
            var heading = UIKit.Text("Heading", panel.transform, "CREW", 40f, Palette.Accent);
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(540f, 56f));

            var rows = director.Save.crew;
            CampaignLook.ScrollList(panel.transform, new Vector2(580f, 770f), new Vector2(0f, -82f), new Vector2(0.5f, 1f), out var crewList);
            ((RectTransform)crewList.parent).pivot = new Vector2(0.5f, 1f);
            crewList.sizeDelta = new Vector2(0f, rows.Count * 188f);
            for (var i = 0; i < rows.Count; i++)
            {
                var progress = rows[i];
                var member = Array.Find(director.Content.crew, c => c.id == progress.id);
                var cls = director.Content.Class(member.classId);
                var row = UIKit.Panel(member.name, crewList, Palette.WithAlpha(Palette.PanelLight, 0.7f));
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f - i * 188f), new Vector2(560f, 176f));

                var name = UIKit.Text("Name", row.transform, member.name, 34f, Palette.TextLight, TextAlignmentOptions.Left);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(380f, 44f));
                var level = UIKit.Text("Level", row.transform, "", 34f, Palette.Accent, TextAlignmentOptions.Right);
                UIKit.Place(level.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -10f), new Vector2(150f, 44f));
                var look = director.Content.Look(member.look);
                var weapon = look != null ? (!string.IsNullOrEmpty(look.weaponR) ? look.weaponR : look.gunR) : null;
                var weaponIcon = UIKit.Icon("Weapon", row.transform, null);
                weaponIcon.sprite = KitArt.WeaponIcon(weapon);
                weaponIcon.enabled = weaponIcon.sprite != null;
                UIKit.Place(weaponIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, -4f), Vector2.one * 58f);
                var role = UIKit.Text("Class", row.transform, cls.name, 26f, Palette.TextMuted, TextAlignmentOptions.Left, false);
                UIKit.Place(role.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -54f), new Vector2(520f, 34f));

                var stats = UIKit.Text("Stats", row.transform, "", 24f, Palette.TextLight, TextAlignmentOptions.Left, false);
                UIKit.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -90f), new Vector2(530f, 34f));
                stats.characterSpacing = 0f;

                var xpBar = UIKit.Bar("XP", row.transform, Palette.Accent);
                UIKit.Place((RectTransform)xpBar.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(520f, 22f));

                // The captain always sails. Anyone else can trade places with whoever is in reserve.
                GameButton swap = null;
                if (i > 0)
                {
                    swap = UIKit.Button("Swap", row.transform, "SWAP", 24f, Palette.PanelLight, () =>
                    {
                        if (!director.Save.SwapOut(progress.id)) return;
                        director.SaveGame();
                        Refresh();
                    });
                    UIKit.Place((RectTransform)swap.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-120f, -8f), new Vector2(150f, 48f));
                }

                var rowColor = row.color;
                refreshers.Add(() =>
                {
                    var active = director.Save.IsInParty(progress.id);
                    row.color = active ? rowColor : Palette.WithAlpha(Palette.Panel, 0.8f);
                    role.text = active ? cls.name : $"{cls.name}  (in reserve)";
                    if (swap != null)
                    {
                        swap.gameObject.SetActive(active);
                    }
                    var s = Growth.StatsAtLevel(member.baseStats, cls, progress.level, director.Content.economy.baseStatGrowthInfluence);
                    level.text = $"LV {progress.level}";
                    stats.text = $"HP {s.hp}  MP {s.mp}  ATK {s.attack}  DEF {s.defense}  SPI {s.spirit}  SPD {s.speed}";
                    xpBar.Set(progress.xp / (float)Growth.XpToNext(director.Content.economy, progress.level));
                });
            }
        }

        void BuildShop(RectTransform safe)
        {
            var panel = UIKit.Panel("Shipwright", safe, Palette.WithAlpha(Palette.Panel, 0.92f));
            UIKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 30f), new Vector2(660f, 860f));
            var heading = UIKit.Text("Heading", panel.transform, "SHIPWRIGHT", 40f, Palette.Accent);
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(600f, 56f));

            var upgrades = director.Content.upgrades;
            for (var i = 0; i < upgrades.Length; i++)
            {
                var upgrade = upgrades[i];
                var row = UIKit.Panel(upgrade.name, panel.transform, Palette.WithAlpha(Palette.PanelLight, 0.7f));
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f - i * 126f), new Vector2(620f, 116f));

                var name = UIKit.Text("Name", row.transform, upgrade.name, 30f, Palette.TextLight, TextAlignmentOptions.Left);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(380f, 40f));
                var description = UIKit.Text("Description", row.transform, upgrade.description, 22f, Palette.TextMuted, TextAlignmentOptions.Left, false);
                UIKit.Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -50f), new Vector2(390f, 28f));
                description.characterSpacing = 0f;
                var level = UIKit.Text("Level", row.transform, "", 22f, Palette.Accent, TextAlignmentOptions.Left);
                UIKit.Place(level.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -80f), new Vector2(380f, 28f));

                var buy = UIKit.Button("Buy", row.transform, "", 28f, Palette.PlayerPaint, () => Buy(upgrade));
                UIKit.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(190f, 88f));
                buy.ClickSound = Sfx.Confirm;

                refreshers.Add(() =>
                {
                    var current = director.Save.UpgradeLevel(upgrade.id);
                    level.text = $"LEVEL {current} / {upgrade.maxLevel}";
                    var maxed = current >= upgrade.maxLevel;
                    buy.Label.text = maxed ? "MAX" : $"{Shop.Cost(upgrade, current)} G";
                    buy.Interactable = Shop.CanBuy(director.Save, upgrade);
                });
            }
        }

        // Consumables for the fights ahead.
        void BuildChandler(RectTransform safe)
        {
            var items = director.Content.items;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var row = UIKit.Panel(item.name, safe, Palette.WithAlpha(Palette.Panel, 0.92f));
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-98f + i * 98f, -118f), new Vector2(92f, 142f));
                var icon = UIKit.Icon("Icon", row.transform, item.icon);
                UIKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), Vector2.one * 66f);
                var count = UIKit.Text("Count", row.transform, "", 22f, Palette.Accent);
                UIKit.Place(count.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(90f, 26f));
                var buy = UIKit.Button("Buy Item", row.transform, $"{item.cost}G", 20f, Palette.PlayerPaint, () =>
                {
                    if (!director.Save.BuyItem(item)) return;
                    director.SaveGame();
                    Refresh();
                });
                buy.Label.characterSpacing = 0f;
                buy.Label.rectTransform.offsetMin = new Vector2(2f, 2f);
                buy.Label.rectTransform.offsetMax = new Vector2(-2f, -2f);
                UIKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(84f, 44f));
                refreshers.Add(() =>
                {
                    count.text = $"x{director.Save.ItemCount(item.id)}";
                    buy.Interactable = director.Save.gold >= item.cost;
                });
            }
        }

        void Buy(UpgradeDef upgrade)
        {
            if (!Shop.Buy(director.Save, upgrade)) return;
            director.SaveGame();
            director.ScreenFx.Flash(Palette.Accent, 0.22f);
            Refresh();
        }

        void Refresh()
        {
            goldText.text = $"{director.Save.gold} GOLD";
            foreach (var refresh in refreshers) refresh();
        }

        void ToggleSettings()
        {
            if (settingsPanel != null)
            {
                Destroy(settingsPanel);
                return;
            }
            settingsPanel = SettingsPanel.Build(canvas.transform, director, () => settingsPanel = null);
        }
    }

    public static class SettingsPanel
    {
        public static GameObject Build(Transform parent, GameDirector director, Action onClosed)
        {
            var scrim = UIKit.Solid("Settings", parent, Palette.WithAlpha(Palette.Ink, 0.72f), true);
            UIKit.Fill(scrim.rectTransform);

            var panel = UIKit.Panel("Panel", scrim.transform, Palette.Panel);
            UIKit.Place(panel.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(860f, 660f));
            var title = UIKit.Text("Title", panel.transform, "SETTINGS", 54f, Palette.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(760f, 70f));

            var settings = director.Save.settings;
            AddSlider(panel.transform, "Music", -130f, settings.musicVolume, value =>
            {
                settings.musicVolume = value;
                AudioBank.SetVolumes(settings.musicVolume, settings.sfxVolume);
            });
            AddSlider(panel.transform, "Sound Effects", -250f, settings.sfxVolume, value =>
            {
                settings.sfxVolume = value;
                AudioBank.SetVolumes(settings.musicVolume, settings.sfxVolume);
            });

            GameButton graphics = null;
            graphics = UIKit.Button("Graphics", panel.transform, "", 34f, Palette.PanelLight, () =>
            {
                settings.highDefinition = !settings.highDefinition;
                director.ApplySettings();
                graphics.Label.text = GraphicsLabel(settings.highDefinition);
            });
            graphics.Label.text = GraphicsLabel(settings.highDefinition);
            UIKit.Place((RectTransform)graphics.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -375f), new Vector2(760f, 100f));

            var close = UIKit.Button("Close", panel.transform, "CLOSE", 38f, Palette.PlayerPaint, () =>
            {
                director.SaveGame();
                onClosed?.Invoke();
                UnityEngine.Object.Destroy(scrim.gameObject);
            });
            UIKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(380f, 104f));
            return scrim.gameObject;
        }

        static string GraphicsLabel(bool high) => high ? "GRAPHICS: HIGH DEFINITION" : "GRAPHICS: LOW DEFINITION";

        static void AddSlider(Transform parent, string label, float y, float value, Action<float> changed)
        {
            var text = UIKit.Text(label, parent, label, 32f, Palette.TextLight, TextAlignmentOptions.Left);
            UIKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 42f));

            var root = UIKit.Node(label + " Slider", parent);
            UIKit.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 56f), new Vector2(760f, 50f));
            var track = UIKit.Panel("Track", root, Palette.Ink, true);
            UIKit.Fill(track.rectTransform);
            track.rectTransform.offsetMin = new Vector2(0f, 12f);
            track.rectTransform.offsetMax = new Vector2(0f, -12f);

            var fillArea = UIKit.Stretch("Fill Area", root);
            fillArea.offsetMin = new Vector2(0f, 12f);
            fillArea.offsetMax = new Vector2(0f, -12f);
            var fill = UIKit.Panel("Fill", fillArea, Palette.Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = UIKit.Stretch("Handle Area", root);
            var handle = new GameObject("Handle", typeof(RectTransform)).AddComponent<Image>();
            handle.transform.SetParent(handleArea, false);
            handle.sprite = UIKit.Circle;
            handle.color = Palette.TextLight;
            handle.rectTransform.sizeDelta = new Vector2(50f, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(v => changed(v));
        }
    }

    public class ResultsScreen : MonoBehaviour
    {
        string title;
        Color color;
        List<string> lines;
        bool hadLevelUp;
        Action retry;
        Action abandon;

        public void Setup(string resultTitle, Color resultColor, List<string> resultLines, bool levelUp)
        {
            title = resultTitle;
            color = resultColor;
            lines = resultLines;
            hadLevelUp = levelUp;
        }

        // A failed mission phase offers another go, from its checkpoint.
        public void SetupRetry(Action onRetry, Action onAbandon)
        {
            retry = onRetry;
            abandon = onAbandon;
        }

        void Start()
        {
            var director = GameDirector.Instance;
            OrbitBackdrop.Create(transform, director.Stage, new Vector3(0f, 8f, -62f), new Vector3(24f, 6f, 0f), 40f);

            var canvas = UIKit.CreateCanvas("Results", transform, 10);
            var safe = UIKit.SafeArea(canvas.transform);

            var panel = UIKit.Panel("Summary", safe, Palette.WithAlpha(Palette.Panel, 0.94f));
            UIKit.Place(panel.rectTransform, new Vector2(0.72f, 0.5f), Vector2.one * 0.5f, Vector2.zero, new Vector2(960f, 780f));

            var heading = UIKit.Text("Heading", panel.transform, title, 88f, color);
            heading.outlineWidth = 0.18f;
            heading.outlineColor = Palette.Ink;
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(880f, 110f));

            for (var i = 0; i < lines.Count; i++)
            {
                var isLevelUp = lines[i].EndsWith("!");
                var line = UIKit.Text("Line", panel.transform, lines[i], 38f, isLevelUp ? Palette.Accent : Palette.TextLight);
                UIKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -176f - i * 62f), new Vector2(880f, 54f));
                StartCoroutine(RevealLine(line, i * 0.12f));
            }

            var total = UIKit.Text("Gold", panel.transform, $"{director.Save.gold} GOLD", 46f, Palette.Accent);
            UIKit.Place(total.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 156f), new Vector2(880f, 60f));

            if (retry != null)
            {
                var again = UIKit.Button("Retry", panel.transform, "TRY AGAIN", 40f, Palette.PlayerPaint, retry);
                again.ClickSound = Sfx.Confirm;
                UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 30f), new Vector2(430f, 110f));
                var leave = UIKit.Button("Leave", panel.transform, "BACK TO DECK", 36f, Palette.PanelLight, abandon);
                UIKit.Place((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 30f), new Vector2(430f, 110f));
            }
            else
            {
                var next = UIKit.Button("Continue", panel.transform, director.Run != null ? "TO THE QUARTERDECK" : "RETURN TO PORT", 40f, Palette.PlayerPaint, director.FinishResults);
                next.ClickSound = Sfx.Confirm;
                UIKit.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(640f, 110f));
            }

            StartCoroutine(Celebrate(director));
        }

        IEnumerator RevealLine(TextMeshProUGUI line, float delay)
        {
            line.alpha = 0f;
            yield return new WaitForSecondsRealtime(0.35f + delay);
            var rect = line.rectTransform;
            var end = rect.anchoredPosition;
            yield return Ease.Tween(0.25f, t =>
            {
                line.alpha = t;
                rect.anchoredPosition = end + Vector2.left * 40f * (1f - t);
            });
        }

        IEnumerator Celebrate(GameDirector director)
        {
            yield return new WaitForSecondsRealtime(0.45f);
            if (hadLevelUp) director.ScreenFx.Milestone("LEVEL UP!", Palette.Accent);
            if (!GameDirector.Autoplay) yield break;
            yield return new WaitForSecondsRealtime(1.5f);
            if (retry != null) retry();
            else director.FinishResults();
        }
    }
}
