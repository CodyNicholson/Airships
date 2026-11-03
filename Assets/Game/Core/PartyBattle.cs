using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public class Combatant
    {
        public string Id;
        public string Name;
        public string ClassId;
        public string Look; // A class whose model to wear, if not its own.
        public int Level;
        public bool IsPlayer;
        public int Slot;
        public StatBlock Stats;
        public int Hp;
        public int Mp;
        public bool Guarding;
        public List<SkillDef> Skills = new List<SkillDef>();
        public int BurnTurns;
        public int StunTurns;
        public int ExposedTurns;
        public int HasteTurns;
        public float EnrageAt;
        public bool Enraged;

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

        // Adds the skills only this person knows.
        public void AddSignature(SkillUnlock[] signature, GameContent content)
        {
            if (signature == null) return;
            foreach (var unlock in signature)
            {
                if (unlock.level <= Level) Skills.Add(content.Skill(unlock.skill));
            }
        }
    }

    public static class BattleSetup
    {
        public const int MaxPartySize = 3;

        public static List<Combatant> BuildParty(GameContent content, SaveData save)
        {
            var party = new List<Combatant>();
            foreach (var id in save.party)
            {
                if (party.Count == MaxPartySize) break;
                var member = Array.Find(content.crew, c => c.id == id);
                var progress = save.crew.Find(c => c.id == id);
                if (member == null || progress == null) continue;
                var combatant = Combatant.Create(member.id, member.name, content.Class(member.classId), member.baseStats, progress.level, content, true, party.Count);
                combatant.Look = member.look;
                combatant.AddSignature(member.signature, content);
                party.Add(combatant);
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
            var crewCount = tier >= 3 ? 2 : 1;
            for (var i = 0; i < crewCount; i++)
            {
                var enemy = eligible[i % eligible.Length];
                enemies.Add(Combatant.Create($"{enemy.id}_{i}", enemy.name, content.Class(enemy.classId), enemy.baseStats, level, content, false, enemies.Count));
            }
            return enemies;
        }
    }

    public enum BattleActionKind { Attack, Skill, Guard, Rush, Item }

    // How well a timed touch prompt was hit. Missing deals less damage or takes more.
    public enum TimingGrade { Miss, Good, Perfect }

    public struct BattleAction
    {
        public BattleActionKind Kind;
        public SkillDef Skill;
        public Combatant Target; // Ignored by skills that hit a whole side.
        public TimingGrade Timing; // The attacker's prompt on a party turn, the defender's on an enemy turn.
        public ItemDef Item;
    }

    public enum BattleEventKind { Damage, Heal, Guard, Defeated, MpSpent, Status, Stunned, Burned, Counter, Enraged, Revived, Rush }

    public struct BattleEvent
    {
        public BattleEventKind Kind;
        public Combatant Source;
        public Combatant Target;
        public int Amount;
        public bool Critical;
        public string Label;
        public TimingGrade Timing;
    }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    // Turn-based party combat with no Unity dependency. Turn order is rebuilt each round from speed.
    public class PartyBattle
    {
        public const float CriticalChance = 0.08f;
        public const float CriticalMultiplier = 1.5f;
        public const float BasicAttackPower = 1f;
        public const float GuardMultiplier = 0.5f;
        public const float ExposedMultiplier = 1.5f;
        public const float HasteSpeedScale = 1.5f;
        public const float BurnShare = 0.07f;
        public const float EnrageScale = 1.3f;
        public const float GuardMpShare = 0.2f;
        public const float CounterPower = 0.7f;
        public const float RushPower = 0.85f;
        public const float ReviveHealth = 0.4f;
        public const int MaxMomentum = 6;

        readonly List<Combatant> party;
        readonly List<Combatant> enemies;
        readonly List<Combatant> turnOrder = new List<Combatant>();
        readonly GameRandom random;
        readonly TimingDef timing;
        readonly Dictionary<Combatant, BattleAction> intents = new Dictionary<Combatant, BattleAction>();
        readonly List<BattleEvent> turnStartEvents = new List<BattleEvent>();
        int turnIndex;

        // Filled by landing timed prompts. When full, the whole crew can charge together.
        public int Momentum { get; private set; }
        public bool RushReady => Momentum >= MaxMomentum;

        public IReadOnlyList<Combatant> Party => party;
        public IReadOnlyList<Combatant> Enemies => enemies;
        public IReadOnlyList<Combatant> TurnOrder => turnOrder;
        public int Round { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public Combatant Current { get; private set; }

        // Above zero, the enemy gives up once this many rounds have passed, because charges are set below deck.
        public int SurrenderAfterRound { get; set; }
        public bool Surrendered { get; private set; }

        // A combatant to beat without killing. Defeating everyone else wins, and felling them loses.
        public Combatant SpareTarget { get; set; }
        public bool SpareKilled { get; private set; }

        public PartyBattle(List<Combatant> party, List<Combatant> enemies, int seed, TimingDef timing = null, int maxParty = BattleSetup.MaxPartySize)
        {
            if (party.Count == 0 || party.Count > maxParty) throw new ArgumentException($"A party has one to {maxParty} members", nameof(party));
            this.party = party;
            this.enemies = enemies;
            this.timing = timing;
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
            results.AddRange(turnStartEvents);
            turnStartEvents.Clear();
            actor.Guarding = false;

            switch (action.Kind)
            {
                case BattleActionKind.Attack:
                    ResolveHit(actor, action.Target, BasicAttackPower, SkillKind.Physical, "Attack", action.Timing, results);
                    break;
                case BattleActionKind.Guard:
                    actor.Guarding = true;
                    actor.Mp = Math.Min((int)actor.Stats.mp, actor.Mp + (int)(actor.Stats.mp * GuardMpShare));
                    results.Add(new BattleEvent { Kind = BattleEventKind.Guard, Source = actor, Target = actor, Label = "Guard" });
                    break;
                case BattleActionKind.Skill:
                    ResolveSkill(actor, action, results);
                    break;
                case BattleActionKind.Rush:
                    ResolveRush(action, results);
                    break;
                case BattleActionKind.Item:
                    ResolveItem(actor, action, results);
                    break;
            }

            GainMomentum(actor, action);
            CheckEnrage(results);
            UpdateOutcome();
            if (Outcome == BattleOutcome.Ongoing) Advance();
            return results;
        }

        // Events from the start of the coming turn, such as burns. They arrive with the next result too.
        public List<BattleEvent> TakeTurnStartEvents()
        {
            var taken = new List<BattleEvent>(turnStartEvents);
            turnStartEvents.Clear();
            return taken;
        }

        void GainMomentum(Combatant actor, BattleAction action)
        {
            if (timing == null || action.Kind == BattleActionKind.Rush || action.Kind == BattleActionKind.Guard || action.Kind == BattleActionKind.Item) return;
            var gain = action.Timing == TimingGrade.Perfect ? 2 : action.Timing == TimingGrade.Good ? 1 : 0;
            if (!actor.IsPlayer) gain = action.Timing == TimingGrade.Perfect ? 1 : 0;
            Momentum = Math.Min(MaxMomentum, Momentum + gain);
        }

        // Everyone charges at once: a strike from each living crew member against every enemy.
        void ResolveRush(BattleAction action, List<BattleEvent> results)
        {
            Momentum = 0;
            results.Add(new BattleEvent { Kind = BattleEventKind.Rush, Label = "Boarding Rush" });
            foreach (var member in party.FindAll(c => c.Alive))
            {
                foreach (var enemy in enemies.FindAll(c => c.Alive && c != SpareTarget))
                {
                    ResolveHit(member, enemy, RushPower, SkillKind.Physical, "Boarding Rush", action.Timing, results);
                }
            }
        }

        void ResolveItem(Combatant actor, BattleAction action, List<BattleEvent> results)
        {
            var item = action.Item;
            if (item.kind == "heal")
            {
                var target = action.Target ?? actor;
                var healed = Math.Min((int)(target.Stats.hp * item.power), (int)target.Stats.hp - target.Hp);
                target.Hp += healed;
                results.Add(new BattleEvent { Kind = BattleEventKind.Heal, Source = actor, Target = target, Amount = healed, Label = item.name });
                return;
            }
            foreach (var enemy in enemies.FindAll(c => c.Alive && c != SpareTarget))
            {
                var amount = Math.Max(1, (int)(enemy.Stats.hp * item.power));
                enemy.Hp = Math.Max(0, enemy.Hp - amount);
                results.Add(new BattleEvent { Kind = BattleEventKind.Damage, Source = actor, Target = enemy, Amount = amount, Label = item.name });
                if (!enemy.Alive) results.Add(new BattleEvent { Kind = BattleEventKind.Defeated, Source = actor, Target = enemy });
            }
        }

        void CheckEnrage(List<BattleEvent> results)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive || enemy.Enraged || enemy.EnrageAt <= 0f || enemy.Hp > enemy.Stats.hp * enemy.EnrageAt) continue;
                enemy.Enraged = true;
                enemy.Stats.attack *= EnrageScale;
                enemy.Stats.speed *= EnrageScale;
                results.Add(new BattleEvent { Kind = BattleEventKind.Enraged, Source = enemy, Target = enemy, Label = "Enraged" });
            }
        }

        // What this enemy has announced for its turn, so the player can answer it.
        public bool TryGetIntent(Combatant actor, out BattleAction action) => intents.TryGetValue(actor, out action);

        void PlanIntents()
        {
            intents.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy.Alive) intents[enemy] = DecideEnemyAction(enemy);
            }
        }

        // The action a non-player combatant takes: what it announced, unless its target has since fallen.
        public BattleAction ChooseEnemyAction(Combatant actor)
        {
            if (intents.TryGetValue(actor, out var planned) && (planned.Target == null || planned.Target.Alive) && (planned.Skill == null || CanUse(actor, planned.Skill))) return planned;
            return DecideEnemyAction(actor);
        }

        BattleAction DecideEnemyAction(Combatant actor)
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
                if (skill.revive && !target.Alive)
                {
                    target.Hp = Math.Max(1, (int)(target.Stats.hp * ReviveHealth));
                    results.Add(new BattleEvent { Kind = BattleEventKind.Revived, Source = actor, Target = target, Amount = target.Hp, Label = skill.name });
                    continue;
                }
                if (skill.Kind == SkillKind.Heal)
                {
                    ResolveHeal(actor, target, skill, action.Timing, results);
                }
                else
                {
                    var hits = Math.Max(1, skill.hits);
                    for (var i = 0; i < hits && target.Alive; i++) ResolveHit(actor, target, skill.power, skill.Kind, skill.name, action.Timing, results);
                }
                ApplyStatus(actor, target, skill, action.Timing, results);
            }
        }

        // A better prompt makes a status more likely to land.
        void ApplyStatus(Combatant actor, Combatant target, SkillDef skill, TimingGrade grade, List<BattleEvent> results)
        {
            if (string.IsNullOrEmpty(skill.status) || !target.Alive) return;
            var chance = skill.statusChance;
            if (actor.IsPlayer && grade == TimingGrade.Perfect) chance += 0.25f;
            if (actor.IsPlayer && grade == TimingGrade.Miss) chance -= 0.25f;
            if (!actor.IsPlayer && grade == TimingGrade.Perfect) chance -= 0.4f;
            if (random.Value() >= chance) return;
            switch (skill.status)
            {
                case "burn": target.BurnTurns = Math.Max(target.BurnTurns, skill.statusTurns); break;
                case "stun": target.StunTurns = Math.Max(target.StunTurns, skill.statusTurns); break;
                case "exposed": target.ExposedTurns = Math.Max(target.ExposedTurns, skill.statusTurns); break;
                case "haste": target.HasteTurns = Math.Max(target.HasteTurns, skill.statusTurns); break;
                default: throw new InvalidOperationException($"Unknown status '{skill.status}'");
            }
            results.Add(new BattleEvent { Kind = BattleEventKind.Status, Source = actor, Target = target, Label = skill.status });
        }

        // A party member's prompt boosts what they deal, and a defender's reduces what they take.
        float TimingMultiplier(Combatant actor, TimingGrade grade)
        {
            if (timing == null) return 1f;
            if (actor.IsPlayer) return grade == TimingGrade.Perfect ? timing.attackPerfect : grade == TimingGrade.Good ? timing.attackGood : timing.attackMiss;
            return grade == TimingGrade.Perfect ? timing.defendPerfect : grade == TimingGrade.Good ? timing.defendGood : timing.defendMiss;
        }

        List<Combatant> SkillTargets(Combatant actor, SkillDef skill, Combatant chosen)
        {
            switch (skill.Target)
            {
                case SkillTarget.AllEnemies: return Opponents(actor).FindAll(c => c.Alive && (!actor.IsPlayer || c != SpareTarget));
                case SkillTarget.AllAllies: return Allies(actor).FindAll(c => c.Alive);
                default: return new List<Combatant> { chosen };
            }
        }

        void ResolveHit(Combatant actor, Combatant target, float power, SkillKind kind, string label, TimingGrade grade, List<BattleEvent> results)
        {
            var critical = random.Value() < CriticalChance;
            var multiplier = TimingMultiplier(actor, grade);
            if (target.ExposedTurns > 0) multiplier *= ExposedMultiplier;
            var amount = Damage(actor, target, power, kind, critical, random.Range(0.9f, 1.1f), multiplier);
            target.Hp = Math.Max(0, target.Hp - amount);
            results.Add(new BattleEvent { Kind = BattleEventKind.Damage, Source = actor, Target = target, Amount = amount, Critical = critical, Label = label, Timing = grade });
            if (!target.Alive)
            {
                results.Add(new BattleEvent { Kind = BattleEventKind.Defeated, Source = actor, Target = target });
                return;
            }

            // A perfect block from a guarding crew member turns the blow straight back.
            if (timing != null && !actor.IsPlayer && target.IsPlayer && target.Guarding && grade == TimingGrade.Perfect && actor.Alive)
            {
                var counter = Damage(target, actor, CounterPower, SkillKind.Physical, false, 1f);
                actor.Hp = Math.Max(0, actor.Hp - counter);
                results.Add(new BattleEvent { Kind = BattleEventKind.Counter, Source = target, Target = actor, Amount = counter, Label = "Counter" });
                if (!actor.Alive) results.Add(new BattleEvent { Kind = BattleEventKind.Defeated, Source = target, Target = actor });
            }
        }

        void ResolveHeal(Combatant actor, Combatant target, SkillDef skill, TimingGrade grade, List<BattleEvent> results)
        {
            // A missed heal restores the normal amount, because the penalty is for damage.
            var multiplier = grade == TimingGrade.Miss ? 1f : TimingMultiplier(actor, grade);
            var amount = (int)Math.Round(actor.Stats.spirit * skill.power * random.Range(0.95f, 1.05f) * multiplier);
            var healed = Math.Min(amount, (int)target.Stats.hp - target.Hp);
            target.Hp += healed;
            results.Add(new BattleEvent { Kind = BattleEventKind.Heal, Source = actor, Target = target, Amount = healed, Label = skill.name, Timing = grade });
        }

        public static int Damage(Combatant actor, Combatant target, float power, SkillKind kind, bool critical, float variance, float timingMultiplier = 1f)
        {
            var offense = kind == SkillKind.Magic ? actor.Stats.spirit : actor.Stats.attack;
            var defense = kind == SkillKind.Magic ? target.Stats.spirit * 0.5f : target.Stats.defense * 0.5f;
            var amount = (offense * power * 1.2f - defense) * variance;
            if (critical) amount *= CriticalMultiplier;
            if (target.Guarding) amount *= GuardMultiplier;
            amount *= timingMultiplier;
            return Math.Max(1, (int)Math.Round(amount));
        }

        void UpdateOutcome()
        {
            if (!party.Exists(c => c.Alive))
            {
                Outcome = BattleOutcome.Defeat;
            }
            else if (SpareTarget != null && !SpareTarget.Alive)
            {
                SpareKilled = true;
                Outcome = BattleOutcome.Defeat;
            }
            else if (!enemies.Exists(c => c.Alive && c != SpareTarget))
            {
                Outcome = BattleOutcome.Victory;
            }
        }

        void BeginRound()
        {
            Round++;
            if (SurrenderAfterRound > 0 && Round > SurrenderAfterRound)
            {
                Surrendered = true;
                Outcome = BattleOutcome.Victory;
                return;
            }
            turnOrder.Clear();
            turnOrder.AddRange(party.FindAll(c => c.Alive));
            turnOrder.AddRange(enemies.FindAll(c => c.Alive));

            // Decorate with a rolled initiative first so the sort never re-rolls mid-comparison.
            var initiative = new Dictionary<Combatant, float>();
            foreach (var combatant in turnOrder)
            {
                var speed = combatant.HasteTurns > 0 ? combatant.Stats.speed * HasteSpeedScale : combatant.Stats.speed;
                initiative[combatant] = speed * random.Range(0.9f, 1.1f);
            }
            turnOrder.Sort((a, b) => initiative[b].CompareTo(initiative[a]));

            PlanIntents();
            turnIndex = -1;
            Advance();
        }

        void Advance()
        {
            for (var i = turnIndex + 1; i < turnOrder.Count; i++)
            {
                var next = turnOrder[i];
                if (!next.Alive) continue;
                turnIndex = i;
                if (!StartTurn(next))
                {
                    if (Outcome != BattleOutcome.Ongoing) return;
                    continue;
                }
                Current = next;
                return;
            }
            BeginRound();
        }

        // Burns tick and statuses wear down. Returns false when this fighter can't act this turn.
        bool StartTurn(Combatant fighter)
        {
            fighter.ExposedTurns = Math.Max(0, fighter.ExposedTurns - 1);
            fighter.HasteTurns = Math.Max(0, fighter.HasteTurns - 1);
            if (fighter.BurnTurns > 0)
            {
                fighter.BurnTurns--;
                var amount = Math.Max(1, (int)(fighter.Stats.hp * BurnShare));

                // A burn can't finish someone the player is trying to spare.
                if (fighter == SpareTarget) amount = Math.Min(amount, fighter.Hp - 1);
                fighter.Hp = Math.Max(0, fighter.Hp - amount);
                turnStartEvents.Add(new BattleEvent { Kind = BattleEventKind.Burned, Target = fighter, Source = fighter, Amount = amount, Label = "Burn" });
                if (!fighter.Alive)
                {
                    turnStartEvents.Add(new BattleEvent { Kind = BattleEventKind.Defeated, Source = fighter, Target = fighter });
                    UpdateOutcome();
                    return false;
                }
            }
            if (fighter.StunTurns > 0)
            {
                fighter.StunTurns--;
                turnStartEvents.Add(new BattleEvent { Kind = BattleEventKind.Stunned, Target = fighter, Source = fighter, Label = "Stunned" });
                return false;
            }
            return true;
        }
    }
}
