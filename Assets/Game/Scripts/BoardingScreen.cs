using System;
using System.Collections;
using System.Collections.Generic;
using Airships.Core;
using TMPro;
using UnityEngine;

namespace Airships.Game
{
    // The party-of-four turn-based battle on the enemy's deck. All rules live in PartyBattle.
    public class BoardingScreen : MonoBehaviour
    {
        const float CharacterScale = 1.25f;
        const float FrontLineX = 2.6f;
        const float LineSpacingX = 1.35f;

        // The kit's attack clips land their blow or shot on this frame of 24 per second.
        const float StrikeMoment = 14f / 24f;
        const float BlockMoment = 7f / 24f;

        public PartyBattle Battle { get; private set; }
        public int ActionsResolved { get; private set; }

        GameDirector director;
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

        void Start()
        {
            director = GameDirector.Instance;
            var party = BattleSetup.BuildParty(director.Content, director.Save);
            var enemies = BattleSetup.BuildEnemies(director.Content, director.EncounterTier);
            Battle = new PartyBattle(party, enemies, director.Seed);

            deckModel = Models.CreateDeck(transform, KitArt.Obsidian);
            deck = deckModel.Root.transform;
            // Three-quarter facing, so faces and silhouettes read from the battle camera.
            BuildFighters(party, "party_slot_", 125f);
            BuildFighters(enemies, "enemy_slot_", -125f);

            cameraRig = new GameObject("Battle Camera Rig").transform;
            cameraRig.SetParent(transform, false);
            lookTarget = new GameObject("Battle Look").transform;
            lookTarget.SetParent(transform, false);
            lookTarget.position = new Vector3(0f, 0.5f, 0f);
            director.Stage.FrameTarget(cameraRig, lookTarget, new Vector3(0f, 3.4f, -9.6f), 46f, 0f);

            var arrowKit = new MeshKit();
            arrowKit.Cylinder(new Vector3(0f, 0.55f, 0f), Vector3.zero, 0.3f, 0f, 4, Palette.Accent);
            arrowMaterial = Models.NewLitMaterial("Turn Arrow");
            turnArrow = Models.Attach("Turn Arrow", transform, arrowKit.Build("Arrow"), arrowMaterial, false).transform;
            turnArrow.localScale = Vector3.one * 0.6f;

            BuildHud();
            StartCoroutine(RunBattle());
        }

        void BuildFighters(List<Combatant> combatants, string slotPrefix, float yaw)
        {
            for (var i = 0; i < combatants.Count; i++)
            {
                var combatant = combatants[i];
                var model = Models.CreateCharacter(combatant.Name, combatant.ClassId, deck);

                // Enemy leaders are tall, so they stand at the back. The kit's slots set each fighter's
                // depth, and a diagonal line across the deck keeps everyone visible from this camera.
                var slot = yaw < 0f ? combatants.Count - 1 - i : i;
                var home = deckModel.Spot($"{slotPrefix}{slot + 1}");
                home.x = Mathf.Sign(home.x) * (FrontLineX + slot * LineSpacingX);
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                model.Root.transform.localPosition = home;
                model.Root.transform.localRotation = rotation;
                model.Root.transform.localScale = Vector3.one * CharacterScale;
                model.Animation.Loop("Idle", fighters.Count * 0.37f);
                var fighter = new Fighter { Combatant = combatant, Model = model, Home = home, HomeRotation = rotation };
                fighters.Add(fighter);
                byCombatant[combatant] = fighter;
            }
        }

        void Update()
        {
            time += Time.deltaTime;

            // The deck rocks gently.
            deck.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.6f) * 0.8f, 0f, Mathf.Sin(time * 0.45f) * 1.2f);
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

        IEnumerator RunBattle()
        {
            yield return new WaitForSeconds(0.7f);
            while (Battle.Outcome == BattleOutcome.Ongoing)
            {
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
                    yield return new WaitForSeconds(0.45f);
                    action = actor.IsPlayer ? ChooseAutoAction(actor) : Battle.ChooseEnemyAction(actor);
                }

                yield return PlayAction(actor, action);
                ActionsResolved++;
                yield return new WaitForSeconds(0.22f);
            }

