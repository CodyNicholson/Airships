using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public class Combatant
    {
        public string Id;
        public string Name;
        public string ClassId;
        public int Level;
        public bool IsPlayer;
        public int Slot;
        public StatBlock Stats;
        public int Hp;
        public int Mp;
        public bool Guarding;
        public List<SkillDef> Skills = new List<SkillDef>();

        public bool Alive => Hp > 0;

        public static Combatant Create(string id, string name, ClassDef cls, StatBlock individual, int level, GameContent content, bool isPlayer, int slot)
        {
            var stats = Growth.StatsAtLevel(individual, cls, level, content.economy.baseStatGrowthInfluence);
            var combatant = new Combatant
            {
                Id = id,
                Name = name,
                ClassId = cls.id,
                Level = level,
                IsPlayer = isPlayer,
                Slot = slot,
                Stats = stats,
                Hp = (int)stats.hp,
                Mp = (int)stats.mp,
            };
            foreach (var unlock in cls.skills)
            {
                if (unlock.level <= level) combatant.Skills.Add(content.Skill(unlock.skill));
            }
            return combatant;
        }
    }

    public static class BattleSetup
    {
        public const int MaxPartySize = 4;

        public static List<Combatant> BuildParty(GameContent content, SaveData save)
        {
            var party = new List<Combatant>();
            foreach (var progress in save.crew)
            {
                if (party.Count == MaxPartySize) break;
                var member = Array.Find(content.crew, c => c.id == progress.id);
                if (member == null) continue;
                party.Add(Combatant.Create(member.id, member.name, content.Class(member.classId), member.baseStats, progress.level, content, true, party.Count));
            }
            return party;
        }

        // A captain always leads. Crew size and level rise with the encounter tier.
        public static List<Combatant> BuildEnemies(GameContent content, int tier)
        {
            var level = 1 + tier;
            var enemies = new List<Combatant>();
            var captain = content.enemies[0];
            enemies.Add(Combatant.Create(captain.id, captain.name, content.Class(captain.classId), captain.baseStats, level, content, false, 0));

            var eligible = Array.FindAll(content.enemies, e => e != captain && e.minTier <= tier);
            var crewCount = tier >= 3 ? 3 : 2;
            for (var i = 0; i < crewCount; i++)
            {
                var enemy = eligible[i % eligible.Length];
                enemies.Add(Combatant.Create($"{enemy.id}_{i}", enemy.name, content.Class(enemy.classId), enemy.baseStats, level, content, false, enemies.Count));
            }
            return enemies;
        }
    }

    public enum BattleActionKind { Attack, Skill, Guard }

    public struct BattleAction
    {
        public BattleActionKind Kind;
        public SkillDef Skill;
        public Combatant Target; // Ignored by skills that hit a whole side.
    }

    public enum BattleEventKind { Damage, Heal, Guard, Defeated, MpSpent }

    public struct BattleEvent
    {
        public BattleEventKind Kind;
        public Combatant Source;
        public Combatant Target;
        public int Amount;
        public bool Critical;
        public string Label;
    }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    // Turn-based party combat with no Unity dependency. Turn order is rebuilt each round from speed.
    public class PartyBattle
    {
        public const float CriticalChance = 0.08f;
        public const float CriticalMultiplier = 1.5f;
        public const float BasicAttackPower = 1f;
        public const float GuardMultiplier = 0.5f;

        readonly List<Combatant> party;
        readonly List<Combatant> enemies;
        readonly List<Combatant> turnOrder = new List<Combatant>();
        readonly GameRandom random;
        int turnIndex;

        public IReadOnlyList<Combatant> Party => party;
        public IReadOnlyList<Combatant> Enemies => enemies;
        public IReadOnlyList<Combatant> TurnOrder => turnOrder;
        public int Round { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public Combatant Current { get; private set; }

        public PartyBattle(List<Combatant> party, List<Combatant> enemies, int seed)
        {
            if (party.Count == 0 || party.Count > 4) throw new ArgumentException("A party has one to four members", nameof(party));
            this.party = party;
            this.enemies = enemies;
            random = new GameRandom(seed);
            BeginRound();
        }

        public bool AwaitingPlayer => Outcome == BattleOutcome.Ongoing && Current != null && Current.IsPlayer;

        public List<Combatant> Opponents(Combatant actor) => actor.IsPlayer ? enemies : party;
        public List<Combatant> Allies(Combatant actor) => actor.IsPlayer ? party : enemies;

        public static bool CanUse(Combatant actor, SkillDef skill) => actor.Mp >= skill.mpCost;

        // Resolves the current actor's action, then advances to the next living actor.
        public List<BattleEvent> Submit(BattleAction action)
        {
            if (Outcome != BattleOutcome.Ongoing) throw new InvalidOperationException("The battle is over");
            var actor = Current;
            var results = new List<BattleEvent>();
            actor.Guarding = false;

            switch (action.Kind)
            {
                case BattleActionKind.Attack:
                    ResolveHit(actor, action.Target, BasicAttackPower, SkillKind.Physical, "Attack", results);
                    break;
                case BattleActionKind.Guard:
                    actor.Guarding = true;
                    results.Add(new BattleEvent { Kind = BattleEventKind.Guard, Source = actor, Target = actor, Label = "Guard" });
                    break;
                case BattleActionKind.Skill:
                    ResolveSkill(actor, action, results);
                    break;
            }

            UpdateOutcome();
            if (Outcome == BattleOutcome.Ongoing) Advance();
            return results;
        }

        // Picks the action for a non-player combatant.
        public BattleAction ChooseEnemyAction(Combatant actor)
        {
            var targets = Opponents(actor).FindAll(c => c.Alive);
            var allies = Allies(actor).FindAll(c => c.Alive);

            // Heal a badly hurt ally when able.
            foreach (var skill in actor.Skills)
            {
                if (skill.Kind != SkillKind.Heal || !CanUse(actor, skill)) continue;
                var hurt = allies.Find(c => c.Hp < c.Stats.hp * 0.4f);
                if (hurt != null) return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = hurt };
            }

            // Use an offensive skill about a third of the time.
            if (actor.Skills.Count > 0 && random.Value() < 0.35f)
            {
                var skill = actor.Skills[random.Range(0, actor.Skills.Count)];
                if (skill.Kind != SkillKind.Heal && CanUse(actor, skill))
                {
                    return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = targets[random.Range(0, targets.Count)] };
                }
            }

            // Otherwise favor the weakest target half the time.
            var target = targets[random.Range(0, targets.Count)];
            if (random.Value() < 0.5f)
            {
                foreach (var candidate in targets)
                {
                    if (candidate.Hp < target.Hp) target = candidate;
                }
            }
            return new BattleAction { Kind = BattleActionKind.Attack, Target = target };
        }

        void ResolveSkill(Combatant actor, BattleAction action, List<BattleEvent> results)
        {
            var skill = action.Skill;
            if (!CanUse(actor, skill)) throw new InvalidOperationException($"{actor.Name} lacks the MP for {skill.name}");
            actor.Mp -= skill.mpCost;
            results.Add(new BattleEvent { Kind = BattleEventKind.MpSpent, Source = actor, Target = actor, Amount = skill.mpCost, Label = skill.name });

            foreach (var target in SkillTargets(actor, skill, action.Target))
            {
                if (skill.Kind == SkillKind.Heal) ResolveHeal(actor, target, skill, results);
                else ResolveHit(actor, target, skill.power, skill.Kind, skill.name, results);
            }
        }

        List<Combatant> SkillTargets(Combatant actor, SkillDef skill, Combatant chosen)
        {
            switch (skill.Target)
            {
                case SkillTarget.AllEnemies: return Opponents(actor).FindAll(c => c.Alive);
                case SkillTarget.AllAllies: return Allies(actor).FindAll(c => c.Alive);
                default: return new List<Combatant> { chosen };
            }
        }

        void ResolveHit(Combatant actor, Combatant target, float power, SkillKind kind, string label, List<BattleEvent> results)
        {
            var critical = random.Value() < CriticalChance;
            var amount = Damage(actor, target, power, kind, critical, random.Range(0.9f, 1.1f));
            target.Hp = Math.Max(0, target.Hp - amount);
            results.Add(new BattleEvent { Kind = BattleEventKind.Damage, Source = actor, Target = target, Amount = amount, Critical = critical, Label = label });
            if (!target.Alive) results.Add(new BattleEvent { Kind = BattleEventKind.Defeated, Source = actor, Target = target });
        }

        void ResolveHeal(Combatant actor, Combatant target, SkillDef skill, List<BattleEvent> results)
        {
            var amount = (int)Math.Round(actor.Stats.spirit * skill.power * random.Range(0.95f, 1.05f));
            var healed = Math.Min(amount, (int)target.Stats.hp - target.Hp);
            target.Hp += healed;
            results.Add(new BattleEvent { Kind = BattleEventKind.Heal, Source = actor, Target = target, Amount = healed, Label = skill.name });
        }

        public static int Damage(Combatant actor, Combatant target, float power, SkillKind kind, bool critical, float variance)
        {
            var offense = kind == SkillKind.Magic ? actor.Stats.spirit : actor.Stats.attack;
            var defense = kind == SkillKind.Magic ? target.Stats.spirit * 0.5f : target.Stats.defense * 0.5f;
            var amount = (offense * power * 1.2f - defense) * variance;
            if (critical) amount *= CriticalMultiplier;
            if (target.Guarding) amount *= GuardMultiplier;
            return Math.Max(1, (int)Math.Round(amount));
        }

        void UpdateOutcome()
        {
            if (!party.Exists(c => c.Alive)) Outcome = BattleOutcome.Defeat;
            else if (!enemies.Exists(c => c.Alive)) Outcome = BattleOutcome.Victory;
        }

        void BeginRound()
        {
            Round++;
            turnOrder.Clear();
            turnOrder.AddRange(party.FindAll(c => c.Alive));
            turnOrder.AddRange(enemies.FindAll(c => c.Alive));

            // Decorate with a rolled initiative first so the sort never re-rolls mid-comparison.
            var initiative = new Dictionary<Combatant, float>();
            foreach (var combatant in turnOrder) initiative[combatant] = combatant.Stats.speed * random.Range(0.9f, 1.1f);
            turnOrder.Sort((a, b) => initiative[b].CompareTo(initiative[a]));

            turnIndex = -1;
            Advance();
        }

        void Advance()
        {
            for (var i = turnIndex + 1; i < turnOrder.Count; i++)
            {
                if (!turnOrder[i].Alive) continue;
                turnIndex = i;
                Current = turnOrder[i];
                return;
            }
            BeginRound();
        }
    }
}
