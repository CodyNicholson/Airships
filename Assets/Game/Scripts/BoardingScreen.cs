using System;
using System.Collections;
using System.Collections.Generic;
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
        const float BlockMoment = 7f / 24f;

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
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI IntentText;
        }

        readonly List<Fighter> fighters = new List<Fighter>();
        readonly Dictionary<Combatant, Fighter> byCombatant = new Dictionary<Combatant, Fighter>();

        Canvas canvas;
        RectTransform safe;
        RectTransform menuRoot;
        TextMeshProUGUI turnLabel;
        TextMeshProUGUI actionLabel;
        Transform turnArrow;
        Material arrowMaterial;
        Action<BattleAction> pendingChoice;
        Fighter current;
        CommsBar comms;
        DeckSetup setup;
        TextMeshProUGUI sabotageLabel;
        Bar momentumBar;
        TextMeshProUGUI momentumLabel;
        RawImage rain;
        float lightningTimer = 3f;
        readonly HashSet<string> saidTriggers = new HashSet<string>();

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
                var fighter = new Fighter { Combatant = combatant, Model = model, Home = home, HomeRotation = rotation };
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
            AudioBank.SetDanger(raging || health < maximum * DangerHealthShare ? 1f : 0f);
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

            if (current != null)
            {
                turnArrow.gameObject.SetActive(true);
                var height = current.Model.HeadHeight * CharacterScale + 0.55f;
                turnArrow.position = current.Model.Root.transform.position + Vector3.up * (height + Mathf.Sin(time * 5f) * 0.14f);
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

        IEnumerator RunBattle()
        {
            yield return Entrance();
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
                turnLabel.text = $"{actor.Name}'s turn";

                BattleAction action = default;
                if (actor.IsPlayer && !GameDirector.Autoplay)
                {
                    var chosen = false;
                    pendingChoice = a =>
                    {
                        action = a;
                        chosen = true;
                    };
                    ShowCommandMenu(actor);
                    while (!chosen) yield return null;
                    pendingChoice = null;
                    ClearMenu();
                }
                else
                {
                    yield return new WaitForSeconds(0.2f);
                    action = actor.IsPlayer ? ChooseAutoAction(actor) : Battle.ChooseEnemyAction(actor);
                }

                yield return PlayAction(actor, action);
                ActionsResolved++;
                CheckBattleBarks();
                RefreshSabotage();
                RefreshMomentum();
                yield return new WaitForSeconds(0.12f);
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
            if (Battle.RushReady) return new BattleAction { Kind = BattleActionKind.Rush };
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

        IEnumerator PlayAction(Combatant actor, BattleAction action)
        {
            var fighter = byCombatant[actor];
            var isMagic = action.Kind == BattleActionKind.Skill && action.Skill.Kind == SkillKind.Magic;
            var isHeal = action.Kind == BattleActionKind.Skill && action.Skill.Kind == SkillKind.Heal;
            var isRanged = director.Content.Class(actor.ClassId).ranged;
            var label = action.Kind == BattleActionKind.Skill ? action.Skill.name : action.Kind == BattleActionKind.Guard ? "Guard" : action.Kind == BattleActionKind.Rush ? "Boarding Rush" : action.Kind == BattleActionKind.Item ? action.Item.name : "Attack";
            actionLabel.text = label;
            StartCoroutine(HideActionLabel());

            if (action.Kind == BattleActionKind.Rush)
            {
                yield return PlayRush(action);
                yield break;
            }
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

            if (action.Kind == BattleActionKind.Guard)
            {
                var guarded = Battle.Submit(action);
                Fx.Puff(fighter.Model.Root.transform.position + Vector3.up * 1.5f, Palette.Mana);
                AudioBank.Play(Sfx.GuardUp, 0.8f);
                ShowFloating(fighter, "GUARD", Palette.Mana, 40f);
                fighter.Model.Animation.PlayThenReturn("Block");
                yield return new WaitForSeconds(BlockMoment);
                yield return ApplyResults(guarded);
                yield break;
            }

            // The pattern's last target lands on the attack's strike frame, so reading the swing helps.
            var pattern = ShowTimingPrompt(actor, action, isHeal, isMagic);
            var lead = Mathf.Max(0f, pattern.Length - StrikeMoment);
            if (lead > 0f) yield return new WaitForSeconds(lead);

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

        // The whole crew charges on one prompt.
        IEnumerator PlayRush(BattleAction action)
        {
            director.ScreenFx.Milestone("BOARDING RUSH!", Palette.Accent, Sfx.WarHorn);
            var pattern = TimingSequence.Pick(director.Content.timing, director.EncounterTier, true, patternRandom, lastPatternId);
            lastPatternId = pattern.id;
            timing.Begin(pattern, "ALL HANDS!", Palette.PlayerPaint);
            var lead = Mathf.Max(0f, pattern.Length - StrikeMoment);
            if (lead > 0f) yield return new WaitForSeconds(lead);
            foreach (var fighter in fighters)
            {
                if (fighter.Combatant.IsPlayer && fighter.Combatant.Alive) fighter.Model.Animation.PlayThenReturn("AttackMelee");
            }
            yield return new WaitForSeconds(StrikeMoment);
            yield return ResolveStrike(action);
            AudioBank.Play(Sfx.Slash, 1f, 0f);
            director.Stage.Shake(0.6f);
            yield return ApplyResults(strikeResults);
            yield return new WaitForSeconds(0.3f);
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

        TimingPattern ShowTimingPrompt(Combatant actor, BattleAction action, bool isHeal, bool isMagic)
        {
            var pattern = TimingSequence.Pick(director.Content.timing, director.EncounterTier, actor.IsPlayer, patternRandom, lastPatternId);
            lastPatternId = pattern.id;
            if (actor.IsPlayer)
            {
                timing.Begin(pattern, isHeal ? "FOCUS!" : isMagic ? "CAST!" : "STRIKE!", isHeal ? Palette.Heal : Palette.PlayerPaint);
                return pattern;
            }

            timing.Begin(pattern, "BLOCK!", Palette.Mana);
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

            var banner = UIKit.Panel("Turn Banner", safe, Palette.WithAlpha(Palette.Panel, 0.9f));
            UIKit.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(640f, 84f));
            turnLabel = UIKit.Text("Turn", banner.transform, "", 40f, Palette.TextLight);
            UIKit.Fill(turnLabel.rectTransform);

            actionLabel = UIKit.Text("Action", safe, "", 54f, Palette.Accent);
            actionLabel.outlineWidth = 0.2f;
            actionLabel.outlineColor = Palette.Ink;
            UIKit.Place(actionLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(800f, 70f));

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
            menuRoot = UIKit.Node("Menu", safe);
            UIKit.Place(menuRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(620f, 500f));
            BuildMomentum();
            timing = TimingPrompt.Create(safe, director.Content.timing);
            comms = CommsBar.Create(safe, director.Content.campaign, -108f);
        }

        void BuildMomentum()
        {
            var panel = UIKit.Panel("Momentum", safe, Palette.WithAlpha(Palette.Panel, 0.9f));
            var y = 28f + 24f + Battle.Party.Count * 69f + 8f;
            UIKit.Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(28f, y), new Vector2(520f, 58f));
            momentumLabel = UIKit.Text("Label", panel.transform, "MOMENTUM", 24f, Palette.TextLight, TextAlignmentOptions.Left);
            UIKit.Place(momentumLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(190f, 40f));
            momentumBar = UIKit.Bar("Bar", panel.transform, Palette.Accent);
            UIKit.Place((RectTransform)momentumBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(210f, 0f), new Vector2(295f, 34f));
            momentumBar.Set(0f, true);
        }

        void RefreshMomentum()
        {
            momentumBar.Set(Battle.Momentum / (float)PartyBattle.MaxMomentum);
            momentumLabel.text = Battle.RushReady ? "RUSH READY!" : "MOMENTUM";
            momentumLabel.color = Battle.RushReady ? Palette.Accent : Palette.TextLight;
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

        void ClearMenu()
        {
            for (var i = menuRoot.childCount - 1; i >= 0; i--) Destroy(menuRoot.GetChild(i).gameObject);
        }

        void ShowCommandMenu(Combatant actor)
        {
            ClearMenu();
            var row = 3;
            if (Battle.RushReady) AddMenuButton(4, "BOARDING RUSH!", Palette.Accent, () => pendingChoice(new BattleAction { Kind = BattleActionKind.Rush }));
            AddMenuButton(row--, "ATTACK", Palette.PlayerPaint, () => ShowEnemyTargets(actor, target =>
                pendingChoice(new BattleAction { Kind = BattleActionKind.Attack, Target = target })));
            AddMenuButton(row--, "SKILLS", Palette.PanelLight, () => ShowSkillMenu(actor));
            var items = AddMenuButton(row--, "ITEMS", Palette.PanelLight, () => ShowItemMenu(actor));
            items.Interactable = HasBattleItems();
            var guard = AddMenuButton(row, "GUARD", Palette.PanelLight, () => pendingChoice(new BattleAction { Kind = BattleActionKind.Guard }));
            AddIcon(guard, "Hud_Defend");
        }

        bool HasBattleItems()
        {
            foreach (var item in director.Content.items)
            {
                if (item.kind != "patch" && director.Save.ItemCount(item.id) > 0) return true;
            }
            return false;
        }

        static void AddIcon(GameButton button, string icon)
        {
            var image = UIKit.Icon("Icon", button.transform, icon);
            UIKit.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), Vector2.one * 68f);
        }

        void ShowItemMenu(Combatant actor)
        {
            ClearMenu();
            var row = 1;
            foreach (var definition in director.Content.items)
            {
                if (definition.kind == "patch" || director.Save.ItemCount(definition.id) <= 0) continue;
                var item = definition;
                var button = AddMenuButton(row++, $"{item.name}  x{director.Save.ItemCount(item.id)}", Palette.PanelLight, () =>
                {
                    if (item.kind == "heal") ShowAllyTargets(actor, target => pendingChoice(new BattleAction { Kind = BattleActionKind.Item, Item = item, Target = target }));
                    else pendingChoice(new BattleAction { Kind = BattleActionKind.Item, Item = item });
                });
                button.Label.fontSize = 34f;
                AddIcon(button, item.icon);
            }
            AddMenuButton(0, "BACK", Palette.Panel, () => ShowCommandMenu(actor));
        }

        void ShowSkillMenu(Combatant actor)
        {
            ClearMenu();
            var row = actor.Skills.Count;
            foreach (var skill in actor.Skills)
            {
                var chosen = skill;
                var button = AddMenuButton(row--, $"{skill.name}  ({skill.mpCost} MP)", Palette.PanelLight, () => ChooseSkillTarget(actor, chosen));
                button.Interactable = PartyBattle.CanUse(actor, skill);
                button.Label.fontSize = 36f;
            }
            AddMenuButton(0, "BACK", Palette.Panel, () => ShowCommandMenu(actor));
        }

        void ChooseSkillTarget(Combatant actor, SkillDef skill)
        {
            switch (skill.Target)
            {
                case SkillTarget.Enemy:
                    ShowEnemyTargets(actor, target => pendingChoice(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = target }));
                    break;
                case SkillTarget.Ally:
                    ShowAllyTargets(actor, target => pendingChoice(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = target }), skill.revive);
                    break;
                default:
                    pendingChoice(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill });
                    break;
            }
        }

        void ShowEnemyTargets(Combatant actor, Action<Combatant> picked)
        {
            ShowTargetList(actor, Battle.Enemies, picked, c => c == Battle.SpareTarget ? $"{c.Name}  (SPARE!)" : $"{c.Name}  ({Mathf.RoundToInt(100f * c.Hp / c.Stats.hp)}%)");
        }

        void ShowAllyTargets(Combatant actor, Action<Combatant> picked, bool includeFallen = false)
        {
            ShowTargetList(actor, Battle.Party, picked, c => c.Alive ? $"{c.Name}  ({c.Hp} HP)" : $"{c.Name}  (DOWN)", includeFallen);
        }

        void ShowTargetList(Combatant actor, IReadOnlyList<Combatant> options, Action<Combatant> picked, Func<Combatant, string> describe, bool includeFallen = false)
        {
            ClearMenu();
            var row = 0;
            foreach (var option in options)
            {
                if (option.Alive || includeFallen) row++;
            }
            foreach (var option in options)
            {
                if (!option.Alive && !includeFallen) continue;
                var target = option;
                var button = AddMenuButton(row--, describe(option), Palette.PanelLight, () => picked(target));
                button.Label.fontSize = 34f;
            }
            AddMenuButton(0, "BACK", Palette.Panel, () => ShowCommandMenu(actor));
        }

        GameButton AddMenuButton(int row, string label, Color color, Action action)
        {
            var button = UIKit.Button(label, menuRoot, label, 44f, color, action);
            UIKit.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, row * 100f), new Vector2(560f, 92f));
            return button;
        }
    }
}