            current = null;
            var won = Battle.Outcome == BattleOutcome.Victory;
            turnLabel.text = "";
            director.ScreenFx.Milestone(won ? "VICTORY!" : "DEFEATED", won ? Palette.Accent : Palette.Danger);
            foreach (var fighter in fighters)
            {
                if (!fighter.Fallen && fighter.Combatant.Alive) fighter.Model.Animation.Loop("Victory");
            }
            if (won) RevealPlunder();
            yield return new WaitForSeconds(2.4f);
            director.OnBattleFinished(won);
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
            AudioBank.Play(Sfx.Sting, 0.7f);
        }

        // A plain policy for automated runs: heal when hurt, otherwise use the strongest attack.
        BattleAction ChooseAutoAction(Combatant actor)
        {
            var allies = Battle.Party;
            Combatant weakest = null;
            foreach (var ally in allies)
            {
                if (ally.Alive && (weakest == null || ally.Hp / ally.Stats.hp < weakest.Hp / weakest.Stats.hp)) weakest = ally;
            }
            foreach (var skill in actor.Skills)
            {
                if (skill.Kind == SkillKind.Heal && PartyBattle.CanUse(actor, skill) && weakest.Hp < weakest.Stats.hp * 0.5f)
                {
                    return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = weakest };
                }
            }
            var target = Battle.Enemies[0];
            foreach (var enemy in Battle.Enemies)
            {
                if (enemy.Alive && (!target.Alive || enemy.Hp < target.Hp)) target = enemy;
            }
            foreach (var skill in actor.Skills)
            {
                if (skill.Kind != SkillKind.Heal && PartyBattle.CanUse(actor, skill) && actor.Mp > skill.mpCost * 2)
                {
                    return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = target };
                }
            }
            return new BattleAction { Kind = BattleActionKind.Attack, Target = target };
        }

        IEnumerator PlayAction(Combatant actor, BattleAction action)
        {
            var fighter = byCombatant[actor];
            var isMagic = action.Kind == BattleActionKind.Skill && action.Skill.Kind == SkillKind.Magic;
            var isHeal = action.Kind == BattleActionKind.Skill && action.Skill.Kind == SkillKind.Heal;
            var isRanged = actor.ClassId == "gunner" || actor.ClassId == "bombardier";
            var label = action.Kind == BattleActionKind.Skill ? action.Skill.name : action.Kind == BattleActionKind.Guard ? "Guard" : "Attack";
            actionLabel.text = label;
            StartCoroutine(HideActionLabel());

            var results = Battle.Submit(action);

            if (action.Kind == BattleActionKind.Guard)
            {
                Fx.Puff(fighter.Model.Root.transform.position + Vector3.up * 1.5f, Palette.Mana);
                AudioBank.Play(Sfx.Confirm, 0.6f);
                ShowFloating(fighter, "GUARD", Palette.Mana, 40f);
                fighter.Model.Animation.PlayThenReturn("Block");
                yield return new WaitForSeconds(BlockMoment);
            }
            else if (isHeal || isMagic || isRanged)
            {
                yield return CastOrShoot(fighter, action, isMagic, isHeal);
            }
            else
            {
                var target = action.Target ?? FirstTarget(results);
                yield return Lunge(fighter, target != null ? byCombatant[target] : null, results);
                yield break;
            }

            yield return ApplyResults(results);
        }

        static Combatant FirstTarget(List<BattleEvent> results)
        {
            foreach (var result in results)
            {
                if (result.Kind == BattleEventKind.Damage) return result.Target;
            }
            return null;
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

        IEnumerator CastOrShoot(Fighter fighter, BattleAction action, bool isMagic, bool isHeal)
        {
            if (action.Target != null) Face(fighter, byCombatant[action.Target]);
            fighter.Model.Animation.PlayThenReturn("AttackRanged");
            yield return new WaitForSeconds(StrikeMoment);

            var origin = fighter.Model.Root.transform.position + Vector3.up * 1.6f;
            if (isHeal)
            {
                AudioBank.Play(Sfx.Heal);
                Fx.Burst(origin, Palette.Heal, 12, 2f, 0.5f, 0.6f, 1.5f);
            }
            else
            {
                AudioBank.Play(isMagic ? Sfx.HeavyCannon : Sfx.Cannon, isMagic ? 0.55f : 0.7f);
                var color = isMagic ? Palette.Storm : Palette.Spark;
                Fx.Burst(origin + fighter.Model.Root.transform.forward * 1.5f, color, 12, 4f, 0.8f, 0.3f);
                yield return ShootProjectile(origin, action, color);
            }
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

        IEnumerator Lunge(Fighter fighter, Fighter target, List<BattleEvent> results)
        {
            var root = fighter.Model.Root.transform;
            var home = fighter.Home;
            var strikePoint = target != null
                ? Vector3.Lerp(home, target.Home, 0.72f)
                : home + (fighter.Combatant.IsPlayer ? Vector3.right : Vector3.left) * 3f;

            Face(fighter, target);
            fighter.Model.Animation.PlayThenReturn("AttackMelee");
            yield return Ease.Tween(StrikeMoment, t => root.localPosition = Vector3.LerpUnclamped(home, strikePoint, t), Ease.OutCubic, false);
            AudioBank.Play(Sfx.Slash, 0.9f);
            yield return ApplyResults(results);
            yield return Ease.Tween(0.28f, t => root.localPosition = Vector3.LerpUnclamped(strikePoint, home, t), Ease.OutCubic, false);
            root.localRotation = fighter.HomeRotation;
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
            Fx.DeckHit(position, result.Critical);
            AudioBank.Play(result.Critical ? Sfx.HeavyHit : Sfx.Hit, 0.85f);
            ShowFloating(fighter, result.Critical ? $"{result.Amount}!" : result.Amount.ToString(), result.Critical ? Palette.Accent : Palette.TextLight, result.Critical ? 76f : 58f);
            if (fighter.Combatant.Alive) fighter.Model.Animation.PlayThenReturn("Hit");
            director.Stage.Shake(result.Critical ? 0.28f : 0.12f);
            if (result.Critical) GameTime.HitStop(0.05f);
        }

        void ShowFloating(Fighter fighter, string value, Color color, float size)
        {
            var position = fighter.Model.Root.transform.position + Vector3.up * 3f;
            director.ScreenFx.FloatingNumber(position, director.Stage.MainCamera, value, color, size);
        }

        void Fall(Fighter fighter)
        {
            fighter.Fallen = true;
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
            menuRoot = UIKit.Node("Menu", safe);
            UIKit.Place(menuRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(620f, 500f));
        }

        void BuildPartyPanel()
        {
            var panel = UIKit.Panel("Party", safe, Palette.WithAlpha(Palette.Panel, 0.9f));
            UIKit.Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(28f, 28f), new Vector2(860f, 300f));

            for (var i = 0; i < Battle.Party.Count; i++)
            {
                var fighter = byCombatant[Battle.Party[i]];
                var y = -18f - i * 69f;
                var name = UIKit.Text("Name", panel.transform, fighter.Combatant.Name, 30f, Palette.TextLight, TextAlignmentOptions.Left);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, y), new Vector2(250f, 56f));

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
                var name = UIKit.Text("Name", panel.transform, fighter.Combatant.Name, 26f, Palette.TextLight, TextAlignmentOptions.Left);
                UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(270f, 56f));
                fighter.EnemyBar = UIKit.Bar("HP", panel.transform, Palette.Danger);
                UIKit.Place((RectTransform)fighter.EnemyBar.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, y - 10f), new Vector2(250f, 34f));
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
            AddMenuButton(2, "ATTACK", Palette.PlayerPaint, () => ShowEnemyTargets(actor, target =>
                pendingChoice(new BattleAction { Kind = BattleActionKind.Attack, Target = target })));
            AddMenuButton(1, "SKILLS", Palette.PanelLight, () => ShowSkillMenu(actor));
            AddMenuButton(0, "GUARD", Palette.PanelLight, () => pendingChoice(new BattleAction { Kind = BattleActionKind.Guard }));
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
                    ShowAllyTargets(actor, target => pendingChoice(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = target }));
                    break;
                default:
                    pendingChoice(new BattleAction { Kind = BattleActionKind.Skill, Skill = skill });
                    break;
            }
        }

        void ShowEnemyTargets(Combatant actor, Action<Combatant> picked)
        {
            ShowTargetList(actor, Battle.Enemies, picked, c => $"{c.Name}  ({Mathf.RoundToInt(100f * c.Hp / c.Stats.hp)}%)");
        }

        void ShowAllyTargets(Combatant actor, Action<Combatant> picked)
        {
            ShowTargetList(actor, Battle.Party, picked, c => $"{c.Name}  ({c.Hp} HP)");
        }

        void ShowTargetList(Combatant actor, IReadOnlyList<Combatant> options, Action<Combatant> picked, Func<Combatant, string> describe)
        {
            ClearMenu();
            var row = 0;
            foreach (var option in options)
            {
                if (option.Alive) row++;
            }
            foreach (var option in options)
            {
                if (!option.Alive) continue;
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
