using System;
using System.Collections.Generic;
using Airships.Core;
using NUnit.Framework;
using UnityEngine;

namespace Airships.Tests
{
    public class CoreRulesTests
    {
        GameContent content;

        [SetUp]
        public void Load()
        {
            content = JsonUtility.FromJson<GameContent>(Resources.Load<TextAsset>("game_content").text);
        }

        [Test]
        public void ContentReferencesResolve()
        {
            foreach (var cls in content.classes)
            {
                foreach (var unlock in cls.skills)
                {
                    var skill = content.Skill(unlock.skill);
                    Assert.DoesNotThrow(() => { _ = skill.Kind; _ = skill.Target; }, skill.id);
                }
            }
            foreach (var member in content.crew) Assert.NotNull(content.Class(member.classId));
            foreach (var enemy in content.enemies) Assert.NotNull(content.Class(enemy.classId));
            foreach (var ship in content.ships)
            {
                Assert.NotNull(content.Cannon(ship.bowCannon));
                Assert.NotNull(content.Cannon(ship.broadsideCannon));
            }
        }

        [Test]
        public void GrowthDependsOnClassAndIndividualBase()
        {
            var influence = content.economy.baseStatGrowthInfluence;
            var atClassBase = Growth.Rate(2f, 10f, 10f, influence);
            var aboveClassBase = Growth.Rate(2f, 10f, 14f, influence);
            var belowClassBase = Growth.Rate(2f, 10f, 6f, influence);
            Assert.AreEqual(2f, atClassBase, 0.0001f);
            Assert.Greater(aboveClassBase, atClassBase);
            Assert.Less(belowClassBase, atClassBase);
            Assert.Greater(Growth.Rate(3f, 10f, 10f, influence), atClassBase);
        }

        [Test]
        public void StatsAtLevelOneEqualIndividualBase()
        {
            var mirren = Array.Find(content.crew, c => c.id == "mirren");
            var stats = Growth.StatsAtLevel(mirren.baseStats, content.Class(mirren.classId), 1, content.economy.baseStatGrowthInfluence);
            Assert.AreEqual(mirren.baseStats.speed, stats.speed);
            Assert.AreEqual(mirren.baseStats.attack, stats.attack);
        }

        [Test]
        public void XpLevelsUpAcrossMultipleThresholdsAndStopsAtCap()
        {
            var member = new CrewProgress { id = "captain" };
            var first = Growth.XpToNext(content.economy, 1);
            var second = Growth.XpToNext(content.economy, 2);
            Assert.AreEqual(2, Growth.AddXp(content.economy, member, first + second + 1));
            Assert.AreEqual(3, member.level);
            Assert.AreEqual(1, member.xp);

            Growth.AddXp(content.economy, member, int.MaxValue / 2);
            Assert.AreEqual(content.economy.maxLevel, member.level);
            Assert.AreEqual(0, member.xp);
        }

        [Test]
        public void ShopChargesAndRespectsLimits()
        {
            var save = SaveData.NewGame(content);
            var upgrade = content.Upgrade("hull");
            save.gold = Shop.Cost(upgrade, 0);
            Assert.IsTrue(Shop.Buy(save, upgrade));
            Assert.AreEqual(0, save.gold);
            Assert.AreEqual(1, save.UpgradeLevel("hull"));
            Assert.IsFalse(Shop.Buy(save, upgrade), "Should not buy without gold");

            save.gold = int.MaxValue / 2;
            while (Shop.Buy(save, upgrade)) { }
            Assert.AreEqual(upgrade.maxLevel, save.UpgradeLevel("hull"));
        }

        [Test]
        public void UpgradesImproveThePlayerLoadout()
        {
            var save = SaveData.NewGame(content);
            var before = Encounter.PlayerLoadout(content, save, "player_sloop");
            save.upgrades.Add(new UpgradeProgress { id = "hull", level = 2 });
            save.upgrades.Add(new UpgradeProgress { id = "armor", level = 1 });
            save.upgrades.Add(new UpgradeProgress { id = "broadside", level = 1 });
            var after = Encounter.PlayerLoadout(content, save, "player_sloop");
            Assert.AreEqual(before.Hull * 1.3f, after.Hull, 0.01f);
            Assert.AreEqual(before.Armor + 2f, after.Armor, 0.01f);
            Assert.AreEqual(before.BroadsideDamageScale + 0.15f, after.BroadsideDamageScale, 0.01f);
        }

