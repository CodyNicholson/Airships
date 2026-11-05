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
            content.campaign = JsonUtility.FromJson<CampaignDef>(Resources.Load<TextAsset>("campaign_content").text);
        }

        static void WaitForTurn(PartyBattle battle, Combatant actor)
        {
            Assert.NotNull(actor);
            for (var i = 0; battle.Current != actor; i++)
            {
                Assert.Less(i, 100, $"{actor.Name} never got a turn");
                battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            }
        }

        // A full crew, for the rules that don't care how they were recruited.
        SaveData CrewedSave()
        {
            var save = SaveData.NewGame(content);
            foreach (var id in new[] { "rook", "thorne", "lyra" }) Campaign.Recruit(save, content, id, 1);
            return save;
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
                Assert.NotNull(content.Cannon(ship.chaseCannon));
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
            var rook = Array.Find(content.crew, c => c.id == "rook");
            var stats = Growth.StatsAtLevel(rook.baseStats, content.Class(rook.classId), 1, content.economy.baseStatGrowthInfluence);
            Assert.AreEqual(rook.baseStats.speed, stats.speed);
            Assert.AreEqual(rook.baseStats.attack, stats.attack);
        }

        [Test]
        public void XpLevelsUpAcrossMultipleThresholdsAndStopsAtCap()
        {
            var member = new CrewProgress { id = "vane" };
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
            var save = CrewedSave();
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
            var save = CrewedSave();
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
            var save = CrewedSave();
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
            var save = CrewedSave();
            var ship = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "player_sloop"), DogfightSim.ArenaRadius + 60f, 0f, Mathf.PI * 0.5f);
            sim.AddShip(1, Encounter.EnemyLoadout(content, "pirate_raider", 0), 0f, 0f, 0f);
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f }, new ShipCommand { Throttle = 0f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 20; i++) sim.Step(commands);
            Assert.Less(Math.Sqrt(ship.X * ship.X + ship.Z * ship.Z), DogfightSim.ArenaRadius + 90f);
        }

        DogfightSim TwoShips(int seed, out ShipState a, out ShipState b)
        {
            var sim = new DogfightSim(seed);
            var save = CrewedSave();
            a = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "player_sloop"), 0f, 0f, 0f);
            b = sim.AddShip(1, Encounter.EnemyLoadout(content, "pirate_raider", 0), 0f, 200f, Mathf.PI);
            return sim;
        }

        static HazardDef NoHazards() => new HazardDef { whaleMinTier = 99 };

        [Test]
        public void ClimbChangesAltitudeWithinLimits()
        {
            var sim = TwoShips(1, out var ship, out _);
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 0.7f, Climb = 1f }, new ShipCommand { Throttle = 0.7f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond; i++) sim.Step(commands);
            Assert.Greater(ship.Y, 5f);

            for (var i = 0; i < DogfightSim.TicksPerSecond * 20; i++) sim.Step(commands);
            Assert.AreEqual(DogfightSim.MaxAltitude, ship.Y, 0.001f);

            commands[0] = new ShipCommand { Throttle = 0.7f, Climb = -1f };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 20; i++) sim.Step(commands);
            Assert.AreEqual(DogfightSim.MinAltitude, ship.Y, 0.001f);
        }

        [Test]
        public void DivingIsFasterThanClimbing()
        {
            float SpeedWhile(float climb)
            {
                var sim = TwoShips(2, out var ship, out _);
                var commands = new List<ShipCommand> { new ShipCommand { Throttle = 0.7f, Climb = climb }, new ShipCommand { Throttle = 0.7f } };
                for (var i = 0; i < DogfightSim.TicksPerSecond * 2; i++) sim.Step(commands);
                return ship.Speed;
            }
            Assert.Greater(SpeedWhile(-1f), SpeedWhile(1f));
        }

        [Test]
        public void BroadsideCantReachBeyondItsElevationButTorpedoCan()
        {
            var sim = TwoShips(3, out var a, out var b);
            b.X = 0f;
            b.Z = 60f;
            b.Y = 40f;
            Assert.IsFalse(DogfightSim.CanElevate(a, b, a.Loadout.Broadside));
            Assert.IsTrue(DogfightSim.CanElevate(a, b, a.Loadout.Torpedo));

            b.Y = 5f;
            Assert.IsTrue(DogfightSim.CanElevate(a, b, a.Loadout.Broadside));
            _ = sim;
        }

        [Test]
        public void ShotsMissShipsAtADifferentAltitude()
        {
            var sim = TwoShips(4, out var a, out var b);
            b.Z = 60f;
            b.Y = 40f;
            var hull = b.Hull;

            // Facing the target head-on, but the broadside's limit keeps level fire below it.
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 0f, FireBroadside = true }, new ShipCommand { Throttle = 0f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 2; i++)
            {
                sim.Step(commands);
                b.Y = 40f;
                b.Z = 60f;
                b.X = 0f;
            }
            Assert.AreEqual(hull, b.Hull);
            _ = a;
        }

        HazardField SingleHazard(DogfightSim sim, HazardKind kind, float radius, float damagePerMeter, float x, float y, float z)
        {
            var def = NoHazards();
            def.rockCount = kind == HazardKind.Rock ? 1 : 0;
            def.debrisCount = kind == HazardKind.Debris ? 1 : 0;
            def.jellyfishCount = kind == HazardKind.Jellyfish ? 1 : 0;
            def.rockMinRadius = def.rockMaxRadius = def.debrisMinRadius = def.debrisMaxRadius = def.jellyfishMinRadius = def.jellyfishMaxRadius = radius;
            def.rockDamagePerMeter = def.debrisDamagePerMeter = def.jellyfishDamagePerMeter = damagePerMeter;
            def.debrisToughnessPerMeter = 12f;
            var field = sim.AddHazards(def, 0, 5);
            var hazard = field.Hazards[0];
            hazard.X = x;
            hazard.Y = y;
            hazard.Z = z;
            hazard.BaseY = y;
            hazard.DriftX = 0f;
            hazard.DriftZ = 0f;
            return field;
        }

        float CrashDamage(HazardKind kind, float radius)
        {
            var sim = TwoShips(6, out var ship, out _);
            SingleHazard(sim, kind, radius, 10f, 0f, 0f, 30f);
            var hull = ship.Hull;
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f }, new ShipCommand { Throttle = 0.5f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 2 && ship.Hull == hull; i++) sim.Step(commands);
            return hull - ship.Hull;
        }

        [Test]
        public void CrashDamageIsProportionalToSize()
        {
            Assert.AreEqual(30f, CrashDamage(HazardKind.Debris, 3f), 0.01f);
            Assert.AreEqual(60f, CrashDamage(HazardKind.Debris, 6f), 0.01f);
            Assert.AreEqual(150f, CrashDamage(HazardKind.Rock, 15f), 0.01f);
        }

        [Test]
        public void JellyfishShockStopsTheShipDeadUntilTheCurrentLetsGo()
        {
            var sim = TwoShips(9, out var ship, out _);
            var field = SingleHazard(sim, HazardKind.Jellyfish, 5f, 10f, 0f, 0f, 12f);
            field.Hazards[0].SwimSpeed = 0f;
            field.Hazards[0].BobPhase = 0f;
            var hull = ship.Hull;
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f, Turn = 1f, Climb = 1f }, new ShipCommand { Throttle = 0.5f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 2 && ship.Hull == hull; i++) sim.Step(commands);
            Assert.Less(ship.Hull, hull, "The sting hurts");
            Assert.IsTrue(ship.Stunned);

            // Held in place, facing the same way and with its guns silent, however hard the stick is pushed
            // and the triggers pulled.
            commands[0] = new ShipCommand { Throttle = 1f, Turn = 1f, Climb = 1f, FireTorpedo = true, FireBroadside = true, FireChaseGuns = true, Signature = true, Grapple = true };
            var x = ship.X;
            var y = ship.Y;
            var z = ship.Z;
            var heading = ship.Heading;
            var stunTicks = (int)(HazardField.JellyStunSeconds * DogfightSim.TicksPerSecond) - 2;
            for (var i = 0; i < stunTicks; i++)
            {
                sim.Step(commands);
                foreach (var e in sim.Events)
                {
                    Assert.IsFalse((e.Kind == DogfightEventKind.Fired || e.Kind == DogfightEventKind.SignatureFired) && e.ShipId == ship.Id, "A shocked ship can't fire");
                }
            }
            Assert.IsTrue(ship.Stunned);
            Assert.AreEqual(0f, ship.Speed);
            Assert.AreEqual(x, ship.X, 0.001f);
            Assert.AreEqual(y, ship.Y, 0.001f);
            Assert.AreEqual(z, ship.Z, 0.001f);
            Assert.AreEqual(heading, ship.Heading, 0.001f);

            for (var i = 0; i < DogfightSim.TicksPerSecond; i++) sim.Step(commands);
            Assert.IsFalse(ship.Stunned);
            Assert.Greater(ship.Speed, 0f, "Once the current lets go, the ship flies on");
        }

        [Test]
        public void DebrisBreaksOnImpactButRocksRemain()
        {
            var sim = TwoShips(7, out _, out _);
            var field = SingleHazard(sim, HazardKind.Debris, 4f, 10f, 0f, 0f, 30f);
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f }, new ShipCommand { Throttle = 0.5f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 2; i++) sim.Step(commands);
            Assert.IsFalse(field.Hazards[0].Alive);

            var rockSim = TwoShips(7, out var ship, out _);
            var rocks = SingleHazard(rockSim, HazardKind.Rock, 10f, 10f, 0f, 0f, 30f);
            for (var i = 0; i < DogfightSim.TicksPerSecond * 3; i++) rockSim.Step(commands);
            Assert.IsTrue(rocks.Hazards[0].Alive);

            // The rock stops the ship rather than letting it pass through.
            var distance = Mathf.Sqrt(ship.X * ship.X + (ship.Y * ship.Y) + (ship.Z - 30f) * (ship.Z - 30f));
            Assert.GreaterOrEqual(distance, 10f);
        }

        [Test]
        public void OneRockDamagesOnlyOncePerGracePeriod()
        {
            var sim = TwoShips(8, out var ship, out _);
            SingleHazard(sim, HazardKind.Rock, 10f, 10f, 0f, 0f, 16f);
            var hull = ship.Hull;
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f }, new ShipCommand { Throttle = 0.5f } };
            for (var i = 0; i < 15; i++) sim.Step(commands);
            Assert.AreEqual(hull - 100f, ship.Hull, 0.01f);
        }

        [Test]
        public void RocksBlockCannonFire()
        {
            var sim = TwoShips(9, out _, out var b);
            b.Z = 70f;
            SingleHazard(sim, HazardKind.Rock, 10f, 10f, 0f, 0f, 35f);
            var hull = b.Hull;
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 0f, FireTorpedo = true }, new ShipCommand { Throttle = 0f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 2; i++)
            {
                sim.Step(commands);
                b.Z = 70f;
                b.X = 0f;
            }
            Assert.AreEqual(hull, b.Hull);
        }

        DogfightSim WhaleSim(out ShipState ship, out HazardField field)
        {
            var sim = TwoShips(10, out ship, out _);
            var def = content.hazards;
            field = sim.AddHazards(new HazardDef
            {
                whaleMinTier = 0, whaleFirstDelayMin = 0f, whaleFirstDelayMax = 0f, whaleRestMin = 60f, whaleRestMax = 60f,
                whaleAlertSeconds = def.whaleAlertSeconds, whaleLungeSeconds = def.whaleLungeSeconds, whaleLungeSpeed = def.whaleLungeSpeed,
                whaleCruiseSpeed = def.whaleCruiseSpeed, whaleMouthRadius = def.whaleMouthRadius, whaleSenseRange = 1000f,
            }, 0, 3);
            return sim;
        }

        [Test]
        public void WhaleEatsAShipInItsPathAndEndsTheFight()
        {
            var sim = WhaleSim(out var ship, out var field);
            var whale = field.Whale;
            whale.Mode = WhaleMode.Lunging;
            whale.Timer = 2f;
            whale.X = 0f;
            whale.Y = 0f;
            whale.Z = -80f;
            whale.DirX = 0f;
            whale.DirY = 0f;
            whale.DirZ = 1f;
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 0f }, new ShipCommand { Throttle = 0f } };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 2 && !sim.Finished; i++) sim.Step(commands);
            Assert.IsTrue(ship.Eaten);
            Assert.AreEqual(0f, ship.Hull);
            Assert.AreEqual(1, sim.WinningTeam);
        }

        [Test]
        public void WhaleWarnsBeforeLungingAndCanBeDodged()
        {
            var sim = WhaleSim(out var ship, out var field);
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f }, new ShipCommand { Throttle = 0.5f } };
            var alertTick = -1;
            var lungeTick = -1;
            for (var tick = 0; tick < DogfightSim.TicksPerSecond * 30 && lungeTick < 0; tick++)
            {
                sim.Step(commands);
                foreach (var e in sim.Events)
                {
                    if (e.Kind == DogfightEventKind.WhaleAlert) alertTick = tick;
                    if (e.Kind == DogfightEventKind.WhaleLunge) lungeTick = tick;
                }
            }
            Assert.GreaterOrEqual(alertTick, 0);
            Assert.GreaterOrEqual((lungeTick - alertTick) / (float)DogfightSim.TicksPerSecond, 2.5f, "The player needs time to react");

            // Once it lunges it can't steer, so leaving the line by climbing escapes it.
            commands[0] = new ShipCommand { Turn = 1f, Throttle = 1f, Climb = field.Whale.Y >= ship.Y ? -1f : 1f };
            for (var i = 0; i < DogfightSim.TicksPerSecond * 3; i++) sim.Step(commands);
            Assert.IsFalse(ship.Eaten);
        }

        [Test]
        public void WhaleNeverEatsOrHuntsAnAiEnemy()
        {
            var sim = WhaleSim(out var player, out var field);
            var enemy = sim.Ships[1];
            enemy.HuntedByWhale = false;

            // Far closer to the enemy, but the whale still chooses the player.
            var whale = field.Whale;
            whale.Mode = WhaleMode.Roaming;
            whale.Timer = 0f;
            whale.X = enemy.X;
            whale.Y = enemy.Y + 40f;
            whale.Z = enemy.Z;
            var commands = new List<ShipCommand> { new ShipCommand { Throttle = 0.5f }, new ShipCommand { Throttle = 0.5f } };
            sim.Step(commands);
            Assert.AreEqual(player.Id, whale.TargetId);

            // A lunge straight through the enemy leaves it untouched.
            whale.Mode = WhaleMode.Lunging;
            whale.Timer = 2f;
            whale.X = enemy.X;
            whale.Y = enemy.Y;
            whale.Z = enemy.Z - 30f;
            whale.DirX = 0f;
            whale.DirY = 0f;
            whale.DirZ = 1f;
            var hull = enemy.Hull;
            for (var i = 0; i < DogfightSim.TicksPerSecond; i++) sim.Step(commands);
            Assert.IsFalse(enemy.Eaten);
            Assert.AreEqual(hull, enemy.Hull);
        }

        [Test]
        public void WhaleSteersAroundAiEnemies()
        {
            float ClosestPass(bool hunted)
            {
                var sim = WhaleSim(out var player, out var field);
                var enemy = sim.Ships[1];
                enemy.HuntedByWhale = hunted;
                player.HuntedByWhale = false;
                var whale = field.Whale;
                whale.Mode = WhaleMode.Resting;
                whale.Timer = 60f;
                whale.X = enemy.X;
                whale.Y = enemy.Y;
                whale.Z = enemy.Z - 70f;
                whale.DirX = 0f;
                whale.DirY = 0f;
                whale.DirZ = 1f;
                whale.WaypointX = enemy.X;
                whale.WaypointY = enemy.Y;
                whale.WaypointZ = enemy.Z + 300f;
                var commands = new List<ShipCommand> { new ShipCommand { Throttle = 0f }, new ShipCommand { Throttle = 0f } };
                var closest = float.MaxValue;
                for (var i = 0; i < DogfightSim.TicksPerSecond * 8; i++)
                {
                    sim.Step(commands);
                    enemy.X = 0f;
                    enemy.Z = 200f;
                    enemy.Y = 0f;
                    var dx = whale.X - enemy.X;
                    var dy = whale.Y - enemy.Y;
                    var dz = whale.Z - enemy.Z;
                    closest = Mathf.Min(closest, Mathf.Sqrt(dx * dx + dy * dy + dz * dz));
                }
                return closest;
            }

            Assert.Greater(ClosestPass(false), 30f, "It should keep its distance from a ship it doesn't hunt");
        }

        [Test]
        public void EngineUpgradesMakeShipsMeasurablyFaster()
        {
            float TopSpeed(int engineLevel)
            {
                var save = CrewedSave();
                if (engineLevel > 0) save.upgrades.Add(new UpgradeProgress { id = "engine", level = engineLevel });
                var sim = new DogfightSim(1);
                var ship = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "player_sloop"), 0f, 0f, 0f);
                sim.AddShip(1, Encounter.EnemyLoadout(content, "pirate_raider", 0), 0f, 400f, Mathf.PI);
                var commands = new List<ShipCommand> { new ShipCommand { Throttle = 1f }, new ShipCommand { Throttle = 0.5f } };
                for (var i = 0; i < DogfightSim.TicksPerSecond * 4; i++) sim.Step(commands);
                return ship.Speed;
            }
            Assert.Greater(TopSpeed(0), content.Ship("pirate_raider").speed, "The player starts faster than the raider");
            Assert.AreEqual(TopSpeed(0) * 1.6f, TopSpeed(5), 0.5f, "Five levels add 60%");
        }

        [Test]
        public void WhaleStaysAwayFromTheFirstEncounter()
        {
            var sim = TwoShips(11, out _, out _);
            Assert.IsNull(sim.AddHazards(content.hazards, 0, 1).Whale);
            var later = TwoShips(11, out _, out _);
            Assert.IsNotNull(later.AddHazards(content.hazards, content.hazards.whaleMinTier, 1).Whale);
        }

        [Test]
        public void HazardFightsFinishAndReplayIdentically()
        {
            DogfightSim Run(int seed, out int ticks)
            {
                var sim = TwoShips(seed, out var a, out var b);
                sim.AddHazards(content.hazards, 2, seed);
                var aiA = new DogfightAi(seed + 1, 8f);
                var aiB = new DogfightAi(seed + 2, 10f);
                var commands = new List<ShipCommand> { default, default };
                ticks = 0;
                while (!sim.Finished && ticks < DogfightSim.TicksPerSecond * 300)
                {
                    commands[0] = aiA.Think(sim, a);
                    commands[1] = aiB.Think(sim, b);
                    sim.Step(commands);
                    ticks++;
                }
                return sim;
            }

            for (var seed = 1; seed <= 10; seed++)
            {
                var first = Run(seed, out var firstTicks);
                var second = Run(seed, out var secondTicks);
                Assert.IsTrue(first.Finished, $"Seed {seed} did not finish");
                Assert.AreEqual(firstTicks, secondTicks);
                Assert.AreEqual(first.WinningTeam, second.WinningTeam);
                Assert.AreEqual(first.Ships[0].Hull, second.Ships[0].Hull);
                Assert.AreEqual(first.Ships[0].Y, second.Ships[0].Y);
            }
        }

        PartyBattle NewBattle(int tier, int seed)
        {
            var save = CrewedSave();
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
            Combatant surgeon = null;
            foreach (var member in battle.Party)
            {
                if (member.ClassId == "sorcerer") surgeon = member;
            }
            Assert.IsNotNull(surgeon, "The default party should include a healer");
            var ally = battle.Party[0];
            ally.Hp = (int)ally.Stats.hp - 1;
            WaitForTurn(battle, surgeon);
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
            Assert.AreEqual(2, early.Count);
            Assert.AreEqual(3, late.Count);
            Assert.Greater(late[0].Stats.hp, early[0].Stats.hp);
            Assert.AreEqual("enemy_captain", early[0].Id);
        }

        [Test]
        public void PartyHasThreeMembersLedByTheCaptain()
        {
            var save = CrewedSave();
            Assert.AreEqual(3, save.party.Count);
            Assert.AreEqual("vane", save.party[0]);
            var built = BattleSetup.BuildParty(content, save);
            Assert.AreEqual(3, built.Count);
            Assert.Throws<ArgumentException>(() =>
            {
                var tooMany = new List<Combatant>(built) { built[0] };
                _ = new PartyBattle(tooMany, BattleSetup.BuildEnemies(content, 0), 1);
            });
        }

        [Test]
        public void SwappingTradesPlacesWithTheReserveButNeverTheCaptain()
        {
            var save = CrewedSave();
            Assert.IsFalse(save.SwapOut("vane"), "The captain always sails");
            Assert.IsFalse(save.IsInParty("lyra"));

            Assert.IsTrue(save.SwapOut("rook"));
            Assert.IsFalse(save.IsInParty("rook"));
            Assert.IsTrue(save.IsInParty("lyra"));
            Assert.AreEqual(3, save.party.Count);
            Assert.IsFalse(save.SwapOut("rook"), "A reserve member can't be swapped out again");
        }

        [Test]
        public void YouChooseWhoBoardsAndWhoStaysBehind()
        {
            var save = CrewedSave();
            Assert.IsTrue(save.PartyFull);
            Assert.IsFalse(save.JoinParty("lyra"), "No place for her while the party is full");
            Assert.IsFalse(save.StayBehind("vane"), "The captain always sails");

            Assert.IsTrue(save.StayBehind("rook"));
            Assert.IsFalse(save.IsInParty("rook"));
            Assert.IsFalse(save.IsInParty("lyra"), "Leaving someone behind doesn't pick their replacement");
            Assert.IsFalse(save.StayBehind("rook"), "Already staying behind");

            Assert.IsTrue(save.JoinParty("lyra"));
            Assert.IsTrue(save.IsInParty("lyra"));
            Assert.IsFalse(save.JoinParty("lyra"), "Already aboard the party");
            Assert.IsFalse(save.JoinParty("nobody"), "Only crew can join");

            // A short party is a choice: the rest defend the ship, and it survives a save.
            Assert.IsTrue(save.StayBehind("thorne"));
            Assert.AreEqual(2, save.party.Count);
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            restored.EnsureValidParty(restored.party.Count == 0);
            Assert.AreEqual(new List<string> { "vane", "lyra" }, restored.party);
            Assert.AreEqual(2, BattleSetup.BuildParty(content, restored).Count);
        }

        [Test]
        public void OlderSavesWithoutAPartyGetAValidOne()
        {
            var save = CrewedSave();
            save.party.Clear();
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            restored.EnsureValidParty();
            Assert.AreEqual(3, restored.party.Count);
            Assert.AreEqual("vane", restored.party[0]);
        }

        [Test]
        public void SavedPartyChoiceSurvivesAndWorksInBattle()
        {
            var save = CrewedSave();
            save.SwapOut("thorne");
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            restored.EnsureValidParty();
            Assert.IsTrue(restored.IsInParty("lyra"));
            Assert.IsFalse(restored.IsInParty("thorne"));
            Assert.IsTrue(BattleSetup.BuildParty(content, restored).Exists(c => c.Id == "lyra"));
        }

        PartyBattle TimingBattle(out Combatant hero, out Combatant foe)
        {
            var save = CrewedSave();
            var party = BattleSetup.BuildParty(content, save);
            var enemies = BattleSetup.BuildEnemies(content, 0);
            hero = party[0];
            foe = enemies[0];
            return new PartyBattle(party, enemies, 21, content.timing);
        }

        [Test]
        public void TimedAttacksDealMoreDamage()
        {
            TimingBattle(out var hero, out var foe);
            var t = content.timing;
            var plain = PartyBattle.Damage(hero, foe, 1f, SkillKind.Physical, false, 1f);
            var good = PartyBattle.Damage(hero, foe, 1f, SkillKind.Physical, false, 1f, t.attackGood);
            var perfect = PartyBattle.Damage(hero, foe, 1f, SkillKind.Physical, false, 1f, t.attackPerfect);
            Assert.Greater(good, plain);
            Assert.Greater(perfect, good);
        }

        int ResolveWith(TimingGrade grade, bool playerAttacks)
        {
            // The same seed gives the same variance and critical rolls, so only the grade differs.
            var battle = TimingBattle(out var hero, out var foe);
            var guard = 0;
            while (battle.Current.IsPlayer != playerAttacks && guard++ < 50)
            {
                battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            }
            foreach (var combatant in battle.Party) combatant.Guarding = false;
            foreach (var combatant in battle.Enemies) combatant.Guarding = false;
            var target = playerAttacks ? foe : hero;
            var before = target.Hp;
            battle.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = target, Timing = grade });
            return before - target.Hp;
        }

        [Test]
        public void GradesChangeRealDamageOnBothSides()
        {
            Assert.Greater(ResolveWith(TimingGrade.Perfect, true), ResolveWith(TimingGrade.Good, true));
            Assert.Greater(ResolveWith(TimingGrade.Good, true), ResolveWith(TimingGrade.Miss, true));
            Assert.Less(ResolveWith(TimingGrade.Perfect, false), ResolveWith(TimingGrade.Good, false));
            Assert.Less(ResolveWith(TimingGrade.Good, false), ResolveWith(TimingGrade.Miss, false));
        }

        [Test]
        public void TimedHealsRestoreMore()
        {
            int Healed(TimingGrade grade)
            {
                var save = CrewedSave();
                var party = BattleSetup.BuildParty(content, save);
                var battle = new PartyBattle(party, BattleSetup.BuildEnemies(content, 0), 5, content.timing);
                var surgeon = party.Find(c => c.ClassId == "sorcerer");
                WaitForTurn(battle, surgeon);
                var ally = party[0];
                ally.Hp = 1;
                battle.Submit(new BattleAction { Kind = BattleActionKind.Skill, Skill = content.Skill("patch_up"), Target = ally, Timing = grade });
                return ally.Hp - 1;
            }
            Assert.Greater(Healed(TimingGrade.Perfect), Healed(TimingGrade.Miss));
        }

        // The same attack with no timing rules at all, as the baseline a miss is measured against.
        int ResolveWithoutTiming(bool playerAttacks)
        {
            var save = CrewedSave();
            var party = BattleSetup.BuildParty(content, save);
            var enemies = BattleSetup.BuildEnemies(content, 0);
            var battle = new PartyBattle(party, enemies, 21);
            var guard = 0;
            while (battle.Current.IsPlayer != playerAttacks && guard++ < 50) battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            foreach (var combatant in battle.Party) combatant.Guarding = false;
            foreach (var combatant in battle.Enemies) combatant.Guarding = false;
            var target = playerAttacks ? enemies[0] : party[0];
            var before = target.Hp;
            battle.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = target });
            return before - target.Hp;
        }

        [Test]
        public void MissingDealsLessDamageByANoticeableShare()
        {
            var baseline = ResolveWithoutTiming(true);
            var missed = ResolveWith(TimingGrade.Miss, true);
            Assert.AreEqual(baseline * content.timing.attackMiss, missed, 1f);
            Assert.LessOrEqual(content.timing.attackMiss, 0.85f, "The cost should be easy to notice");
        }

        [Test]
        public void MissingTakesMoreDamageByANoticeableShare()
        {
            var baseline = ResolveWithoutTiming(false);
            var missed = ResolveWith(TimingGrade.Miss, false);
            Assert.AreEqual(baseline * content.timing.defendMiss, missed, 1f);
            Assert.GreaterOrEqual(content.timing.defendMiss, 1.15f, "The cost should be easy to notice");
        }

        [Test]
        public void MissedHealsRestoreTheNormalAmount()
        {
            int Healed(bool withTiming)
            {
                var save = CrewedSave();
                var party = BattleSetup.BuildParty(content, save);
                var battle = new PartyBattle(party, BattleSetup.BuildEnemies(content, 0), 5, withTiming ? content.timing : null);
                var surgeon = party.Find(c => c.ClassId == "sorcerer");
                WaitForTurn(battle, surgeon);
                party[0].Hp = 1;
                battle.Submit(new BattleAction { Kind = BattleActionKind.Skill, Skill = content.Skill("patch_up"), Target = party[0], Timing = TimingGrade.Miss });
                return party[0].Hp - 1;
            }
            Assert.AreEqual(Healed(false), Healed(true));
        }

        static TimingPattern TwoBeats() => new TimingPattern
        {
            id = "test",
            role = "any",
            beats = new[]
            {
                new TimingBeat { at = 1f, ring = 1f, x = 0.5f, y = 0.5f },
                new TimingBeat { at = 1.5f, ring = 0.5f, x = 0.5f, y = 0.5f },
            },
        };

        const float PromptSize = 1000f;

        TimingSequence NewSequence(TimingPattern pattern) => new TimingSequence(pattern, content.timing, PromptSize, PromptSize);

        static void PressAt(TimingSequence sequence, TimingBeat beat) => sequence.PointerDown(beat.x * PromptSize, beat.y * PromptSize);

        TimingSequence PlayTwoBeats(float firstTap, float secondTap, bool secondOnTarget = true)
        {
            var pattern = TwoBeats();
            var sequence = NewSequence(pattern);
            sequence.Advance(firstTap);
            PressAt(sequence, pattern.beats[0]);
            if (!sequence.Done)
            {
                sequence.Advance(secondTap - firstTap);
                if (!sequence.Done)
                {
                    if (secondOnTarget) PressAt(sequence, pattern.beats[1]);
                    else sequence.PointerDown(0f, 0f);
                }
            }
            return sequence;
        }

        static TimingPattern SwipeUp() => new TimingPattern
        {
            id = "swipe",
            role = "any",
            beats = new[] { new TimingBeat { at = 1f, ring = 1f, x = 0.5f, y = 0.5f, kind = "swipe", dir = "up" } },
        };

        static TimingPattern DragRight() => new TimingPattern
        {
            id = "drag",
            role = "any",
            beats = new[] { new TimingBeat { at = 0.5f, ring = 0.5f, x = 0.2f, y = 0.5f, kind = "drag", endX = 0.8f, endY = 0.5f, duration = 0.6f } },
        };

        TimingSequence PlaySwipe(float dx, float dy, float finishAt = 1f)
        {
            var sequence = NewSequence(SwipeUp());
            sequence.Advance(finishAt - 0.05f);
            sequence.PointerDown(500f, 500f);
            sequence.Advance(0.05f);
            sequence.PointerMove(500f + dx, 500f + dy);
            return sequence;
        }

        [Test]
        public void SwipesMustGoTheShownWay()
        {
            var distance = content.timing.swipeDistance * 1.2f;
            Assert.AreEqual(TimingGrade.Perfect, PlaySwipe(0f, distance).Grade);
            Assert.AreEqual(TimingGrade.Perfect, PlaySwipe(distance * 0.3f, distance).Grade, "A slightly angled flick still counts");
            Assert.AreEqual(TimingGrade.Miss, PlaySwipe(0f, -distance).Grade, "The wrong way fails");
            Assert.AreEqual(TimingGrade.Miss, PlaySwipe(distance, 0f).Grade, "Sideways fails");
        }

        [Test]
        public void SwipesAreGradedWhenTheFlickFinishes()
        {
            var distance = content.timing.swipeDistance * 1.2f;
            Assert.AreEqual(TimingGrade.Good, PlaySwipe(0f, distance, 1f + content.timing.goodWindow * 0.8f).Grade);
            Assert.AreEqual(TimingGrade.Miss, PlaySwipe(0f, distance, 1f - content.timing.goodWindow * 2f).Grade, "Too early fails");
        }

        [Test]
        public void TappingWhereASwipeIsNeededFails()
        {
            var sequence = NewSequence(SwipeUp());
            sequence.Advance(0.98f);
            sequence.PointerDown(500f, 500f);
            sequence.Advance(0.02f);
            sequence.PointerUp();
            Assert.IsTrue(sequence.Done);
            Assert.AreEqual(TimingGrade.Miss, sequence.Grade);
        }

        [Test]
        public void ASmallWobbleDoesNotCountAsASwipe()
        {
            var sequence = NewSequence(SwipeUp());
            sequence.Advance(0.95f);
            sequence.PointerDown(500f, 500f);
            sequence.PointerMove(510f, 505f);
            Assert.IsFalse(sequence.Done);
            Assert.IsTrue(sequence.IsSwipeArmed);
        }

        // Follows the marker to the end, then returns the finished sequence.
        TimingSequence PlayDrag(float pressAt, float strayBy = 0f, float liftAtProgress = 2f)
        {
            var pattern = DragRight();
            var beat = pattern.beats[0];
            var sequence = NewSequence(pattern);
            sequence.Advance(pressAt);
            PressAt(sequence, beat);
            var step = 1f / DogfightSim.TicksPerSecond;
            var guard = 0;
            while (!sequence.Done && guard++ < 200)
            {
                sequence.CurrentMarker(out var x, out var y);
                sequence.PointerMove(x, y + strayBy);
                if ((sequence.Elapsed - beat.at) / beat.duration >= liftAtProgress)
                {
                    sequence.PointerUp();
                    break;
                }
                sequence.Advance(step);
            }
            return sequence;
        }

        [Test]
        public void DragsSucceedByFollowingTheMarkerToTheEnd()
        {
            Assert.AreEqual(TimingGrade.Perfect, PlayDrag(0.5f).Grade);
            Assert.AreEqual(TimingGrade.Good, PlayDrag(0.5f - content.timing.goodWindow * 0.8f).Grade, "The press sets the grade");
        }

        [Test]
        public void DragsFailOnALatePressStrayingOrLifting()
        {
            Assert.AreEqual(TimingGrade.Miss, PlayDrag(0.5f - content.timing.goodWindow * 2f).Grade, "Pressing too early fails");
            Assert.AreEqual(TimingGrade.Miss, PlayDrag(0.5f, content.timing.dragTolerance * 1.5f).Grade, "Straying fails");
            Assert.AreEqual(TimingGrade.Miss, PlayDrag(0.5f, 0f, 0.4f).Grade, "Lifting early fails");
            Assert.AreEqual(TimingGrade.Perfect, PlayDrag(0.5f, 0f, 0.95f).Grade, "Lifting just before the end is forgiven");
        }

        [Test]
        public void AStillFingerFallsBehindTheMarker()
        {
            var pattern = DragRight();
            var sequence = NewSequence(pattern);
            sequence.Advance(0.5f);
            PressAt(sequence, pattern.beats[0]);
            for (var i = 0; i < 30 && !sequence.Done; i++) sequence.Advance(1f / 30f);
            Assert.AreEqual(TimingGrade.Miss, sequence.Grade);
        }

        [Test]
        public void EveryTierMixesTapsSwipesAndDrags()
        {
            foreach (var attacking in new[] { true, false })
            {
                var seen = new HashSet<string>();
                foreach (var pattern in content.timing.patterns)
                {
                    if (pattern.minTier > 0 || (pattern.role != "any" && pattern.role != (attacking ? "attack" : "defend"))) continue;
                    foreach (var beat in pattern.beats) seen.Add(beat.IsSwipe ? "swipe" : beat.IsDrag ? "drag" : "tap");
                }
                Assert.AreEqual(3, seen.Count, "The first encounter should already include all three gestures");
            }

            var mixed = 0;
            foreach (var pattern in content.timing.patterns)
            {
                var kinds = new HashSet<string>();
                foreach (var beat in pattern.beats) kinds.Add(beat.IsSwipe ? "swipe" : beat.IsDrag ? "drag" : "tap");
                if (kinds.Count >= 2) mixed++;
            }
            Assert.GreaterOrEqual(mixed, 6, "Most multi-step patterns should mix gestures");
        }

        [Test]
        public void EveryTargetMustBeHitAndTheWeakestSetsTheGrade()
        {
            var perfect = content.timing.perfectWindow * 0.5f;
            var good = content.timing.goodWindow * 0.8f;
            Assert.AreEqual(TimingGrade.Perfect, PlayTwoBeats(1f + perfect, 1.5f - perfect).Grade);
            Assert.AreEqual(TimingGrade.Good, PlayTwoBeats(1f, 1.5f + good).Grade, "One good tap caps the grade");
            Assert.AreEqual(TimingGrade.Miss, PlayTwoBeats(1f, 1.5f + content.timing.goodWindow * 1.5f).Grade, "A late second tap fails it");
        }

        [Test]
        public void TappingTheWrongSpotFailsThePattern()
        {
            Assert.AreEqual(TimingGrade.Miss, PlayTwoBeats(1f, 1.5f, false).Grade);
        }

        [Test]
        public void NotTappingFailsAtTheFirstMissedTarget()
        {
            var sequence = NewSequence(TwoBeats());
            sequence.Advance(1f + content.timing.goodWindow + 0.01f);
            Assert.IsTrue(sequence.Done);
            Assert.AreEqual(TimingGrade.Miss, sequence.Grade);
        }

        [Test]
        public void VeryEarlyTapsAreIgnoredButCloseOnesCount()
        {
            var pattern = TwoBeats();
            var sequence = NewSequence(pattern);
            sequence.Advance(0.1f);
            PressAt(sequence, pattern.beats[0]);
            Assert.IsFalse(sequence.Done, "A double-tap on a menu button shouldn't fail it");

            sequence.Advance(0.6f);
            PressAt(sequence, pattern.beats[0]);
            Assert.IsTrue(sequence.Done, "Tapping early once the target is showing fails it");
            Assert.AreEqual(TimingGrade.Miss, sequence.Grade);
        }

        [Test]
        public void PatternsAreValidAndVariedAtEveryTier()
        {
            var timing = content.timing;
            foreach (var pattern in timing.patterns)
            {
                Assert.Greater(pattern.beats.Length, 0, pattern.id);
                var previous = 0f;
                foreach (var beat in pattern.beats)
                {
                    Assert.Greater(beat.at, previous, $"{pattern.id} beats must run in order");
                    Assert.GreaterOrEqual(beat.at - beat.ring, -0.001f, $"{pattern.id} ring can't start before the pattern");
                    Assert.Greater(beat.ring, timing.goodWindow, $"{pattern.id} ring must be visible before its window opens");
                    Assert.That(beat.x, Is.InRange(0.55f, 1f), $"{pattern.id}: every tap stays on the enemies' side of the screen");
                    if (beat.kind == "drag") Assert.That(beat.endX, Is.InRange(0.55f, 1f), $"{pattern.id}: drags too");
                    Assert.That(beat.y, Is.InRange(0f, 1f));
                    if (beat.IsSwipe) Assert.AreEqual(1f, Mathf.Abs(beat.DirX) + Mathf.Abs(beat.DirY), $"{pattern.id} swipe needs a direction");
                    if (beat.IsDrag)
                    {
                        Assert.Greater(beat.duration, 0f, pattern.id);
                        Assert.That(beat.endX, Is.InRange(0f, 1f));
                        Assert.That(beat.endY, Is.InRange(0f, 1f));
                    }
                    previous = beat.End;
                }
            }

            foreach (var tier in new[] { 0, 1, 2, 3, 5, 9 })
            {
                foreach (var attacking in new[] { true, false })
                {
                    var random = new GameRandom(tier + 3);
                    var seen = new HashSet<string>();
                    string last = null;
                    for (var i = 0; i < 80; i++)
                    {
                        var pick = TimingSequence.Pick(timing, tier, attacking, random, last);
                        Assert.AreNotEqual(last, pick.id, "Patterns shouldn't repeat back to back");
                        Assert.LessOrEqual(pick.minTier, tier);
                        seen.Add(pick.id);
                        last = pick.id;
                    }
                    Assert.GreaterOrEqual(seen.Count, 4, $"Tier {tier} should offer several patterns");
                }
            }
        }

        [Test]
        public void HigherTiersUnlockHarderPatterns()
        {
            int MaxBeats(int tier)
            {
                var most = 0;
                foreach (var pattern in content.timing.patterns)
                {
                    if (pattern.minTier <= tier) most = Math.Max(most, pattern.beats.Length);
                }
                return most;
            }
            Assert.Greater(MaxBeats(5), MaxBeats(0));
        }

        [Test]
        public void SaveRoundTripsThroughJson()
        {
            var save = CrewedSave();
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
