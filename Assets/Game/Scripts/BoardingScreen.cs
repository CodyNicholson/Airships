using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Airships.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Airships.Game
{
    // The turn-based party battle on a ship's deck. All rules live in PartyBattle.
    public class BoardingScreen : MonoBehaviour
    {
        const float DangerHealthShare = 0.4f;
        const float CharacterScale = 1.25f;
        const float FrontLineX = 2.2f;
        const float RowSpacingX = 2.1f;
        const float RowStagger = 0.45f;
        const float RowEdgeMargin = 1.3f;
        const float BattleFieldOfView = 46f;

        // The kit's attack clips land their blow or shot on this frame of 24 per second.
        const float StrikeMoment = 14f / 24f;

        // Enemies strike on the next bar of the music, and wait no longer than that for the song to begin.
        const int BarBeats = 4;
        const float SongWaitSeconds = 8f;

        // The action pad: a round attack button, a skill button for each of the three skills, and the rum.
        const float AttackSize = 200f;
        const float SkillSize = 128f;
        const float RumSize = 118f;
        const float PadRadius = 228f;
        static readonly float[] SkillAngles = { 96f, 141f, 186f };
        const float RumAngle = 232f;
        const float RumCaptionAngle = 180f; // Beside the rum rather than under it, which would be off the bottom of the screen.
        static readonly Vector2 PadCenter = new Vector2(-210f, 222f);
        const float TapReachShare = 0.2f; // How near an enemy a tap must land to pick them, as a share of screen height.

        // The battle shows the beat it plays to: each fighter bounces in their own rhythm on the beat, a track
        // under the banner lights the beat, and each sequence's first beat flashes the edges and jolts the camera.
        const float BounceSquash = 0.06f;
        static readonly int[] BounceEveryChoices = { 2, 3, 4 }; // Each fighter bounces on one beat in this many.
        const float BounceSpread = 0.04f;
        const float DownbeatShake = 0.06f;
        const float DownbeatFlash = 0.34f;
        const float BeatFlash = 0.08f;
        const int WarningTicks = 3; // The window's last beats tick, so running out of time is heard coming.

        // The song's danger layer, its tremolo, low brass and war drums, plays through every deck battle,
        // and swells to full when the crew is in trouble.
        const float BattleDangerFloor = 0.45f;

        public PartyBattle Battle { get; private set; }
        public int ActionsResolved { get; private set; }
        public TimingPrompt Timing => timing;

        GameDirector director;
        TimingPrompt timing;
        GameRandom patternRandom;
        string lastPatternId;
        DeckModel deckModel;
        Transform deck;
        Transform lookTarget;
        Transform cameraRig;
        float time;

        class Fighter
        {
            public Combatant Combatant;
            public CharacterModel Model;
            public Vector3 Home;
            public Quaternion HomeRotation;
            public Bar HpBar;
            public Bar MpBar;
            public TextMeshProUGUI HpText;
            public TextMeshProUGUI MpText;
            public Bar EnemyBar;
            public float Flash;
            public bool Fallen;
            public int BounceEvery; // Beats between bounces, picked at random for each battle.
            public int BounceOffset; // Which of those beats they bounce on, so the crowd doesn't move as one.
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI IntentText;
        }

        readonly List<Fighter> fighters = new List<Fighter>();
        readonly Dictionary<Combatant, Fighter> byCombatant = new Dictionary<Combatant, Fighter>();

        Canvas canvas;
        RectTransform safe;
        TextMeshProUGUI turnLabel;
        TextMeshProUGUI actionLabel;
        Transform turnArrow;
        Material arrowMaterial;
        Action<BattleAction> pendingChoice;
        Fighter current;
        CommsBar comms;
        DeckSetup setup;
        TextMeshProUGUI sabotageLabel;
        RawImage rain;
        float lightningTimer = 3f;
        readonly HashSet<string> saidTriggers = new HashSet<string>();

        // The music's clock. Turns open on a sequence's first beat, and actions start on a beat.
        BattleBeat beat;
        string song;
        double localClockStart;
        long lastBeat = long.MinValue;
        double windowOpen;
        double windowClose;
        bool choiceMade;
        BattleAction chosenAction;

        class SkillSlot
        {
            public GameButton Button;
            public Image Icon;
            public Image Lock;
            public TextMeshProUGUI Caption;
            public SkillDef Skill;
            public int Level;
        }

        RectTransform pad;
        GameButton attackButton;
        Image attackIcon;
        Image windowRing;
        readonly List<Image> beatPips = new List<Image>();
        readonly List<SkillSlot> skillSlots = new List<SkillSlot>();
        GameButton rumButton;
        Image rumIcon;
        TextMeshProUGUI rumCaption;
        Image targetMark;
        Combatant selectedTarget;
        Combatant choosingActor;
        bool clockReady;
        readonly List<Image> sequenceTrack = new List<Image>();
        Image edgeFlash;
        static Sprite edgeSprite;

        void Start()
        {
            director = GameDirector.Instance;
            setup = director.Deck;
            var party = setup.Party;
            var enemies = setup.Enemies;
            Battle = new PartyBattle(party, enemies, director.Seed, director.Content.timing)
            {
                SpareTarget = setup.Spare,
                SurrenderAfterRound = setup.SurrenderAfterRound,
            };
            patternRandom = new GameRandom(director.Seed + 5);
            beat = BattleBeat.From(director.Content.timing);
            song = AudioBank.WantedSong;

            deckModel = Models.CreateDeck(transform, setup.Faction, setup.Arena);
            if (setup.Below) director.Stage.ApplyWeather(director.Content.Weather("below"));
            else if (setup.Storm) director.Stage.ApplyWeather(director.Content.Weather("maelstrom"));
            else if (director.Run != null) director.Stage.ApplyWeather(director.Content.Weather(CampaignLook.WeatherFor(director.Run.Mission)));
            DressArena();
            deck = deckModel.Root.transform;
            // Three-quarter facing, so faces and silhouettes read from the battle camera.
            BuildFighters(party, "party_slot_", 125f);
            BuildFighters(enemies, "enemy_slot_", -125f);

            cameraRig = new GameObject("Battle Camera Rig").transform;
            cameraRig.SetParent(transform, false);
            lookTarget = new GameObject("Battle Look").transform;
            lookTarget.SetParent(transform, false);
            GetCamera(setup.Arena, out var cameraOffset, out var lookHeight);
            lookTarget.position = new Vector3(0f, lookHeight, 0f);
            director.Stage.FrameTarget(cameraRig, lookTarget, cameraOffset, BattleFieldOfView, 0f);

            var arrowKit = new MeshKit();
            arrowKit.Cylinder(new Vector3(0f, 0.55f, 0f), Vector3.zero, 0.3f, 0f, 4, Palette.Accent);
            arrowMaterial = Models.NewLitMaterial("Turn Arrow");
            turnArrow = Models.Attach("Turn Arrow", transform, arrowKit.Build("Arrow"), arrowMaterial, false).transform;
            turnArrow.localScale = Vector3.one * 0.6f;

            BuildHud();
            Say("start");
            if (Battle.SurrenderAfterRound > 0) Say("sabotage");
            if (setup.Storm) BuildRain();
            StartCoroutine(RunBattle());
        }

        // Plays any lines waiting on this moment, once each.
        void Say(string trigger)
        {
            if (setup.Lines == null || !saidTriggers.Add(trigger)) return;
            foreach (var line in setup.Lines)
            {
                if (line.trigger == trigger) comms.Say(line);
            }
        }

        // Interiors have low ceilings, so the camera sits lower. The chamber is large, so it sits further back.
        static void GetCamera(string arena, out Vector3 offset, out float lookHeight)
        {
            switch (arena)
            {
                case "SM_Env_GunDeck":
                    offset = new Vector3(0f, 1.9f, -7.6f);
                    lookHeight = 1f;
                    break;
                case "SM_Env_CellBlock":
                case "SM_Env_WarRoom":
                    offset = new Vector3(0f, 2.3f, -8.4f);
                    lookHeight = 1f;
                    break;
                case "SM_Env_LiftCoreChamber":
                    offset = new Vector3(0f, 3f, -15.5f);
                    lookHeight = 1.5f;
                    break;
                default:
                    offset = new Vector3(0f, 3.4f, -9.6f);
                    lookHeight = 0.5f;
                    break;
            }
        }

        void Dress(string asset, string faction, string socket, Vector3 offset)
        {
            var mark = KitArt.Socket(deckModel.Kit, socket);
            if (mark == null) return;
            var prop = Models.CreateProp(asset, faction, deckModel.Root.transform);
            prop.transform.position = mark.position + offset;
        }

        // Set pieces each arena's story calls for.
        void DressArena()
        {
            if (setup.Arena == "SM_Env_LiftCoreChamber")
            {
                foreach (var ember in Effects.AttachAll("EmberDrift", deckModel.Kit, 2f)) ember.Active = true;
            }
            switch (setup.Arena)
            {
                case "SM_Env_ScrapDeck":
                    Dress("SM_Prop_WinchControls", "Scrap", "winch_controls", Vector3.zero);
                    Dress("SM_Prop_HarpoonWinch", "Scrap", "winch_controls", new Vector3(-2f, 0f, 0.4f));
                    Dress("SM_Prop_KragAnchor", "Scrap", "bay_edge", new Vector3(-1f, 0f, -2.4f));
                    Dress("SM_Prop_ScrapPile", "Scrap", "centre_stage", new Vector3(-5f, 0f, 2.4f));
                    break;
                case "SM_Env_CellBlock":
                    for (var i = 0; i < 5; i++) Dress("SM_Prop_CellDoor", "Warden", "cell_" + i, Vector3.zero);
                    break;
                case "SM_Env_GunDeck":
                    Dress("SM_Prop_DeckMortar", setup.Faction, "floor_breach", new Vector3(0f, 0f, 1.4f));
                    Dress("SM_Prop_SiegeCannon", setup.Faction, "ladder_up", new Vector3(-3.4f, 0f, 0.2f));
                    Dress("SM_Prop_MortarShell", setup.Faction, "ladder_up", new Vector3(2f, 0f, 0.6f));
                    break;
                case "SM_Env_WarRoom":
                    Dress("SM_Prop_ArmadaBanner", "Gilded", "cassian_mark", new Vector3(-2.6f, 0f, -0.4f));
                    Dress("SM_Prop_ArmadaBanner", "Gilded", "cassian_mark", new Vector3(2.6f, 0f, -0.4f));
                    break;
                case "SM_Env_DreadnoughtDeck":
                    Dress("SM_Prop_ArmadaBanner", "Gilded", "hatch", new Vector3(-3.4f, 0f, -1.6f));
                    Dress("SM_Prop_ArmadaBanner", "Gilded", "hatch", new Vector3(3.4f, 0f, -1.6f));
                    Dress("SM_Prop_FlakGun", "Gilded", "hatch", new Vector3(6f, 0f, -1.6f));
                    break;
            }
        }

        void CheckBattleBarks()
        {
            var anyEnemyLow = false;
            foreach (var enemy in Battle.Enemies)
            {
                if (enemy.Alive && enemy != Battle.SpareTarget && enemy.Hp < enemy.Stats.hp * 0.45f) anyEnemyLow = true;
            }
            if (anyEnemyLow) Say("enemyLow");
            foreach (var member in Battle.Party)
            {
                if (member.Alive && member.Hp < member.Stats.hp * 0.3f) Say("partyLow");
            }
        }

        // As wide as the camera can see on this screen, up to a comfortable gap.
        float RowSpacing(int count, float rowZ)
        {
            if (count <= 1) return RowSpacingX;
            GetCamera(setup.Arena, out var offset, out _);
            var depth = rowZ - offset.z;
            var halfWidth = depth * Mathf.Tan(BattleFieldOfView * 0.5f * Mathf.Deg2Rad) * Screen.width / Screen.height;
            var room = (halfWidth - RowEdgeMargin - FrontLineX) / (count - 1);
            return Mathf.Min(RowSpacingX, room);
        }

        void BuildFighters(List<Combatant> combatants, string slotPrefix, float yaw)
        {
            for (var i = 0; i < combatants.Count; i++)
            {
                var combatant = combatants[i];
                var model = Models.CreateCharacter(combatant.Name, string.IsNullOrEmpty(combatant.Look) ? combatant.ClassId : combatant.Look, deck);

                // Enemy leaders are tall, so they stand at the back. Everyone stands in a row across the
                // deck, far enough apart that nobody hides anybody from this camera. A slight stagger keeps
                // it from looking like a parade line.
                var slot = yaw < 0f ? combatants.Count - 1 - i : i;
                var home = deckModel.Spot($"{slotPrefix}3"); // The slot furthest from the camera, so a row fits.
                var spacing = RowSpacing(combatants.Count, home.z);
                home.x = Mathf.Sign(home.x) * (FrontLineX + slot * spacing);
                home.z -= slot % 2 == 1 ? RowStagger : 0f;

                // The chamber's Lift-Core fills the middle of the room, so the fight is in front of it.
                if (setup.Arena == "SM_Env_LiftCoreChamber") home = new Vector3(Mathf.Sign(home.x) * (3.4f + slot * spacing), home.y, -6.6f - slot * 0.5f);
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                model.Root.transform.localPosition = home;
                model.Root.transform.localRotation = rotation;
                model.Root.transform.localScale = Vector3.one * CharacterScale;
                if (model.Companion != null)
                {
                    // Right in front of its owner and nearer the camera, so it only overlaps its owner's boots.
                    model.Companion.Root.transform.position = model.Root.transform.position
                        + new Vector3(-Mathf.Sign(home.x) * CompanionFormationForward, 0f, -CompanionFormationNear);
                }
                model.Animation.Loop("Idle", fighters.Count * 0.37f);
                var bounceEvery = BounceEveryChoices[UnityEngine.Random.Range(0, BounceEveryChoices.Length)];
                var fighter = new Fighter { Combatant = combatant, Model = model, Home = home, HomeRotation = rotation, BounceEvery = bounceEvery, BounceOffset = UnityEngine.Random.Range(0, bounceEvery) };
                fighters.Add(fighter);
                byCombatant[combatant] = fighter;
            }
        }

        // The music tightens when the crew is in trouble, a boss is raging or the charges are about to blow.
        void UpdateMusicDanger()
        {
            var health = 0f;
            var maximum = 0f;
            foreach (var member in Battle.Party)
            {
                health += member.Hp;
                maximum += member.Stats.hp;
            }
            var raging = false;
            foreach (var enemy in Battle.Enemies) raging |= enemy.Enraged && enemy.Hp > 0;
            AudioBank.SetDanger(raging || health < maximum * DangerHealthShare ? 1f : BattleDangerFloor);
        }

        void Update()
        {
            UpdateMusicDanger();
            time += Time.deltaTime;
            UpdateWeather();

            // The deck rocks gently, except on the arenas built into a ship's insides.
            if (!setup.Below) deck.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.6f) * 0.8f, 0f, Mathf.Sin(time * 0.45f) * 1.2f);
            cameraRig.position = new Vector3(Mathf.Sin(time * 0.2f) * 0.4f, 0f, 0f);

            foreach (var fighter in fighters)
            {
                fighter.Flash = Mathf.MoveTowards(fighter.Flash, 0f, Time.deltaTime * 5f);
                fighter.Model.SetEmission(Palette.Cloud * fighter.Flash * 0.75f);
            }

            UpdateBeat();

            if (current != null)
            {
                turnArrow.gameObject.SetActive(true);
                var height = current.Model.HeadHeight * CharacterScale + 0.55f;

                // The arrow drops onto every beat, so the music's pulse shows on the deck too.
                var bounce = beat.Pulse(Now());
                turnArrow.position = current.Model.Root.transform.position + Vector3.up * (height + bounce * bounce * 0.22f);
                turnArrow.Rotate(0f, 180f * Time.deltaTime, 0f);
            }
            else
            {
                turnArrow.gameObject.SetActive(false);
            }
        }

        void BuildRain()
        {
            rain = new GameObject("Rain", typeof(RectTransform)).AddComponent<RawImage>();
            rain.transform.SetParent(canvas.transform, false);
            rain.transform.SetAsFirstSibling();
            rain.raycastTarget = false;
            rain.texture = Resources.Load<Texture2D>($"Art/Vfx/T_VFX_Rain_{KitArt.Quality}");
            rain.color = new Color(1f, 1f, 1f, 0.22f);
            UIKit.Fill(rain.rectTransform);
        }

        void UpdateWeather()
        {
            if (rain == null) return;
            rain.uvRect = new Rect(Time.time * 0.05f, Time.time * 1.2f, rain.rectTransform.rect.width / 512f, rain.rectTransform.rect.height / 512f);
            lightningTimer -= Time.deltaTime;
            if (lightningTimer > 0f) return;
            lightningTimer = UnityEngine.Random.Range(4f, 8f);
            var strikes = KitArt.MatchSockets(deckModel.Kit, new[] { "lightning_strike_*" });
            if (strikes.Count == 0) return;
            Effects.Play("LightningStrike", strikes[UnityEngine.Random.Range(0, strikes.Count)].position, 0.35f);
            director.ScreenFx.Flash(Palette.Cloud, 0.25f, 0.15f);
            AudioBank.Play(Sfx.ThunderDistant, 0.6f);
        }

        // The boarding party swings in on grapple lines, and bosses greet them with a taunt.
        IEnumerator Entrance()
        {
            foreach (var fighter in fighters)
            {
                if (fighter.Combatant.IsPlayer && setup.Boarded) fighter.Model.Animation.Loop("Grapple");
                else if (!fighter.Combatant.IsPlayer && fighter.Combatant.EnrageAt > 0f) fighter.Model.Animation.PlayThenReturn("Taunt");
            }
            if (setup.Boarded)
            {
                AudioBank.Play(Sfx.GrappleSwing, 0.85f);
                var starts = new Dictionary<Fighter, Vector3>();
                foreach (var fighter in fighters)
                {
                    if (fighter.Combatant.IsPlayer) starts[fighter] = fighter.Home + new Vector3(-7f, 3.5f, -2f);
                }
                yield return Ease.Tween(0.85f, t =>
                {
                    foreach (var pair in starts)
                    {
                        var at = Vector3.Lerp(pair.Value, pair.Key.Home, t);
                        at.y += Mathf.Sin(t * Mathf.PI) * 1.2f;
                        pair.Key.Model.Root.transform.localPosition = at;
                    }
                }, Ease.OutCubic, false);
                foreach (var fighter in starts.Keys) fighter.Model.Animation.Loop("Idle", fighter.Combatant.Slot * 0.37f);
                Fx.Burst(deck.position + Vector3.up, Palette.Smoke, 8, 2f, 0.6f, 0.5f);
            }
            yield return new WaitForSeconds(0.5f);
        }

        // Seconds since the battle song's first sample, as the player hears it. Until the song is playing, or in
        // automated runs, a clock of the battle's own stands in, on the same grid.
        double Now()
        {
            if (!GameDirector.Autoplay && AudioBank.TryGetSongTime(song, director.Content.timing.audioLatencySeconds, out var seconds)) return seconds;
            return Time.timeAsDouble - localClockStart;
        }

        IEnumerator WaitForClock(double moment)
        {
            while (Now() < moment) yield return null;
        }

        // The song starts as the screen opens. Its first sequence plays under the boarding party's entrance.
        IEnumerator WaitForSong()
        {
            var waited = 0f;
            while (!GameDirector.Autoplay && !AudioBank.TryGetSongTime(song, 0, out _) && waited < SongWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            localClockStart = Time.timeAsDouble;
            clockReady = true;
        }

        // Counts the last beats before the first turn, so the fight starts on the music's big downbeat.
        IEnumerator CountIn()
        {
            var start = beat.NextSequence(Now());
            if (start - Now() > beat.BeatSeconds * 3.5) actionLabel.text = "READY?";
            var shown = -1;
            while (Now() < start)
            {
                var left = (int)Math.Ceiling((start - Now()) / beat.BeatSeconds);
                if (left <= 3 && left != shown)
                {
                    shown = left;
                    actionLabel.text = left.ToString();
                    AudioBank.Play(Sfx.Tick, 1f, 0f);
                }
                yield return null;
            }
            actionLabel.text = "FIGHT!";
            StartCoroutine(HideActionLabel());
        }

        IEnumerator RunBattle()
        {
            yield return Entrance();
            yield return WaitForSong();
            yield return CountIn();
            while (Battle.Outcome == BattleOutcome.Ongoing)
            {
                var opening = Battle.TakeTurnStartEvents();
                if (opening.Count > 0)
                {
                    yield return ApplyResults(opening);
                    if (Battle.Outcome != BattleOutcome.Ongoing) break;
                }
                RefreshIntents();
                var actor = Battle.Current;
                current = byCombatant[actor];

                BattleAction action;
                double start;
                if (actor.IsPlayer)
                {
                    // A crew member's turn opens on the crash that starts a sequence, and lasts that sequence.
                    var open = beat.NextSequence(Now());
                    turnLabel.text = $"{actor.Name} is up!";
                    yield return WaitForClock(open);

                    // Running late, after a pause or a long hitch, the turn waits for the next sequence rather than
                    // opening with its time already spent.
                    while (Now() - open > BattleBeat.Grace)
                    {
                        open = beat.NextSequence(Now());
                        yield return WaitForClock(open);
                    }
                    yield return Choose(actor, open);
                    action = chosenAction;
                    if (!choiceMade)
                    {
                        // Too slow: the turn is lost, and the next fighter goes on the coming bar.
                        yield return LoseTurn(actor);
                        continue;
                    }
                    start = beat.NextAhead(Now());
                }
                else
                {
                    turnLabel.text = $"{actor.Name}'s turn";
                    start = beat.Next(Now(), BarBeats);
                    action = Battle.ChooseEnemyAction(actor);
                }

                yield return WaitForClock(start);
                yield return PlayAction(actor, action, start);
                ActionsResolved++;
                CheckBattleBarks();
                RefreshSabotage();
            }
            var closing = Battle.TakeTurnStartEvents();
            if (closing.Count > 0) yield return ApplyResults(closing);

            current = null;
            var won = Battle.Outcome == BattleOutcome.Victory;
            turnLabel.text = "";
            director.ScreenFx.Milestone(Battle.Surrendered ? "THEY SURRENDER!" : won ? "VICTORY!" : "DEFEATED", won ? Palette.Accent : Palette.Danger);
            foreach (var fighter in fighters)
            {
                if (fighter.Fallen || !fighter.Combatant.Alive) continue;
                if (fighter.Combatant.IsPlayer == won) fighter.Model.Animation.Loop("Victory");
                else fighter.Model.Animation.Play("Surrender");
            }
            if (Battle.Surrendered)
            {
                Effects.Play("LiftCoreSabotage", deck.position + Vector3.up * 2f, 0.7f);
                var flag = UIKit.Icon("Surrender", safe, "Story_Surrender");
                UIKit.Place(flag.rectTransform, new Vector2(0.5f, 0.3f), Vector2.one * 0.5f, Vector2.zero, Vector2.one * 180f);
            }
            if (won && !setup.Below) RevealPlunder();
            yield return new WaitForSeconds(2.4f);
            director.OnBattleFinished(won, Battle);
        }

        void RevealPlunder()
        {
            var center = deckModel.Spot("centre_stage");
            var chest = Models.CreateProp("SM_Prop_TreasureChestOpen", KitArt.Crimson, deck);
            chest.transform.localPosition = center + new Vector3(0f, 0f, -1.2f);
            chest.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            chest.transform.localScale = Vector3.one * 1.5f;
            var gold = Models.CreateProp("SM_Prop_GoldPile", KitArt.Crimson, deck);
            gold.transform.localPosition = center + new Vector3(1f, 0f, -1.6f);
            gold.transform.localScale = Vector3.one * 1.2f;
            Fx.Burst(chest.transform.position + Vector3.up * 0.8f, Palette.Spark, 18, 3f, 0.6f, 0.8f, 2f);
            AudioBank.Play(Sfx.Plunder, 0.85f);
        }

        // A plain policy for automated runs: heal when hurt, otherwise use the strongest attack.
        BattleAction ChooseAutoAction(Combatant actor)
        {
            var allies = Battle.Party;
            Combatant weakest = null;
            foreach (var ally in allies)
            {
                if (ally.Alive) continue;
                foreach (var skill in actor.Skills)
                {
                    if (skill.revive && PartyBattle.CanUse(actor, skill)) return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = ally };
                }
            }
            foreach (var ally in allies)
            {
                if (ally.Alive && (weakest == null || ally.Hp / ally.Stats.hp < weakest.Hp / weakest.Stats.hp)) weakest = ally;
            }
            foreach (var skill in actor.Skills)
            {
                if (skill.Kind == SkillKind.Heal && skill.status == null && PartyBattle.CanUse(actor, skill) && weakest.Hp < weakest.Stats.hp * 0.5f)
                {
                    return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = weakest };
                }
            }
            Combatant target = null;
            foreach (var enemy in Battle.Enemies)
            {
                if (!enemy.Alive || enemy == Battle.SpareTarget) continue;
                if (target == null || enemy.Hp < target.Hp) target = enemy;
            }
            var foes = 0;
            foreach (var enemy in Battle.Enemies)
            {
                if (enemy.Alive && enemy != Battle.SpareTarget) foes++;
            }
            foreach (var skill in actor.Skills)
            {
                // A sweep only earns its cost against a crowd.
                var sweeps = skill.Target == SkillTarget.AllEnemies;
                if (skill.Kind == SkillKind.Heal || (sweeps && foes < 2) || !PartyBattle.CanUse(actor, skill)) continue;
                return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = target };
            }
            return new BattleAction { Kind = BattleActionKind.Attack, Target = target };
        }

        // Starts on a beat of the music, at the moment start. Its timing prompt runs on the music's clock from there.
        IEnumerator PlayAction(Combatant actor, BattleAction action, double start)
        {
            var fighter = byCombatant[actor];
            var isMagic = action.Kind == BattleActionKind.Skill && action.Skill.Kind == SkillKind.Magic;
            var isHeal = action.Kind == BattleActionKind.Skill && action.Skill.Kind == SkillKind.Heal;
            var isRanged = director.Content.Class(actor.ClassId).ranged;
            var label = action.Kind == BattleActionKind.Skill ? action.Skill.name : action.Kind == BattleActionKind.Guard ? "Guard" : action.Kind == BattleActionKind.Item ? action.Item.name : "Attack";
            actionLabel.text = label;
            StartCoroutine(HideActionLabel());

            if (action.Kind == BattleActionKind.Item)
            {
                var used = Battle.Submit(action);
                director.Save.AddItem(action.Item.id, -1);
                director.SaveGame();
                AudioBank.Play(action.Item.kind == "heal" ? Sfx.Heal : Sfx.HeavyCannon, 0.8f);
                if (action.Item.kind != "heal") Effects.Play("MortarImpact", deck.position + new Vector3(4f, 1.2f, 0f));
                fighter.Model.Animation.PlayThenReturn(action.Item.kind == "heal" ? "Work" : "AttackRanged");
                yield return ApplyResults(used);
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            // The pattern's last target lands on the attack's strike frame, so reading the swing helps.
            var pattern = ShowTimingPrompt(actor, action, isHeal, isMagic, start);
            yield return WaitForClock(start + Mathf.Max(0f, pattern.Length - StrikeMoment));

            if (action.Kind == BattleActionKind.Skill && action.Skill.companion && fighter.Model.Companion != null)
            {
                yield return CompanionAttack(fighter, action);
            }
            else if (isHeal || isMagic || isRanged)
            {
                yield return CastOrShoot(fighter, action, isMagic, isHeal);
            }
            else
            {
                var target = action.Target ?? Battle.Opponents(actor).Find(c => c.Alive);
                yield return Lunge(fighter, target != null ? byCombatant[target] : null, action);
            }
        }

        // The tip of what someone's holding, or their chest if it has none.
        const float CompanionFormationForward = 0f;
        const float CompanionFormationNear = 1.9f;
        const float CompanionWalkSeconds = 0.6f;
        const float CompanionStepForward = 2f;
        const float CompanionFlameDelay = 0.5f; // The clip's jaws are open by here.
        const float CompanionFlameSeconds = 0.8f;
        const float CompanionFlameHeight = 1f;

        static Vector3 WeaponPoint(Fighter fighter, string socket)
        {
            foreach (var held in fighter.Model.Held)
            {
                var point = KitArt.Socket(held, socket);
                if (point != null) return point.position;
            }
            var chest = KitArt.Socket(fighter.Model.Root, "chest_centre");
            return chest != null ? chest.position : fighter.Model.Root.transform.position + Vector3.up * 1.4f;
        }

        TimingPattern ShowTimingPrompt(Combatant actor, BattleAction action, bool isHeal, bool isMagic, double start)
        {
            var pattern = TimingSequence.Pick(director.Content.timing, director.EncounterTier, actor.IsPlayer, patternRandom, lastPatternId);
            lastPatternId = pattern.id;
            Func<double> clock = () => Now() - start;
            if (actor.IsPlayer)
            {
                timing.Begin(pattern, isHeal ? "FOCUS!" : isMagic ? "CAST!" : "STRIKE!", isHeal ? Palette.Heal : Palette.PlayerPaint, clock);
                return pattern;
            }

            timing.Begin(pattern, "BLOCK!", Palette.Mana, clock);
            var hitsEveryone = action.Kind == BattleActionKind.Skill && action.Skill.Target == SkillTarget.AllEnemies;
            turnLabel.text = hitsEveryone || action.Target == null ? "Targeting everyone!" : $"Targeting {action.Target.Name}!";
            return pattern;
        }

        // Waits for the prompt's grade, then resolves the action with it.
        List<BattleEvent> strikeResults;

        IEnumerator ResolveStrike(BattleAction action)
        {
            while (!timing.Done) yield return null;
            action.Timing = timing.Grade;
            strikeResults = Battle.Submit(action);
        }

        IEnumerator HideActionLabel()
        {
            yield return new WaitForSeconds(1.1f);
            actionLabel.text = "";
        }

        // Turns a fighter toward a target, for the length of an attack.
        void Face(Fighter fighter, Fighter target)
        {
            if (target == null || target == fighter) return;
            var direction = target.Home - fighter.Home;
            direction.y = 0f;
            fighter.Model.Root.transform.localRotation = Quaternion.LookRotation(direction);
        }

        // The character whistles, and their companion steps up and does it instead.
        IEnumerator CompanionAttack(Fighter fighter, BattleAction action)
        {
            var companion = fighter.Model.Companion;
            var dog = companion.Root.transform;
            var gait = companion.Root.GetComponent<CompanionGait>();
            var home = dog.position;
            var homeRotation = dog.localRotation;
            var foes = Battle.Opponents(fighter.Combatant).FindAll(c => c.Alive);
            var aim = Vector3.zero;
            foreach (var foe in foes) aim += byCombatant[foe].Model.Root.transform.position;
            aim = aim / Mathf.Max(1, foes.Count) + Vector3.up * CompanionFlameHeight;

            // It walks out in front of the line, so nothing is in the way of the fire.
            fighter.Model.Animation.PlayThenReturn("Taunt");
            var flat = aim - home;
            flat.y = 0f;
            dog.rotation = Quaternion.LookRotation(flat);
            var stand = home + flat.normalized * CompanionStepForward;
            for (var t = 0f; t < 1f; t += Time.deltaTime / CompanionWalkSeconds)
            {
                dog.position = Vector3.Lerp(home, stand, t);
                yield return null;
            }
            dog.position = stand;

            yield return ResolveStrike(action);
            gait.Busy = true;
            companion.Animation.PlayThenReturn("Fire");
            yield return new WaitForSeconds(CompanionFlameDelay);
            AudioBank.Play(Sfx.Cannon, 0.7f);
            AudioBank.Play(Sfx.Burn, 0.9f);
            var muzzle = KitArt.FindDeep(dog, "muzzle");
            Effects.Play("CannonFire", muzzle.position, 0.5f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / CompanionFlameSeconds)
            {
                var mouth = muzzle.position;
                for (var i = 0; i < 3; i++)
                {
                    var along = UnityEngine.Random.value;
                    var color = Color.Lerp(Palette.Accent, Palette.Danger, along);
                    var spread = UnityEngine.Random.insideUnitSphere * (0.15f + along * 0.9f);
                    Fx.Burst(Vector3.Lerp(mouth, aim, along * Mathf.Min(1f, t * 1.6f)) + spread, color, 1, 0.8f, 0.45f + along * 0.5f, 0.28f);
                }
                yield return null;
            }
            foreach (var foe in foes) Effects.Play("HullFire", byCombatant[foe].Model.Root.transform.position + Vector3.up, 0.6f);
            yield return ApplyResults(strikeResults);
            gait.Busy = false;

            var back = home - dog.position;
            back.y = 0f;
            if (back.sqrMagnitude > 0.01f) dog.rotation = Quaternion.LookRotation(back);
            var from = dog.position;
            for (var t = 0f; t < 1f; t += Time.deltaTime / CompanionWalkSeconds)
            {
                dog.position = Vector3.Lerp(from, home, t);
                yield return null;
            }
            dog.position = home;
            dog.localRotation = homeRotation;
            fighter.Model.Root.transform.localRotation = fighter.HomeRotation;
        }

        IEnumerator CastOrShoot(Fighter fighter, BattleAction action, bool isMagic, bool isHeal)
        {
            if (action.Target != null) Face(fighter, byCombatant[action.Target]);
            fighter.Model.Animation.PlayThenReturn("AttackRanged");
            yield return new WaitForSeconds(StrikeMoment);
            yield return ResolveStrike(action);

            var origin = fighter.Model.Root.transform.position + Vector3.up * 1.6f;
            if (isHeal)
            {
                AudioBank.Play(Sfx.Heal);
                Fx.Burst(origin, Palette.Heal, 12, 2f, 0.5f, 0.6f, 1.5f);
            }
            else
            {
                AudioBank.Play(isMagic ? Sfx.MagicCast : Sfx.Pistol, isMagic ? 0.75f : 0.7f);
                var color = isMagic ? Palette.Storm : Palette.Spark;
                var muzzle = WeaponPoint(fighter, "muzzle");
                Effects.Play(isMagic ? "LiftCoreSabotage" : "CannonFire", muzzle, isMagic ? 0.25f : 0.5f);
                yield return ShootProjectile(muzzle, action, color);
            }
            yield return ApplyResults(strikeResults);
            fighter.Model.Root.transform.localRotation = fighter.HomeRotation;
        }

        IEnumerator ShootProjectile(Vector3 origin, BattleAction action, Color color)
        {
            var aim = action.Target != null
                ? byCombatant[action.Target].Model.Root.transform.position + Vector3.up * 1.4f
                : (Battle.Current != null ? new Vector3(origin.x > 0f ? -5f : 6f, 1.6f, 0f) : origin);

            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.16f)
            {
                Fx.Burst(Vector3.Lerp(origin, aim, t), color, 1, 0.5f, 0.55f, 0.18f);
                yield return null;
            }
        }

        IEnumerator Lunge(Fighter fighter, Fighter target, BattleAction action)
        {
            var root = fighter.Model.Root.transform;
            var home = fighter.Home;
            var strikePoint = target != null
                ? Vector3.Lerp(home, target.Home, 0.72f)
                : home + (fighter.Combatant.IsPlayer ? Vector3.right : Vector3.left) * 3f;

            Face(fighter, target);
            fighter.Model.Animation.PlayThenReturn("AttackMelee");
            yield return Ease.Tween(StrikeMoment, t => root.localPosition = Vector3.LerpUnclamped(home, strikePoint, t), Ease.OutCubic, false);
            yield return ResolveStrike(action);
            AudioBank.Play(Sfx.Slash, 0.9f);
            Effects.Play("MeleeSlash", WeaponPoint(fighter, "trail_tip"));
            yield return ApplyResults(strikeResults);
            if (fighter.Combatant.Alive) fighter.Model.Animation.Loop("Run");
            root.localRotation = Quaternion.LookRotation(home - strikePoint == Vector3.zero ? Vector3.forward : home - strikePoint);
            yield return Ease.Tween(0.28f, t => root.localPosition = Vector3.LerpUnclamped(strikePoint, home, t), Ease.OutCubic, false);
            root.localRotation = fighter.HomeRotation;
            if (fighter.Combatant.Alive) fighter.Model.Animation.Loop("Idle", fighter.Combatant.Slot * 0.37f);
        }

        IEnumerator ApplyResults(List<BattleEvent> results)
        {
            var anyDefeated = false;
            foreach (var result in results)
            {
                switch (result.Kind)
                {
                    case BattleEventKind.Damage:
                        ShowDamage(result);
                        break;
                    case BattleEventKind.Heal:
                        var healed = byCombatant[result.Target];
                        ShowFloating(healed, $"+{result.Amount}", Palette.Heal, 56f);
                        Fx.Burst(healed.Model.Root.transform.position + Vector3.up * 1.2f, Palette.Heal, 14, 1.8f, 0.5f, 0.7f, 2.2f);
                        break;
                    case BattleEventKind.Defeated:
                        anyDefeated = true;
                        Fall(byCombatant[result.Target]);
                        break;
                    case BattleEventKind.Status:
                        ShowFloating(byCombatant[result.Target], result.Label.ToUpperInvariant() + "!", Palette.Accent, 44f);
                        AudioBank.Play(Sfx.StatusHit, 0.65f);
                        break;
                    case BattleEventKind.Burned:
                        var burned = byCombatant[result.Target];
                        burned.Flash = 1f;
                        ShowFloating(burned, result.Amount.ToString(), Palette.Danger, 50f);
                        Effects.Play("HullFire", burned.Model.Root.transform.position, 0.6f);
                        AudioBank.Play(Sfx.Burn, 0.7f);
                        break;
                    case BattleEventKind.Stunned:
                        var stunned = byCombatant[result.Target];
                        ShowFloating(stunned, "STUNNED", Palette.Storm, 50f);
                        stunned.Model.Animation.PlayThenReturn("Hit");
                        AudioBank.Play(Sfx.Stunned, 0.7f);
                        break;
                    case BattleEventKind.Counter:
                        var countered = byCombatant[result.Target];
                        countered.Flash = 1f;
                        byCombatant[result.Source].Model.Animation.PlayThenReturn("AttackMelee");
                        Effects.Play("BladeClash", countered.Model.Root.transform.position + Vector3.up * 1.4f, 1.5f);
                        ShowFloating(countered, $"COUNTER {result.Amount}", Palette.Accent, 56f);
                        AudioBank.Play(Sfx.BladeClash, 0.9f);
                        break;
                    case BattleEventKind.Enraged:
                        var enraged = byCombatant[result.Target];
                        enraged.Model.Animation.PlayThenReturn("Taunt");
                        enraged.Model.SetEmission(Palette.Danger * 0.4f);
                        Effects.Play("BladeClash", enraged.Model.Root.transform.position + Vector3.up * 1.5f, 2.5f);
                        director.ScreenFx.Milestone($"{enraged.Combatant.Name.ToUpperInvariant()} RAGES!", Palette.Danger, Sfx.BossRoar);
                        Say("enrage");
                        break;
                    case BattleEventKind.Revived:
                        var revived = byCombatant[result.Target];
                        revived.Fallen = false;
                        revived.Model.Animation.Loop("Idle", 0f);
                        ShowFloating(revived, "BACK ON DECK", Palette.Heal, 50f);
                        AudioBank.Play(Sfx.Revive, 0.85f);
                        Fx.Burst(revived.Model.Root.transform.position + Vector3.up, Palette.Heal, 18, 2f, 0.5f, 0.8f, 2f);
                        break;
                }
            }
            RefreshBars();
            yield return new WaitForSeconds(anyDefeated ? 0.4f : 0.18f);
        }

        void ShowDamage(BattleEvent result)
        {
            var fighter = byCombatant[result.Target];
            fighter.Flash = 1f;
            var position = fighter.Model.Root.transform.position + Vector3.up * 1.6f;
            Effects.Play("HullImpact", position, result.Critical ? 0.8f : 0.5f);
            AudioBank.Play(result.Critical ? Sfx.HeavyHit : Sfx.Hit, 0.85f);

            // Timing shows in the number's color and size, so a good tap reads as a bigger hit or a
            // smaller wound. A blocked hit plays the guard pose instead of the flinch.
            var perfect = result.Timing == TimingGrade.Perfect;
            var timed = result.Timing != TimingGrade.Miss;
            var blocked = timed && fighter.Combatant.IsPlayer;
            if (blocked)
            {
                Effects.Play("BladeClash", position);
                AudioBank.Play(Sfx.BladeClash, 0.75f);
            }
            var woundedByMiss = !timed && fighter.Combatant.IsPlayer;
            var weakenedByMiss = !timed && !fighter.Combatant.IsPlayer;
            var color = woundedByMiss ? Palette.Danger : weakenedByMiss ? Palette.TextMuted : blocked ? Palette.Mana : perfect || result.Critical ? Palette.Accent : Palette.TextLight;
            var size = woundedByMiss || (!blocked && (perfect || result.Critical)) ? 76f : blocked || weakenedByMiss ? 46f : 58f;
            ShowFloating(fighter, result.Critical ? $"{result.Amount}!" : result.Amount.ToString(), color, size);
            if (fighter.Combatant.Alive) fighter.Model.Animation.PlayThenReturn(blocked ? "Block" : "Hit");
            director.Stage.Shake(result.Critical || (perfect && !blocked) ? 0.28f : blocked ? 0.05f : 0.12f);
            if (result.Critical || perfect) GameTime.HitStop(0.05f);
        }

        void ShowFloating(Fighter fighter, string value, Color color, float size)
        {
            var position = fighter.Model.Root.transform.position + Vector3.up * 3f;
            director.ScreenFx.FloatingNumber(position, director.Stage.MainCamera, value, color, size);
        }

        void Fall(Fighter fighter)
        {
            fighter.Fallen = true;
            AudioBank.Play(Sfx.BodyFall, 0.85f);
            Fx.Burst(fighter.Model.Root.transform.position + Vector3.up * 0.5f, Palette.Smoke, 7, 2f, 0.5f, 0.6f, 1f);
            fighter.Model.Animation.Play("Death");
        }

        void BuildHud()
        {
            canvas = UIKit.CreateCanvas("Boarding Hud", transform, 10);
            safe = UIKit.SafeArea(canvas.transform);

            // Behind everything else: a tap on the deck picks the nearest enemy as the target.
            var deckTaps = TapZone.Create("Deck Taps", safe, TapDeck);
            UIKit.Fill((RectTransform)deckTaps.transform);

            var banner = UIKit.Panel("Turn Banner", safe, Palette.WithAlpha(Palette.Panel, 0.9f));
            UIKit.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(640f, 84f));
            turnLabel = UIKit.Text("Turn", banner.transform, "", 40f, Palette.TextLight);
            UIKit.Fill(turnLabel.rectTransform);

            actionLabel = UIKit.Text("Action", safe, "", 54f, Palette.Accent);
            actionLabel.outlineWidth = 0.2f;
            actionLabel.outlineColor = Palette.Ink;
            UIKit.Place(actionLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(800f, 70f));

            BuildPartyPanel();
            BuildEnemyPanel();
            if (!string.IsNullOrEmpty(setup.Location))
            {
                var location = UIKit.Text("Location", safe, setup.Location, 28f, Palette.TextLight, TextAlignmentOptions.Left);
                location.outlineWidth = 0.2f;
                location.outlineColor = Palette.Ink;
                UIKit.Place(location.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -24f), new Vector2(620f, 40f));
            }
            sabotageLabel = UIKit.Text("Sabotage", safe, "", 32f, Palette.Danger, TextAlignmentOptions.Left);
            sabotageLabel.outlineWidth = 0.2f;
            sabotageLabel.outlineColor = Palette.Ink;
            UIKit.Place(sabotageLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -68f), new Vector2(620f, 44f));
            if (Battle.SurrenderAfterRound > 0)
            {
                var timer = UIKit.Icon("Timer", safe, "Hud_Timer");
                UIKit.Place(timer.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -62f), Vector2.one * 54f);
                var charge = UIKit.Icon("Sable", safe, "Ability_SableSabotage");
                UIKit.Place(charge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(690f, -62f), Vector2.one * 54f);
            }
            RefreshSabotage();
            BuildActionPad();
            BuildBeatTrack();
            timing = TimingPrompt.Create(safe, director.Content.timing);
            comms = CommsBar.Create(safe, director.Content.campaign, -108f);
        }

        // Enemies announce their turn, so the player can guard the target or strike first.
        void RefreshIntents()
        {
            foreach (var fighter in fighters)
            {
                var combatant = fighter.Combatant;
                var tags = StatusTags(combatant);
                if (fighter.NameText != null)
                {
                    var name = combatant == Battle.SpareTarget ? $"{combatant.Name} (SPARE)" : combatant.Name;
                    fighter.NameText.text = tags.Length > 0 ? $"{name} <size=70%><color=#F2B134>{tags}</color></size>" : name;
                }
                if (fighter.IntentText == null) continue;
                if (!combatant.Alive)
                {
                    fighter.IntentText.text = "";
                }
                else if (combatant.StunTurns > 0)
                {
                    fighter.IntentText.text = "Stunned";
                }
                else if (Battle.TryGetIntent(combatant, out var intent))
                {
                    var hitsAll = intent.Kind == BattleActionKind.Skill && intent.Skill.Target == SkillTarget.AllEnemies;
                    var heals = intent.Kind == BattleActionKind.Skill && intent.Skill.Kind == SkillKind.Heal;
                    var label = intent.Kind == BattleActionKind.Skill ? intent.Skill.name : "Attack";
                    fighter.IntentText.text = heals ? $"{label}: {intent.Target?.Name}" : hitsAll ? $"{label}: everyone" : $"{label}: {intent.Target?.Name}";
                    fighter.IntentText.color = hitsAll ? Palette.Danger : Palette.Accent;
                }
            }
        }

        static string StatusTags(Combatant combatant)
        {
            var tags = "";
            if (combatant.BurnTurns > 0) tags += " BURN";
            if (combatant.StunTurns > 0) tags += " STUN";
            if (combatant.ExposedTurns > 0) tags += " EXPOSED";
            if (combatant.HasteTurns > 0) tags += " HASTE";
            return tags;
        }

        void RefreshSabotage()
        {
            if (Battle.SurrenderAfterRound <= 0) return;
            var left = Mathf.Max(0, Battle.SurrenderAfterRound - Battle.Round + 1);
            sabotageLabel.text = $"LIFT-CORE CHARGES: {left} ROUND{(left == 1 ? "" : "S")}";
        }

        void BuildPartyPanel()
        {
            var panel = UIKit.Panel("Party", safe, Palette.WithAlpha(Palette.Panel, 0.9f));
            UIKit.Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(28f, 28f), new Vector2(860f, 24f + Battle.Party.Count * 69f));

            for (var i = 0; i < Battle.Party.Count; i++)
            {
                var fighter = byCombatant[Battle.Party[i]];
                var y = -18f - i * 69f;
                var name = UIKit.Text("Name", panel.transform, fighter.Combatant.Name, 30f, Palette.TextLight, TextAlignmentOptions.Left);
                name.enableAutoSizing = true;
                name.fontSizeMin = 16f;
                name.fontSizeMax = 30f;
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, y), new Vector2(250f, 56f));
                fighter.NameText = name;

                fighter.HpBar = UIKit.Bar("HP", panel.transform, Palette.Heal);
                UIKit.Place((RectTransform)fighter.HpBar.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(278f, y - 8f), new Vector2(330f, 40f));
                fighter.HpText = UIKit.Text("HP Value", fighter.HpBar.transform, "", 25f, Palette.TextLight);
                fighter.HpText.outlineWidth = 0.2f;
                fighter.HpText.outlineColor = Palette.Ink;
                UIKit.Fill(fighter.HpText.rectTransform);

                fighter.MpBar = UIKit.Bar("MP", panel.transform, Palette.Mana);
                UIKit.Place((RectTransform)fighter.MpBar.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, y - 8f), new Vector2(218f, 40f));
                fighter.MpText = UIKit.Text("MP Value", fighter.MpBar.transform, "", 25f, Palette.TextLight);
                fighter.MpText.outlineWidth = 0.2f;
                fighter.MpText.outlineColor = Palette.Ink;
                UIKit.Fill(fighter.MpText.rectTransform);
            }
            RefreshBars(true);
        }

        void BuildEnemyPanel()
        {
            var panel = UIKit.Panel("Enemies", safe, Palette.WithAlpha(Palette.Panel, 0.9f));
            var rows = Battle.Enemies.Count;
            UIKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -20f), new Vector2(560f, 22f + rows * 68f));
            for (var i = 0; i < rows; i++)
            {
                var fighter = byCombatant[Battle.Enemies[i]];
                var y = -12f - i * 68f;
                var isSpare = fighter.Combatant == Battle.SpareTarget;
                var name = UIKit.Text("Name", panel.transform, isSpare ? $"{fighter.Combatant.Name} (SPARE)" : fighter.Combatant.Name, 26f, isSpare ? Palette.Accent : Palette.TextLight, TextAlignmentOptions.Left);
                name.enableAutoSizing = true;
                name.fontSizeMin = 16f;
                name.fontSizeMax = 26f;
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(270f, 34f));
                fighter.NameText = name;
                fighter.EnemyBar = UIKit.Bar("HP", panel.transform, Palette.Danger);
                UIKit.Place((RectTransform)fighter.EnemyBar.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, y - 6f), new Vector2(250f, 26f));
                fighter.IntentText = UIKit.Text("Intent", panel.transform, "", 21f, Palette.Accent, TextAlignmentOptions.Left, false);
                fighter.IntentText.characterSpacing = 0f;
                UIKit.Place(fighter.IntentText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, y - 32f), new Vector2(530f, 28f));

                // Tapping an enemy's row picks them as the target, the same as tapping them on deck.
                var enemy = fighter.Combatant;
                var row = TapZone.Create("Pick Target", panel.transform, _ => PickTarget(enemy));
                UIKit.Place((RectTransform)row.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y + 6f), new Vector2(560f, 68f));
            }
            RefreshBars(true);
        }

        void RefreshBars(bool instant = false)
        {
            foreach (var fighter in fighters)
            {
                var combatant = fighter.Combatant;
                if (fighter.HpBar != null)
                {
                    fighter.HpBar.Set(combatant.Hp / combatant.Stats.hp, instant);
                    fighter.HpText.text = combatant.Hp.ToString();
                    fighter.MpBar.Set(combatant.Stats.mp > 0f ? combatant.Mp / combatant.Stats.mp : 0f, instant);
                    fighter.MpText.text = combatant.Mp.ToString();
                }
                if (fighter.EnemyBar != null) fighter.EnemyBar.Set(combatant.Hp / combatant.Stats.hp, instant);
            }
        }

        // One sequence of the music to pick a move. Picking early starts it on the next beat, and running out of
        // time loses the turn. A pause gives the window back once play resumes.
        IEnumerator Choose(Combatant actor, double open)
        {
            windowOpen = open;
            windowClose = open + beat.SequenceSeconds;
            choiceMade = false;
            pendingChoice = a =>
            {
                if (choiceMade) return;
                chosenAction = a;
                choiceMade = true;
            };
            turnLabel.text = $"{actor.Name}: choose!";
            if (GameDirector.Autoplay) pendingChoice(ChooseAutoAction(actor));
            else ShowActionPad(actor);

            var paused = false;
            while (!choiceMade)
            {
                if (GameTime.Paused)
                {
                    paused = true;
                    yield return null;
                    continue;
                }
                if (paused)
                {
                    paused = false;
                    windowClose = beat.NextSequence(Now()) + beat.SequenceSeconds;
                    windowOpen = windowClose - beat.SequenceSeconds;
                }
                if (Now() >= windowClose) break;
                yield return null;
            }
            HideActionPad();
            pendingChoice = null;
            if (!choiceMade) chosenAction = new BattleAction { Kind = BattleActionKind.Pass };
            turnLabel.text = $"{actor.Name}'s turn";
        }

        IEnumerator LoseTurn(Combatant actor)
        {
            turnLabel.text = $"{actor.Name} hesitated!";
            ShowFloating(current, "TOO SLOW!", Palette.Danger, 50f);
            AudioBank.Play(Sfx.Denied, 0.8f);
            current.Model.Animation.PlayThenReturn("Hit");
            var results = Battle.Submit(new BattleAction { Kind = BattleActionKind.Pass });
            ActionsResolved++;
            yield return ApplyResults(results);
            RefreshSabotage();
        }

        // The selected enemy while they stand, else the weakest one not being spared.
        Combatant ChosenEnemy()
        {
            if (selectedTarget != null && selectedTarget.Alive) return selectedTarget;
            Combatant weakest = null;
            foreach (var enemy in Battle.Enemies)
            {
                if (!enemy.Alive || enemy == Battle.SpareTarget) continue;
                if (weakest == null || enemy.Hp < weakest.Hp) weakest = enemy;
            }
            selectedTarget = weakest ?? Battle.Enemies.FirstOrDefault(c => c.Alive);
            return selectedTarget;
        }

        // There's no time for a menu, so a heal goes to whoever needs it most: the fallen first, for a revive.
        Combatant NeediestAlly(bool revive)
        {
            if (revive)
            {
                var fallen = Battle.Party.FirstOrDefault(c => !c.Alive);
                if (fallen != null) return fallen;
            }
            Combatant neediest = null;
            foreach (var ally in Battle.Party)
            {
                if (ally.Alive && (neediest == null || ally.Hp / ally.Stats.hp < neediest.Hp / neediest.Stats.hp)) neediest = ally;
            }
            return neediest;
        }

        bool AnyAllyHurt() => Battle.Party.Any(c => c.Alive && c.Hp < c.Stats.hp);

        void PickTarget(Combatant enemy)
        {
            if (choosingActor == null || enemy == null || !enemy.Alive) return;
            selectedTarget = enemy;
            byCombatant[enemy].Flash = 0.7f;
            AudioBank.Play(Sfx.Click, 0.9f, 0f);
        }

        // Picks the enemy whose figure is nearest the tap, if it's close enough to mean them.
        void TapDeck(Vector2 screen)
        {
            if (choosingActor == null) return;
            Combatant nearest = null;
            var best = Screen.height * TapReachShare;
            foreach (var enemy in Battle.Enemies)
            {
                if (!enemy.Alive) continue;
                var fighter = byCombatant[enemy];
                var chest = fighter.Model.Root.transform.position + Vector3.up * fighter.Model.HeadHeight * CharacterScale * 0.6f;
                var at = director.Stage.MainCamera.WorldToScreenPoint(chest);
                if (at.z <= 0f) continue;
                var distance = Vector2.Distance(screen, at);
                if (distance < best)
                {
                    best = distance;
                    nearest = enemy;
                }
            }
            PickTarget(nearest);
        }

        void BuildActionPad()
        {
            pad = UIKit.Node("Action Pad", safe);
            UIKit.Place(pad, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(660f, 600f));

            // The ring drains over the turn's sequence, and a pip goes dark on each beat, so the time left is
            // something the player can see and hear at once.
            windowRing = new GameObject("Beat Ring", typeof(RectTransform)).AddComponent<Image>();
            windowRing.transform.SetParent(pad, false);
            windowRing.sprite = TimingPrompt.RingSprite;
            windowRing.type = Image.Type.Filled;
            windowRing.fillMethod = Image.FillMethod.Radial360;
            windowRing.fillOrigin = (int)Image.Origin360.Top;
            windowRing.fillClockwise = false;
            windowRing.raycastTarget = false;
            UIKit.Place(windowRing.rectTransform, new Vector2(1f, 0f), Vector2.one * 0.5f, PadCenter, Vector2.one * (AttackSize + 40f));
            for (var i = 0; i < beat.SequenceBeats; i++)
            {
                var pip = UIKit.Solid("Beat Pip", pad, Palette.Accent);
                pip.sprite = UIKit.Circle;
                var angle = (90f - 360f * i / beat.SequenceBeats) * Mathf.Deg2Rad;
                var radius = AttackSize * 0.5f + 40f;
                UIKit.Place(pip.rectTransform, new Vector2(1f, 0f), Vector2.one * 0.5f, PadCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, Vector2.one * 22f);
                beatPips.Add(pip);
            }

            attackButton = RoundButton("Attack", PadCenter, AttackSize, Palette.PlayerPaint, "Hud_Attack", 0.6f, out attackIcon,
                () => pendingChoice?.Invoke(new BattleAction { Kind = BattleActionKind.Attack, Target = ChosenEnemy() }));
            attackButton.Label.text = "ATTACK";
            attackButton.Label.fontSize = 26f;
            UIKit.Place(attackButton.Label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(200f, 34f));

            for (var i = 0; i < BattleSetup.SkillSlots; i++)
            {
                var slot = new SkillSlot();
                var chosen = slot;
                slot.Button = RoundButton($"Skill {i + 1}", Around(SkillAngles[i], PadRadius), SkillSize, Palette.PanelLight, null, 0.92f, out slot.Icon, () => UseSkill(chosen));
                slot.Lock = UIKit.Icon("Locked", slot.Button.transform, "Hud_Locked");
                UIKit.Place(slot.Lock.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * SkillSize * 0.6f);
                slot.Caption = Caption(slot.Button, SkillAngles[i], SkillSize);
                skillSlots.Add(slot);
            }

            rumButton = RoundButton("Rum", Around(RumAngle, PadRadius), RumSize, Palette.PanelLight, "Item_Rum", 0.86f, out rumIcon, UseRum);
            rumCaption = Caption(rumButton, RumCaptionAngle, RumSize);

            targetMark = UIKit.Icon("Target Mark", safe, "Target_Reticle");
            targetMark.color = Palette.Danger;
            targetMark.rectTransform.anchorMin = targetMark.rectTransform.anchorMax = safe.pivot;
            targetMark.rectTransform.sizeDelta = Vector2.one * 120f;
            HideActionPad();
        }

        static Vector2 Around(float degrees, float radius)
        {
            var angle = degrees * Mathf.Deg2Rad;
            return PadCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        GameButton RoundButton(string name, Vector2 at, float size, Color color, string icon, float iconShare, out Image iconImage, Action onClick)
        {
            var rim = UIKit.Solid(name + " Rim", pad, Palette.Ink);
            rim.sprite = UIKit.Circle;
            UIKit.Place(rim.rectTransform, new Vector2(1f, 0f), Vector2.one * 0.5f, at, Vector2.one * (size + 10f));
            var face = UIKit.Solid(name, rim.transform, color, true);
            face.sprite = UIKit.Circle;
            UIKit.Place(face.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * size);
            iconImage = UIKit.Icon("Icon", face.transform, icon);
            UIKit.Place(iconImage.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * size * iconShare);
            var label = UIKit.Text("Label", face.transform, "", 20f, Palette.TextLight);
            label.outlineWidth = 0.25f;
            label.outlineColor = Palette.Ink;
            var button = face.gameObject.AddComponent<GameButton>();
            button.Setup(face, label, color, onClick);
            return button;
        }

        // A small button's name and cost sit just outside it, away from the attack button.
        TextMeshProUGUI Caption(GameButton button, float degrees, float size)
        {
            var caption = button.Label;
            caption.fontSize = 19f;
            caption.characterSpacing = 0f;
            caption.lineSpacing = -20f;
            var angle = degrees * Mathf.Deg2Rad;
            var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (size * 0.5f + 34f);
            UIKit.Place(caption.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, outward, new Vector2(190f, 52f));
            return caption;
        }

        void ShowActionPad(Combatant actor)
        {
            choosingActor = actor;
            ChosenEnemy();
            var member = Array.Find(director.Content.crew, c => c.id == actor.Id);
            for (var i = 0; i < skillSlots.Count; i++)
            {
                var slot = skillSlots[i];
                slot.Skill = null;
                slot.Level = 0;
                if (member?.skills != null && i < member.skills.Length)
                {
                    slot.Skill = director.Content.Skill(member.skills[i].skill);
                    slot.Level = member.skills[i].level;
                }
                else if (i < actor.Skills.Count)
                {
                    slot.Skill = actor.Skills[i];
                }

                var root = slot.Button.transform.parent.gameObject;
                root.SetActive(slot.Skill != null);
                if (slot.Skill == null) continue;
                var learned = actor.Skills.Contains(slot.Skill);
                var affordable = learned && PartyBattle.CanUse(actor, slot.Skill);
                // A skill not yet learned shows as a lock, not as the skill, until its level comes.
                slot.Icon.sprite = KitArt.Icon(slot.Skill.icon);
                slot.Icon.enabled = learned && slot.Icon.sprite != null;
                slot.Icon.color = affordable ? Color.white : new Color(0.45f, 0.45f, 0.5f, learned ? 0.9f : 0.5f);
                slot.Lock.gameObject.SetActive(!learned);
                slot.Button.Interactable = affordable;
                slot.Caption.text = learned ? $"{slot.Skill.name}\n<color=#57A6F0>{slot.Skill.mpCost} MP</color>" : $"LOCKED\nLv {slot.Level}";
                slot.Caption.color = affordable ? Palette.TextLight : Palette.TextMuted;
            }

            var rum = director.Content.Item("rum");
            var bottles = director.Save.ItemCount(rum.id);
            var canDrink = bottles > 0 && AnyAllyHurt();
            rumButton.Interactable = canDrink;
            rumIcon.color = canDrink ? Color.white : new Color(0.45f, 0.45f, 0.5f, 0.8f);
            rumCaption.text = $"Rum\n<color=#F2B134>x{bottles}</color>";
            rumCaption.color = canDrink ? Palette.TextLight : Palette.TextMuted;

            pad.gameObject.SetActive(true);
        }

        void HideActionPad()
        {
            choosingActor = null;
            pad.gameObject.SetActive(false);
            targetMark.gameObject.SetActive(false);
        }

        void UseSkill(SkillSlot slot)
        {
            var skill = slot.Skill;
            if (choosingActor == null || skill == null) return;
            switch (skill.Target)
            {
                case SkillTarget.Enemy:
                    pendingChoice?.Invoke(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = ChosenEnemy() });
                    break;
                case SkillTarget.Ally:
                    pendingChoice?.Invoke(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = NeediestAlly(skill.revive) });
                    break;
                default:
                    pendingChoice?.Invoke(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill });
                    break;
            }
        }

        void UseRum()
        {
            if (choosingActor == null) return;
            pendingChoice?.Invoke(new BattleAction { Kind = BattleActionKind.Item, Item = director.Content.Item("rum"), Target = NeediestAlly(false) });
        }

        // Bounces the fighters, lights the beat on the track and flashes the edges on a sequence's first beat.
        void ShowBeat(bool playing, long index, int inSequence, float pulse)
        {
            var hit = playing ? pulse * pulse * pulse : 0f;
            foreach (var fighter in fighters)
            {
                var standing = !fighter.Fallen && fighter.Combatant.Alive;
                var theirBeat = (index - fighter.BounceOffset) % fighter.BounceEvery == 0;
                var squash = standing && theirBeat ? hit : 0f;
                var spread = 1f + BounceSpread * squash;
                fighter.Model.Root.transform.localScale = new Vector3(spread, 1f - BounceSquash * squash, spread) * CharacterScale;
            }

            for (var i = 0; i < sequenceTrack.Count; i++)
            {
                var pip = sequenceTrack[i];
                pip.gameObject.SetActive(playing);
                var lit = i == inSequence;
                pip.color = lit ? (i == 0 ? Palette.Accent : Palette.TextLight) : Palette.WithAlpha(i == 0 ? Palette.Accent : Palette.TextMuted, 0.35f);
                pip.rectTransform.localScale = Vector3.one * (lit ? 1f + 0.55f * pulse : 1f);
            }

            edgeFlash.color = Palette.WithAlpha(Palette.Accent, playing ? (inSequence == 0 ? DownbeatFlash : BeatFlash) * pulse * pulse : 0f);
        }

        void BuildBeatTrack()
        {
            edgeFlash = new GameObject("Beat Edge Flash", typeof(RectTransform)).AddComponent<Image>();
            edgeFlash.transform.SetParent(safe.parent, false);
            edgeFlash.transform.SetAsFirstSibling();
            edgeFlash.sprite = EdgeSprite();
            edgeFlash.raycastTarget = false;
            UIKit.Fill(edgeFlash.rectTransform);

            // Eight pips under the turn banner, the first one larger and gold: the sequence a turn takes.
            for (var i = 0; i < beat.SequenceBeats; i++)
            {
                var pip = UIKit.Solid("Sequence Beat", safe, Palette.TextMuted);
                pip.sprite = UIKit.Circle;
                var size = i == 0 ? 26f : 18f;
                UIKit.Place(pip.rectTransform, new Vector2(0.5f, 1f), Vector2.one * 0.5f, new Vector2((i - (beat.SequenceBeats - 1) * 0.5f) * 44f, -118f), Vector2.one * size);
                sequenceTrack.Add(pip);
            }
        }

        // Clear in the middle, glowing toward the edges.
        static Sprite EdgeSprite()
        {
            if (edgeSprite != null) return edgeSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Beat Edge", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size * 2f - 1f;
                    var v = (y + 0.5f) / size * 2f - 1f;
                    var edge = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, edge));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            edgeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f);
            return edgeSprite;
        }

        // The pad keeps the beat: its ring pulses on every beat, drains over the window and turns red for the last two.
        void UpdateBeat()
        {
            if (pad == null) return;
            var now = Now();
            var pulse = beat.Pulse(now);
            var inSequence = beat.BeatInSequence(now);
            var index = beat.BeatIndex(now);
            if (index != lastBeat && clockReady)
            {
                lastBeat = index;
                if (inSequence == 0) director.Stage.Shake(DownbeatShake);
                var remaining = windowClose - now;
                if (pad.gameObject.activeSelf && remaining > 0 && remaining <= beat.BeatSeconds * WarningTicks + 0.02) AudioBank.Play(Sfx.Tick, 1f, 0f);
            }
            ShowBeat(clockReady, index, inSequence, pulse);
            if (!pad.gameObject.activeSelf) return;

            var left = Mathf.Clamp01((float)((windowClose - now) / beat.SequenceSeconds));
            var urgent = windowClose - now <= beat.BeatSeconds * 2;
            windowRing.fillAmount = left;
            windowRing.color = urgent ? Palette.Danger : Palette.Accent;
            windowRing.rectTransform.localScale = Vector3.one * (1f + 0.05f * pulse * pulse);
            for (var i = 0; i < beatPips.Count; i++)
            {
                var played = now >= windowOpen + (i + 1) * beat.BeatSeconds;
                var pip = beatPips[i];
                pip.color = played ? Palette.WithAlpha(Palette.Ink, 0.7f) : urgent ? Palette.Danger : Palette.Accent;
                var landing = !played && now >= windowOpen + i * beat.BeatSeconds;
                pip.rectTransform.localScale = Vector3.one * (landing ? 1f + 0.6f * pulse : 1f);
            }

            var target = selectedTarget;
            var show = target != null && target.Alive;
            targetMark.gameObject.SetActive(show);
            if (!show) return;
            var fighter = byCombatant[target];
            var head = fighter.Model.Root.transform.position + Vector3.up * (fighter.Model.HeadHeight * CharacterScale + 0.4f);
            var screen = director.Stage.MainCamera.WorldToScreenPoint(head);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out var local)) targetMark.rectTransform.anchoredPosition = local;
            targetMark.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse);
            targetMark.rectTransform.Rotate(0f, 0f, -40f * Time.unscaledDeltaTime);
        }
    }
}