        [Test]
        public void ArmorReducesDamageButNeverBelowMinimum()
        {
            Assert.AreEqual(8f, DogfightSim.DamageAfterArmor(12f, 4f), 0.001f);
            Assert.AreEqual(12f * DogfightSim.MinimumDamageShare, DogfightSim.DamageAfterArmor(12f, 500f), 0.001f);
        }

        DogfightSim RunDogfight(int seed, int maxTicks, out int ticks)
        {
            var sim = new DogfightSim(seed);
            var save = SaveData.NewGame(content);
            var a = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "player_sloop"), 0f, -110f, 0f);
            var b = sim.AddShip(1, Encounter.EnemyLoadout(content, "pirate_raider", 0), 40f, 110f, Mathf.PI);
            var aiA = new DogfightAi(seed + 1, 8f);
            var aiB = new DogfightAi(seed + 2, 14f);
            var commands = new List<ShipCommand> { default, default };
            ticks = 0;
            while (!sim.Finished && ticks < maxTicks)
            {
                commands[0] = aiA.Think(sim, a);
                commands[1] = aiB.Think(sim, b);
                sim.Step(commands);
                ticks++;
            }
            return sim;
        }

        [Test]
        public void DogfightsFinishAndReplayIdentically()
        {
            const int limit = DogfightSim.TicksPerSecond * 300;
            var first = RunDogfight(77, limit, out var firstTicks);
            var second = RunDogfight(77, limit, out var secondTicks);

            Assert.IsTrue(first.Finished, "The AI fight should finish within five minutes");
            Assert.AreEqual(firstTicks, secondTicks);
            Assert.AreEqual(first.WinningTeam, second.WinningTeam);
            for (var i = 0; i < first.Ships.Count; i++)
            {
                Assert.AreEqual(first.Ships[i].Hull, second.Ships[i].Hull);
                Assert.AreEqual(first.Ships[i].X, second.Ships[i].X);
                Assert.AreEqual(first.Ships[i].Z, second.Ships[i].Z);
            }
        }

        [Test]
        public void DogfightLengthIsReasonableAcrossSeeds()
        {
            const int limit = DogfightSim.TicksPerSecond * 300;
            var total = 0;
            var wins = 0;
            const int runs = 40;
            for (var seed = 0; seed < runs; seed++)
            {
                var sim = RunDogfight(seed * 31 + 5, limit, out var ticks);
                Assert.IsTrue(sim.Finished, $"Seed {seed} did not finish");
                total += ticks;
                if (sim.WinningTeam == 0) wins++;
            }
            var averageSeconds = total / (float)runs / DogfightSim.TicksPerSecond;
            Debug.Log($"[Balance] Average AI dogfight {averageSeconds:0.0}s, stronger ship won {wins}/{runs}");
            Assert.That(averageSeconds, Is.InRange(25f, 150f));
            Assert.Greater(wins, runs / 2, "The better-equipped ship should usually win");
        }

        [Test]
        public void ShipsOutsideTheArenaTurnBack()
        {
            var sim = new DogfightSim(1);
            var save = SaveData.NewGame(content);
            var ship = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "player_sloop"), DogfightSim.ArenaRadius + 60f, 0f, Mathf.PI * 0.5f);
            sim.AddShip(1, Encounter.EnemyLoadout(content, "pirate_raider", 0), 0f, 0f, 0f);
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f }, new ShipCommand { Throttle = 0f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 20; i++) sim.Step(commands);
            Assert.Less(Math.Sqrt(ship.X * ship.X + ship.Z * ship.Z), DogfightSim.ArenaRadius + 90f);
        }

        PartyBattle NewBattle(int tier, int seed)
        {
            var save = SaveData.NewGame(content);
            foreach (var member in save.crew) member.level = 1 + tier;
            return new PartyBattle(BattleSetup.BuildParty(content, save), BattleSetup.BuildEnemies(content, tier), seed);
        }

        [Test]
        public void TurnOrderFollowsSpeedOverManyRounds()
        {
            var fastestFirst = 0;
            for (var seed = 1; seed <= 200; seed++)
            {
                var battle = NewBattle(0, seed);
                if (battle.TurnOrder[0].Stats.speed >= 15f) fastestFirst++;
            }
            Assert.Greater(fastestFirst, 100, "The fastest fighter should usually act first");
        }

        [Test]
        public void GuardHalvesDamage()
        {
            var battle = NewBattle(0, 3);
            var actor = battle.Enemies[0];
            var target = battle.Party[0];
            var normal = PartyBattle.Damage(actor, target, 1f, SkillKind.Physical, false, 1f);
            target.Guarding = true;
            var guarded = PartyBattle.Damage(actor, target, 1f, SkillKind.Physical, false, 1f);
            Assert.AreEqual(normal * 0.5f, guarded, 1f);
        }

        [Test]
        public void SkillsSpendMpAndAreRefusedWithoutIt()
        {
            var battle = NewBattle(0, 4);
            var actor = battle.Current;
            actor.Mp = 0;
            var skill = actor.Skills[0];
            Assert.IsFalse(PartyBattle.CanUse(actor, skill));
            var action = new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = battle.Opponents(actor)[0] };
            Assert.Throws<InvalidOperationException>(() => battle.Submit(action));

            actor.Mp = skill.mpCost;
            battle.Submit(action);
            Assert.AreEqual(0, actor.Mp);
        }

        [Test]
        public void HealingNeverExceedsMaximumHp()
        {
            var battle = NewBattle(0, 9);
            var surgeon = battle.Party[3];
            var ally = battle.Party[0];
            ally.Hp = (int)ally.Stats.hp - 1;
            while (battle.Current != surgeon) battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            var results = battle.Submit(new BattleAction { Kind = BattleActionKind.Skill, Skill = content.Skill("patch_up"), Target = ally });
            Assert.AreEqual((int)ally.Stats.hp, ally.Hp);
            Assert.IsTrue(results.Exists(r => r.Kind == BattleEventKind.Heal && r.Amount == 1));
        }

        [Test]
        public void DefeatedFightersNeverAct()
        {
            var battle = NewBattle(0, 12);
            var guard = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && guard++ < 2000)
            {
                Assert.IsTrue(battle.Current.Alive);
                battle.Submit(battle.ChooseEnemyAction(battle.Current));
            }
            Assert.AreNotEqual(BattleOutcome.Ongoing, battle.Outcome);
        }

        // Plays with the simple approach a new player might take: basic attacks and a heal when hurt.
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(5)]
        public void PartyUsuallyWinsAtMatchingLevel(int tier)
        {
            var wins = 0;
            var turns = 0;
            const int runs = 60;
            for (var seed = 1; seed <= runs; seed++)
            {
                var battle = NewBattle(tier, seed * 17);
                var guard = 0;
                while (battle.Outcome == BattleOutcome.Ongoing && guard++ < 3000)
                {
                    var actor = battle.Current;
                    battle.Submit(actor.IsPlayer ? SimplePlayerAction(battle, actor) : battle.ChooseEnemyAction(actor));
                    turns++;
                }
                if (battle.Outcome == BattleOutcome.Victory) wins++;
            }
            Debug.Log($"[Balance] Tier {tier}: party won {wins}/{runs}, average {turns / (float)runs:0.0} actions");
            Assert.Greater(wins, runs * 3 / 4, "Battles at a matching level should favor the party");
            Assert.That(turns / (float)runs, Is.InRange(8f, 90f));
        }

        static BattleAction SimplePlayerAction(PartyBattle battle, Combatant actor)
        {
            foreach (var skill in actor.Skills)
            {
                if (skill.Kind != SkillKind.Heal || !PartyBattle.CanUse(actor, skill)) continue;
                foreach (var ally in battle.Party)
                {
                    if (ally.Alive && ally.Hp < ally.Stats.hp * 0.5f) return new BattleAction { Kind = BattleActionKind.Skill, Skill = skill, Target = ally };
                }
            }
            var target = battle.Enemies.Count > 0 ? battle.Opponents(actor).Find(e => e.Alive) : null;
            return new BattleAction { Kind = BattleActionKind.Attack, Target = target };
        }

        [Test]
        public void EnemyCrewGrowsWithTier()
        {
            var early = BattleSetup.BuildEnemies(content, 0);
            var late = BattleSetup.BuildEnemies(content, 4);
            Assert.AreEqual(3, early.Count);
            Assert.AreEqual(4, late.Count);
            Assert.Greater(late[0].Stats.hp, early[0].Stats.hp);
            Assert.AreEqual("enemy_captain", early[0].Id);
        }

        [Test]
        public void SaveRoundTripsThroughJson()
        {
            var save = SaveData.NewGame(content);
            save.gold = 321;
            save.encountersWon = 4;
            save.crew[1].level = 6;
            save.upgrades.Add(new UpgradeProgress { id = "armor", level = 3 });
            save.settings.highDefinition = false;

            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.AreEqual(321, restored.gold);
            Assert.AreEqual(4, restored.encountersWon);
            Assert.AreEqual(6, restored.crew[1].level);
            Assert.AreEqual(3, restored.UpgradeLevel("armor"));
            Assert.IsFalse(restored.settings.highDefinition);
        }
    }
}
