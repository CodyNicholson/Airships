using System;
using System.Collections.Generic;
using Airships.Core;
using Airships.Game;
using NUnit.Framework;
using UnityEngine;

namespace Airships.Tests
{
    public class CampaignTests
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
            for (var i = 0; battle.Current != actor; i++)
            {
                Assert.Less(i, 100, $"{actor.Name} never got a turn");
                battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            }
        }

        SaveData Started()
        {
            var save = SaveData.NewGame(content);
            Campaign.AddShip(save, "rust_wing", true);
            return save;
        }

        DogfightSim Duel(out ShipState a, out ShipState b, float distance = 40f)
        {
            var sim = new DogfightSim(5, content.skyRules);
            var save = Started();
            a = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "rust_wing"), 0f, 0f, Mathf.PI * 0.5f);
            b = sim.AddShip(1, Encounter.EnemyLoadout(content, "scrap_brig", 0), 0f, distance, Mathf.PI * 0.5f);
            return sim;
        }

        static void Run(DogfightSim sim, float seconds, ShipCommand first = default)
        {
            var commands = new List<ShipCommand>();
            foreach (var _ in sim.Ships) commands.Add(default);
            commands[0] = first;
            for (var i = 0; i < seconds * DogfightSim.TicksPerSecond; i++) sim.Step(commands);
        }

        SkyEncounter Sky(string objective, params SkySpawn[] spawns)
        {
            var save = Started();
            var config = new SkyConfig
            {
                Content = content,
                Seed = 9,
                Player = Encounter.PlayerLoadout(content, save, "rust_wing"),
                PlayerShipName = "Test",
                PlayerHull = 400f,
                Objective = objective,
                Weather = "heist",
                Phase = new PhaseDef { kind = "sky", objective = objective },
            };
            config.Enemies.AddRange(spawns);
            return new SkyEncounter(config);
        }

        [Test]
        public void EveryCampaignReferenceResolves()
        {
            var campaign = content.campaign;
            Assert.IsNotEmpty(campaign.missions);
            foreach (var mission in campaign.missions)
            {
                Assert.IsNotEmpty(mission.phases, mission.id);
                if (!string.IsNullOrEmpty(mission.requires)) Assert.NotNull(campaign.Mission(mission.requires));
                if (mission.recruits != null) foreach (var id in mission.recruits) Assert.NotNull(content.Crew(id));
                if (mission.ships != null) foreach (var id in mission.ships) Assert.NotNull(content.Ship(id));
                if (mission.signatures != null) foreach (var id in mission.signatures) Assert.NotNull(content.Signature(id));
                foreach (var phase in mission.phases)
                {
                    Assert.Contains(phase.kind, new[] { "story", "sky", "deck", "assign", "choice" }, mission.id);
                    if (phase.lines != null) foreach (var line in phase.lines) Assert.NotNull(campaign.Speaker(line.speaker), $"{mission.id}: {line.speaker}");
                    if (phase.teach != null) foreach (var teach in phase.teach) Assert.NotNull(campaign.Speaker(teach.speaker), $"{mission.id}: {teach.speaker}");
                    if (phase.kind == "sky")
                    {
                        Assert.NotNull(content.Weather(phase.weather), mission.id);
                        if (!string.IsNullOrEmpty(phase.ship)) Assert.NotNull(content.Ship(phase.ship));
                        foreach (var enemy in phase.enemies) Assert.NotNull(content.Ship(enemy.shipId), mission.id);
                    }
                    if (phase.kind == "deck") foreach (var enemy in phase.crew)
                    {
                        Assert.NotNull(content.Class(enemy.classId), mission.id);
                        foreach (var unlock in content.Class(enemy.classId).skills) Assert.NotNull(content.Skill(unlock.skill));
                    }
                    if (phase.kind == "choice")
                    {
                        Assert.NotNull(content.Ship(phase.shipId));
                        if (!string.IsNullOrEmpty(phase.officer)) Assert.NotNull(content.Crew(phase.officer));
                        if (!string.IsNullOrEmpty(phase.module)) Assert.NotNull(content.Module(phase.module));
                        if (!string.IsNullOrEmpty(phase.signature)) Assert.NotNull(content.Signature(phase.signature));
                    }
                }
            }
            foreach (var talk in campaign.talks)
            {
                Assert.NotNull(content.Crew(talk.crew));
                Assert.NotNull(campaign.Mission(talk.after));
            }
            foreach (var ammo in content.ammo) Assert.Greater(ammo.reloadScale, 0f, ammo.id);
        }

        [Test]
        public void JumpingToAChapterGivesWhatTheStoryWouldHaveByThen()
        {
            var save = SaveData.NewGame(content);
            var target = System.Array.Find(content.campaign.missions, m => m.id == "a3_tartarus");
            Campaign.FastForwardTo(save, content, target);
            foreach (var mission in content.campaign.missions)
            {
                if (mission == target) break;
                if (mission.IsStory) Assert.IsTrue(Campaign.IsDone(save, mission.id), mission.id);
            }
            Assert.IsFalse(Campaign.IsDone(save, target.id), "The chapter itself is still to play");
            Assert.AreEqual(target, Campaign.NextStoryMission(save, content));
            foreach (var crew in new[] { "rook", "lyra", "sable" }) Assert.IsNotNull(save.crew.Find(c => c.id == crew), crew);
            Assert.IsNull(save.crew.Find(c => c.id == "thorne"), "Thorne joins in this chapter, not before it");
            Assert.IsTrue(save.fleet.Exists(s => s.id == "rust_wing"));
            Assert.IsNotNull(save.crew.Find(c => c.id == "brill"), "Officers from earlier choices come aboard");
            var gold = save.gold;
            Campaign.FastForwardTo(save, content, target);
            Assert.AreEqual(gold, save.gold, "Jumping again doesn't pay out twice");
        }

        [Test]
        public void ThePlansActsAndRewardsAreAllPresent()
        {
            var acts = new HashSet<int>();
            var rewards = new HashSet<string>();
            var recruits = new HashSet<string>();
            foreach (var mission in content.campaign.missions)
            {
                acts.Add(mission.act);
                if (mission.ships != null) foreach (var ship in mission.ships) rewards.Add(ship);
                if (mission.recruits != null) foreach (var crew in mission.recruits) recruits.Add(crew);
                foreach (var phase in mission.phases)
                {
                    if (phase.kind == "choice") rewards.Add(phase.shipId);
                    if (phase.kind == "choice" && !string.IsNullOrEmpty(phase.officer)) recruits.Add(phase.officer);
                }
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, acts);
            foreach (var ship in new[] { "rust_wing", "iron_maw", "mirage", "bastion", "sovereign_gale" }) Assert.IsTrue(rewards.Contains(ship), ship);
            foreach (var crew in new[] { "rook", "lyra", "sable", "thorne", "morrow" }) Assert.IsTrue(recruits.Contains(crew), crew);
            Assert.AreEqual("p_mutiny", content.campaign.missions[0].id, "The game opens in the prologue");
        }

        [Test]
        public void NewGamesStartAloneWithNoShip()
        {
            var save = SaveData.NewGame(content);
            Assert.AreEqual(1, save.crew.Count);
            Assert.AreEqual(Campaign.Protagonist, save.party[0]);
            Assert.AreEqual(0, save.fleet.Count);
            Assert.AreEqual("p_mutiny", Campaign.NextStoryMission(save, content).id);
        }

        [Test]
        public void HooksLandOnlyOnceAShipIsDownToItsLastPointOfHull()
        {
            var sim = Duel(out var a, out var b);
            Assert.IsFalse(sim.CanBoard(a, b), "A healthy ship can't be boarded");
            b.Engines = 0f;
            b.Hull = 2f;
            Assert.IsFalse(sim.CanBoard(a, b), "Dead engines and a nearly beaten hull aren't enough");
            b.Hull = DogfightSim.BoardableHull;
            Assert.IsTrue(sim.CanBoard(a, b));
            b.X = 500f;
            Assert.IsFalse(sim.CanBoard(a, b), "Out of range");
        }

        [Test]
        public void AmmoTypesDoDifferentJobs()
        {
            var chain = FireOneVolley("chain");
            var round = FireOneVolley("round");
            var fire = FireOneVolley("fire");
            Assert.Less(chain.Engines, chain.MaxSubsystem, "Chain-shot wrecks sails");
            Assert.Greater(chain.Hull, round.Hull, "Chain-shot barely hurts the hull");
            Assert.Greater(fire.DeckDamage, 0f, "Fire-shot wounds the crew on deck");
            Assert.Greater(fire.BurnTimer, 0f);
            Assert.AreEqual(0f, round.DeckDamage);
        }

        ShipState FireOneVolley(string ammo)
        {
            var sim = Duel(out var a, out var b, 30f);
            a.Ammo = content.Ammo(ammo);
            Run(sim, 0.1f, new ShipCommand { FireBroadside = true });
            Run(sim, 1.5f);
            return b;
        }

        [Test]
        public void MortarsReachAltitudesRoundShotCannot()
        {
            float HullAfter(string ammo)
            {
                var sim = Duel(out var a, out var b, 40f);
                b.Y = 40f;
                b.AltitudeLocked = true;
                a.AltitudeLocked = true;
                a.Ammo = content.Ammo(ammo);
                Run(sim, 0.1f, new ShipCommand { FireBroadside = true });
                Run(sim, 2f);
                return b.Hull;
            }
            Assert.Less(HullAfter("mortar"), HullAfter("round"));
        }

        [Test]
        public void FlakCatchesWhatRoundShotMisses()
        {
            Assert.Greater(content.Ammo("flak").hitRadiusBonus, content.Ammo("round").hitRadiusBonus);
        }

        [Test]
        public void MultipleHooksMustLandAndDriftingSnapsThem()
        {
            var sim = Duel(out var a, out var b);
            b.Hull = DogfightSim.BoardableHull;
            b.HooksRequired = 3;
            b.Speed = 0f;
            Run(sim, 0.1f, new ShipCommand { Grapple = true });
            Assert.AreEqual(1, b.Hooks);
            Assert.AreEqual(-1, sim.BoardedShipId);
            Run(sim, 0.6f, new ShipCommand { Grapple = true });
            Run(sim, 0.6f, new ShipCommand { Grapple = true });
            Assert.AreEqual(b.Id, sim.BoardedShipId, "The third hook boards");

            var other = Duel(out var c, out var d);
            d.Hull = DogfightSim.BoardableHull;
            d.HooksRequired = 3;
            Run(other, 0.1f, new ShipCommand { Grapple = true });
            Assert.AreEqual(1, d.Hooks);
            d.X = 900f;
            Run(other, 0.1f);
            Assert.AreEqual(0, d.Hooks, "Drifting away snaps the lines");
        }

        [Test]
        public void KragsHarpoonDragsYouDownUntilYouRideIt()
        {
            var encounter = Sky("capture", new SkySpawn { key = "krag", shipId = "iron_maw", name = "Krag", ai = "krag", target = true, unsinkable = true, harpoonAfter = 0.1f, hooks = 3 });
            for (var i = 0; i < 90 && encounter.Target.Ship.TetheredTo < 0; i++) encounter.Step(default);
            Assert.AreEqual(encounter.Player.Id, encounter.Target.Ship.TetheredTo);
            var startAltitude = encounter.Player.Y;
            var hull = encounter.Player.Hull;
            for (var i = 0; i < 30 * 4; i++) encounter.Step(default);
            Assert.Less(encounter.Player.Y, startAltitude, "Dragged downward");
            Assert.IsFalse(encounter.Sim.CanBoard(encounter.Player, encounter.Target.Ship), "A healthy main enemy can't be boarded, line or no line");
            for (var i = 0; i < 30 * 25 && encounter.Outcome == SkyOutcome.Ongoing; i++) encounter.Step(default);
            Assert.Less(encounter.Player.Hull, hull, "Staying on the line ends in the crush");

            var rider = Sky("capture", new SkySpawn { key = "krag", shipId = "iron_maw", name = "Krag", ai = "krag", target = true, unsinkable = true, harpoonAfter = 0.1f, hooks = 3 });
            for (var i = 0; i < 90 && rider.Target.Ship.TetheredTo < 0; i++) rider.Step(default);
            rider.Target.Ship.Hull = rider.Target.Ship.Loadout.Hull * 0.2f;
            rider.Step(new ShipCommand { Grapple = true });
            Assert.AreEqual(SkyOutcome.Ongoing, rider.Outcome, "Not until he's down to his last point of hull");
            rider.Target.Ship.Hull = DogfightSim.BoardableHull;
            rider.Step(new ShipCommand { Grapple = true });
            Assert.AreEqual(SkyOutcome.Boarded, rider.Outcome, "One grapple climbs the tether once they're beaten");
        }

        [Test]
        public void YourOwnHarpoonNeedsABeatenTarget()
        {
            var sim = Duel(out var a, out var b);
            a.Loadout.Signature = content.Signature("harpoon_winch");
            Run(sim, 0.1f, new ShipCommand { Signature = true });
            Assert.AreEqual(b.Id, a.TetheredTo);
            Assert.IsFalse(sim.CanBoard(a, b), "Can't jump a healthy ship just by hitting it");
            b.Hull = b.Loadout.Hull * content.skyRules.tetherBoardShare * 0.9f;
            Assert.IsFalse(sim.CanBoard(a, b), "A line doesn't skip the beating");
            b.Hull = DogfightSim.BoardableHull;
            Assert.IsTrue(sim.CanBoard(a, b));
            Run(sim, content.Signature("harpoon_winch").seconds + 0.5f);
            Assert.AreEqual(-1, a.TetheredTo, "The line slips off in time");
        }

        [Test]
        public void SignatureWeaponsDoTheirJobs()
        {
            var burner = Duel(out var a, out _);
            a.Loadout.Signature = content.Signature("afterburner");
            var cruising = a.Loadout.Speed;
            Run(burner, 2f, new ShipCommand { Signature = true, Throttle = 1f });
            Assert.Greater(a.Speed, cruising * 1.3f, "Afterburner");

            var ram = Duel(out var c, out var d, 26f);
            c.Loadout.Signature = content.Signature("ramming_prow");
            c.Ramming = true;
            c.BoostTimer = 2f;
            c.Hull = 400f;
            var before = d.Hull;
            d.Z = 5f;
            Run(ram, 0.2f);
            Assert.Greater(before - d.Hull, 400f - c.Hull, "A ram hurts them far more");

            var thunder = Duel(out var e, out _, 30f);
            e.Loadout.Signature = content.Signature("thunder_volley");
            e.BroadsideCooldown = 4f;
            Run(thunder, 0.1f, new ShipCommand { Signature = true });
            Assert.Greater(thunder.Projectiles.Count, 0, "Thunder fires even mid-reload");
        }

        [Test]
        public void UnsinkableCaptainsCanBeCrippledButNotSunk()
        {
            var sim = Duel(out _, out var b);
            b.HullFloor = 1f;
            b.TakeDamage(99999f);
            Assert.AreEqual(1f, b.Hull);
            Assert.IsTrue(b.Alive);
            Assert.IsTrue(sim.IsBoardable(b));
        }

        [Test]
        public void GlidersLandBoardersUnlessShotDown()
        {
            var sim = Duel(out var a, out var b, 300f);
            var glider = sim.LaunchGlider(b, Encounter.EnemyLoadout(content, "assault_glider", 0));
            glider.X = a.X;
            glider.Z = a.Z + 2f;
            Run(sim, 0.1f);
            Assert.AreEqual(1, sim.BoardersLanded);
            Assert.IsFalse(glider.Alive);

            var encounter = Sky("repel", new SkySpawn { key = "carrier", shipId = "armada_frigate", name = "Carrier", target = true, unsinkable = true });
            encounter.Config.Phase.gliders = 0;
            for (var i = 0; i < 3; i++) encounter.Step(default);
            Assert.AreEqual(SkyOutcome.Won, encounter.Outcome, "With nothing left flying, the wave is repelled");
        }

        [Test]
        public void CloudsHideShipsAndWindsAndUpdraftsMoveThem()
        {
            var def = content.Weather("maelstrom");
            var weather = new SkyWeather(def, content.skyRules, 4);
            var sim = new DogfightSim(4, content.skyRules);
            var save = Started();
            var ship = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "rust_wing"), 0f, 0f, 0f);
            sim.AddShip(1, Encounter.EnemyLoadout(content, "scrap_raider", 0), 300f, 300f, 0f);
            sim.SetWeather(weather);

            var wind = new List<SkyZone>(weather.Zones).Find(z => z.Kind == SkyZoneKind.Wind);
            ship.X = wind.X;
            ship.Y = wind.Y;
            ship.Z = wind.Z;
            ship.Heading = wind.Heading;
            Assert.AreEqual(content.skyRules.windSpeedScale, weather.WindScale(ship, content.skyRules.windSpeedScale), "With the wind");
            ship.Heading = wind.Heading + Mathf.PI;
            Assert.AreEqual(1f, weather.WindScale(ship, content.skyRules.windSpeedScale), "Against the wind");

            var updraft = new List<SkyZone>(weather.Zones).Find(z => z.Kind == SkyZoneKind.TornadoUp);
            ship.X = updraft.X;
            ship.Z = updraft.Z;
            ship.Y = 0f;
            ship.Speed = 0f;
            ship.ClimbRate = 0f;
            var events = new List<DogfightEvent>();
            weather.Step(sim, events);
            Assert.Greater(ship.Y, 0f);

            var cloudy = content.Weather("jetstream");
            var hideout = new SkyWeather(cloudy, content.skyRules, 4);
            var cloud = new List<SkyZone>(hideout.Zones).Find(z => z.Kind == SkyZoneKind.Cloud);
            ship.X = cloud.X;
            ship.Z = cloud.Z;
            ship.Y = cloud.Y;
            hideout.Step(sim, events);
            Assert.IsTrue(ship.Hidden);
            Assert.IsNull(sim.Opponent(sim.Ships[1]), "Enemies can't find a hidden ship");
        }

        [Test]
        public void LightningWarnsThenStrikes()
        {
            var def = content.Weather("maelstrom");
            var weather = new SkyWeather(def, content.skyRules, 4);
            var sim = new DogfightSim(4, content.skyRules);
            var save = Started();
            var ship = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "rust_wing"), 0f, 0f, 0f);
            sim.SetWeather(weather);
            var warned = false;
            var events = new List<DogfightEvent>();
            for (var i = 0; i < 30 * 12 && !warned; i++)
            {
                events.Clear();
                weather.Step(sim, events);
                warned = events.Exists(e => e.Kind == DogfightEventKind.LightningWarning);
            }
            Assert.IsTrue(warned);
            var strike = weather.Strikes[0];
            ship.X = strike.X;
            ship.Z = strike.Z;
            var hull = ship.Hull;
            for (var i = 0; i < 30 * 2; i++)
            {
                ship.X = strike.X; // Standing still, even if a wind tunnel happens to run through here.
                ship.Z = strike.Z;
                weather.Step(sim, events);
            }
            Assert.Less(ship.Hull, hull, "Standing where the warning was costs you");
        }

        [Test]
        public void ThornesPatchWorksOncePerFight()
        {
            var sim = Duel(out var a, out _);
            a.PatchesLeft = 1;
            a.Hull = 100f;
            Run(sim, 0.1f, new ShipCommand { Patch = true });
            Assert.Greater(a.Hull, 100f);
            var patched = a.Hull;
            Run(sim, 0.1f, new ShipCommand { Patch = true });
            Assert.AreEqual(patched, a.Hull, 1f, "Only one patch per fight");
        }

        [Test]
        public void WeakPointsOnlyMatterFromBehind()
        {
            float Damage(bool stern, bool knowsWeakPoints)
            {
                var sim = Duel(out var a, out var b, 30f);
                a.WeakPoints = knowsWeakPoints;
                a.Heading = 0f;
                b.Heading = stern ? 0f : Mathf.PI;
                b.Loadout.Speed = 0f;
                b.Speed = 0f;
                var before = b.Hull;
                a.Loadout.TorpedoDamageScale = 3f;
                Run(sim, 0.1f, new ShipCommand { FireTorpedo = true });
                Run(sim, 0.5f);
                return before - b.Hull;
            }
            Assert.Greater(Damage(true, true), Damage(true, false), "A shot at the stern finds the weak point");
            Assert.AreEqual(Damage(false, false), Damage(false, true), 0.5f, "A shot at the nose doesn't");
        }

        [Test]
        public void SablesChargesForceASurrender()
        {
            var party = new List<Combatant> { Combatant.Create("sable", "Sable", content.Class("saboteur"), content.Crew("sable").baseStats, 1, content, true, 0) };
            var enemies = new List<Combatant> { Combatant.Create("boss", "Boss", content.Class("warlord"), content.Class("warlord").baseStats, 1, content, false, 0) };
            var battle = new PartyBattle(party, enemies, 1) { SurrenderAfterRound = 2 };
            party[0].Stats.hp = 99999f;
            party[0].Hp = 99999;
            var guard = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && guard++ < 50) battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            Assert.AreEqual(BattleOutcome.Victory, battle.Outcome);
            Assert.IsTrue(battle.Surrendered);
        }

        [Test]
        public void SparingWinsWhenTheBodyguardsFallAndLosesIfYouKill()
        {
            PartyBattle Fresh(out Combatant spare, out Combatant guard, out Combatant hero)
            {
                hero = Combatant.Create("vane", "Vane", content.Class("buccaneer"), content.Crew("vane").baseStats, 20, content, true, 0);
                guard = Combatant.Create("g", "Guard", content.Class("brigand"), content.Class("brigand").baseStats, 1, content, false, 0);
                spare = Combatant.Create("sable", "Sable", content.Class("corsair"), content.Class("corsair").baseStats, 1, content, false, 1);
                var battle = new PartyBattle(new List<Combatant> { hero }, new List<Combatant> { guard, spare }, 3) { SpareTarget = spare };
                return battle;
            }

            var mercy = Fresh(out var keep, out var bodyguard, out var vane);
            bodyguard.Hp = 1;
            WaitForTurn(mercy, vane);
            mercy.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = bodyguard });
            Assert.AreEqual(BattleOutcome.Victory, mercy.Outcome);
            Assert.IsTrue(keep.Alive);

            var ruthless = Fresh(out var victim, out _, out var hero2);
            victim.Hp = 1;
            WaitForTurn(ruthless, hero2);
            ruthless.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = victim });
            Assert.AreEqual(BattleOutcome.Defeat, ruthless.Outcome);
            Assert.IsTrue(ruthless.SpareKilled);

            var sweep = Fresh(out var safe, out _, out var hero3);
            safe.Hp = 1;
            WaitForTurn(sweep, hero3);
            sweep.Submit(new BattleAction { Kind = BattleActionKind.Skill, Skill = content.Skill("cleave") });
            Assert.IsTrue(safe.Alive, "Sweeping attacks pass over someone you're sparing");
        }

        [Test]
        public void DavyJonesChoiceHasThreeDifferentResults()
        {
            var phase = new PhaseDef { kind = "choice", shipId = "iron_maw", gold = 300, officer = "hask", module = "winch_motor", signature = "harpoon_winch" };

            var seize = Started();
            Campaign.ApplyChoice(content, seize, phase, DavyJonesChoice.SeizeTheHelm);
            Assert.AreEqual("iron_maw", seize.activeShip);
            Assert.IsNotNull(seize.fleet.Find(s => s.id == "rust_wing"), "The old ship stays in the fleet");

            var press = Started();
            press.fleet[0].damage = 200f;
            Campaign.ApplyChoice(content, press, phase, DavyJonesChoice.PressGang);
            Assert.IsNotNull(press.crew.Find(c => c.id == "hask"));
            Assert.AreEqual(100f, press.fleet[0].damage, 0.01f, "Half the damage repaired");
            Assert.AreEqual("rust_wing", press.activeShip);

            var scuttle = Started();
            var gold = scuttle.gold;
            Campaign.ApplyChoice(content, scuttle, phase, DavyJonesChoice.ScuttleAndPlunder);
            Assert.AreEqual(gold + 300, scuttle.gold);
            Assert.Contains("winch_motor", scuttle.campaign.modules);
            Assert.IsNull(scuttle.fleet.Find(s => s.id == "iron_maw"));

            foreach (var save in new[] { seize, press, scuttle }) Assert.AreEqual("harpoon_winch", save.campaign.equippedSignature, "The boss's weapon is always yours");
        }

        [Test]
        public void SinkingPaysScrapAndBoardingPaysInFull()
        {
            var mission = new MissionDef { id = "m", gold = 100, xp = 0, phases = new[] { new PhaseDef { kind = "story" } } };
            var captured = Started();
            var startGold = captured.gold;
            Campaign.Complete(content, captured, mission, false, null);
            Assert.AreEqual(startGold + 100, captured.gold);

            var sunk = Started();
            Campaign.Complete(content, sunk, mission, true, null);
            Assert.AreEqual(startGold + 20, sunk.gold, "20% scrap");
        }

        SkyEncounter FreeSkies(SaveData save)
        {
            var config = new SkyConfig
            {
                Content = content,
                Seed = 11,
                Player = Encounter.PlayerLoadout(content, save, "rust_wing"),
                PlayerShipName = "Test",
                PlayerHull = 400f,
                AllControls = true,
                Weather = "clear",
                Patches = Campaign.PatchesFor(save),
            };
            config.Enemies.Add(new SkySpawn { shipId = "scrap_raider", name = "Raider", target = true });
            return new SkyEncounter(config);
        }

        [Test]
        public void ARepairKitPatchesTheHullInFreeSkiesAndIsSpentOnlyIfUsed()
        {
            var save = Started();
            save.campaign.mechanics.Remove("patch");
            Assert.AreEqual(0, Campaign.PatchesFor(save));
            Assert.IsFalse(FreeSkies(save).HasControl("patch"), "Nothing to patch with");

            save.AddItem("repair_kit", 2);
            Assert.AreEqual(1, Campaign.PatchesFor(save), "Only one kit counts per fight");
            var unused = FreeSkies(save);
            Assert.IsTrue(unused.HasControl("patch"), "A kit in the hold brings the patch button, taught or not");
            Campaign.SpendRepairKit(save, unused);
            Assert.AreEqual(2, save.ItemCount("repair_kit"), "A kit you never used stays in the hold");

            var used = FreeSkies(save);
            used.Player.Hull = used.Player.Loadout.Hull * 0.5f;
            var hull = used.Player.Hull;
            used.Step(new ShipCommand { Patch = true });
            Assert.Greater(used.Player.Hull, hull, "The patch mends the hull");
            Campaign.SpendRepairKit(save, used);
            Assert.AreEqual(1, save.ItemCount("repair_kit"));
        }

        [Test]
        public void AQuickDogfightRaiderIsBeatenDownAndBoardedNeverSunk()
        {
            var save = Started();
            var config = new SkyConfig
            {
                Content = content,
                Seed = 9,
                Player = Encounter.PlayerLoadout(content, save, "rust_wing"),
                PlayerShipName = "Test",
                PlayerHull = 400f,
                AllControls = true,
                Weather = "clear",
            };
            config.Enemies.Add(new SkySpawn { shipId = "scrap_raider", name = "Raider", target = true });
            var encounter = new SkyEncounter(config);
            var raider = encounter.Target.Ship;
            raider.X = encounter.Player.X;
            raider.Y = encounter.Player.Y;
            raider.Z = encounter.Player.Z + 30f;

            raider.Engines = 0f;
            raider.Hull = raider.Loadout.Hull * 0.1f;
            Assert.IsFalse(encounter.Sim.CanBoard(encounter.Player, raider), "Not until it's beaten all the way down");
            raider.TakeDamage(raider.Loadout.Hull * 10f);
            Assert.AreEqual(DogfightSim.BoardableHull, raider.Hull, "A ship you can board never sinks");
            Assert.IsTrue(raider.Alive);
            encounter.Step(default);
            Assert.AreEqual(SkyOutcome.Ongoing, encounter.Outcome);
            encounter.Step(new ShipCommand { Grapple = true });
            Assert.AreEqual(SkyOutcome.Boarded, encounter.Outcome);
        }

        [Test]
        public void TheMainEnemyCantBeSunkAndOnlyBoardsAtItsLastPointOfHull()
        {
            var encounter = Sky("capture", new SkySpawn { key = "main", shipId = "scrap_raider", name = "Main", target = true }, new SkySpawn { key = "escort", shipId = "scrap_raider", name = "Escort" });
            var main = encounter.Main.Ship;
            var escort = encounter.Enemies[1].Ship;
            main.X = encounter.Player.X;
            main.Y = encounter.Player.Y;
            main.Z = encounter.Player.Z + 30f;
            escort.X = encounter.Player.X + 20f;
            escort.Y = encounter.Player.Y;
            escort.Z = encounter.Player.Z + 30f;

            main.Hull = main.Loadout.Hull * 0.1f;
            Assert.IsFalse(encounter.Sim.CanBoard(encounter.Player, main), "Badly hurt isn't beaten");
            main.Engines = 0f;
            Assert.IsFalse(encounter.Sim.CanBoard(encounter.Player, main), "Dead engines don't open the way");
            main.TakeDamage(main.Loadout.Hull * 10f);
            Assert.IsTrue(encounter.Sim.CanBoard(encounter.Player, main), "Beaten down to its last point of hull");

            escort.Hull = 1f;
            escort.Engines = 0f;
            Assert.IsFalse(encounter.Sim.CanBoard(encounter.Player, escort), "Others can only be shot down");

            main.TakeDamage(main.Loadout.Hull * 10f);
            Assert.AreEqual(1f, main.Hull, "Always keeps 1 hull");
            Assert.IsTrue(main.Alive);
            escort.TakeDamage(10f);
            Assert.IsFalse(escort.Alive, "Everyone else can be sunk");
            encounter.Step(default);
            Assert.AreEqual(SkyOutcome.Ongoing, encounter.Outcome, "Only boarding ends the fight");
            encounter.Step(new ShipCommand { Grapple = true });
            Assert.AreEqual(SkyOutcome.Boarded, encounter.Outcome);
            Assert.AreEqual("main", encounter.BoardedKey);
        }

        [Test]
        public void SinkingTheEscortsDoesntEndTheChapter()
        {
            var encounter = Sky("capture", new SkySpawn { key = "main", shipId = "scrap_raider", name = "Main", target = true }, new SkySpawn { key = "escort", shipId = "scrap_raider", name = "Escort" });
            encounter.Enemies[1].Ship.TakeDamage(99999f);
            for (var i = 0; i < 30; i++) encounter.Step(default);
            Assert.AreEqual(SkyOutcome.Ongoing, encounter.Outcome);
        }

        [Test]
        public void ChaptersWithAFightToWinHaveOneMainEnemyToBoard()
        {
            foreach (var mission in content.campaign.missions)
            {
                for (var i = 0; i < mission.phases.Length; i++)
                {
                    var phase = mission.phases[i];
                    if (phase.kind != "sky") continue;
                    if (phase.objective == "capture")
                    {
                        Assert.AreEqual(1, System.Array.FindAll(phase.enemies, e => e.target && !e.ally).Length, $"{mission.id} needs exactly one main enemy");
                        Assert.Less(i + 1, mission.phases.Length, mission.id);
                        Assert.AreEqual("deck", mission.phases[i + 1].kind, $"{mission.id}: boarding leads to a deck fight");
                    }
                    else if (phase.objective == "sink_all")
                    {
                        Assert.AreEqual("p_mutiny", mission.id, $"{mission.id} is a fight with no main enemy");
                    }
                }
            }
        }

        [Test]
        public void EveryPaintedPortraitExists()
        {
            foreach (var look in content.looks)
            {
                if (string.IsNullOrEmpty(look.portrait)) continue;
                Assert.NotNull(Resources.Load<Texture2D>($"Art/Portraits/T_Portrait_{look.portrait}_HQ"), look.portrait);
                Assert.NotNull(Resources.Load<Texture2D>($"Art/Portraits/T_Portrait_{look.portrait}_LQ"), look.portrait);
            }
            foreach (var speaker in content.campaign.speakers)
            {
                var portrait = !string.IsNullOrEmpty(speaker.portrait) ? speaker.portrait : content.Look(speaker.look)?.portrait;
                Assert.IsFalse(string.IsNullOrEmpty(portrait), $"{speaker.name} has no painted portrait");
                Assert.NotNull(Resources.Load<Sprite>($"Art/Portraits/T_Portrait_{portrait}_HQ"), $"{portrait} must import as a sprite");
            }
        }

        [Test]
        public void TheTorpedoOutrunsEveryShipButCanBeSeenInFlight()
        {
            var tube = content.Cannon(Encounter.PlayerTorpedoCannon);
            Assert.AreEqual("Torpedo", tube.name);
            foreach (var ship in content.ships) Assert.Greater(tube.projectileSpeed, ship.speed, ship.id);
            Assert.Less(tube.projectileSpeed, content.Cannon("chase_guns").projectileSpeed * 0.6f, "Slow enough to watch");
            Assert.GreaterOrEqual(tube.range / tube.projectileSpeed, 4f, "Stays in the air long enough to watch");
            Assert.Greater(tube.damage, content.Cannon("chase_guns").damage * 5f);

            var save = Started();
            Assert.AreEqual(tube, Encounter.PlayerLoadout(content, save, "rust_wing").Torpedo);
            Assert.AreEqual(content.Cannon("chase_guns"), Encounter.EnemyLoadout(content, "rust_wing", 0).ChaseGuns, "Enemies keep their own guns");
        }

        [Test]
        public void ATorpedoHomesInAndHitsLikeAHeavyShot()
        {
            var encounter = Sky("capture", new SkySpawn { key = "t", shipId = "scrap_raider", name = "T", target = true });
            var player = encounter.Player;
            player.Loadout = Encounter.PlayerLoadout(content, Started(), "rust_wing");
            var target = encounter.Target.Ship;
            target.X = player.X + 12f;
            target.Y = player.Y;
            target.Z = player.Z + 120f;
            target.Loadout.Speed = 0f;
            target.Speed = 0f;
            var before = target.Hull;

            var hit = false;
            encounter.Step(new ShipCommand { FireTorpedo = true, Throttle = 0.35f });
            ProjectileState torpedo = null;
            foreach (var shot in encounter.Sim.Projectiles)
            {
                if (shot.AmmoId == "torpedo") torpedo = shot;
            }
            Assert.NotNull(torpedo);
            Assert.IsTrue(torpedo.Heavy);
            Assert.AreEqual(target.Id, torpedo.HomingOn);
            for (var i = 0; i < 30 * 6 && !hit; i++)
            {
                encounter.Step(default);
                foreach (var e in encounter.Sim.Events) hit |= e.Kind == DogfightEventKind.Hit && e.AmmoId == "torpedo" && e.ShipId == target.Id;
            }
            Assert.IsTrue(hit);
            Assert.Less(target.Hull, before - 40f);
        }

        [Test]
        public void ATorpedoBarelyBendsSoItStillTakesAim()
        {
            var tube = content.Cannon(Encounter.PlayerTorpedoCannon);
            Assert.LessOrEqual(tube.aimAssistDegrees, 10f, "The nose has to be nearly on target");

            var encounter = Sky("capture", new SkySpawn { key = "t", shipId = "scrap_raider", name = "T", target = true });
            var player = encounter.Player;
            player.Loadout = Encounter.PlayerLoadout(content, Started(), "rust_wing");
            var target = encounter.Target.Ship;
            target.Loadout.Speed = 0f;
            target.Speed = 0f;
            target.X = player.X;
            target.Y = player.Y;
            target.Z = player.Z + 200f;
            encounter.Step(new ShipCommand { FireTorpedo = true, Throttle = 0.35f });
            ProjectileState torpedo = null;
            foreach (var shot in encounter.Sim.Projectiles)
            {
                if (shot.AmmoId == "torpedo") torpedo = shot;
            }
            Assert.NotNull(torpedo);
            Assert.AreEqual(target.Id, torpedo.HomingOn);
            var start = new Vector3(torpedo.VelocityX, torpedo.VelocityY, torpedo.VelocityZ);

            // The target dodges well off the line, and the torpedo can't follow it.
            target.X += 70f;
            var hit = false;
            var turned = 0f;
            for (var i = 0; i < 30 * 4 && !hit; i++)
            {
                encounter.Step(default);
                foreach (var e in encounter.Sim.Events) hit |= e.Kind == DogfightEventKind.Hit && e.AmmoId == "torpedo" && e.ShipId == target.Id;
                turned = Mathf.Max(turned, Vector3.Angle(start, new Vector3(torpedo.VelocityX, torpedo.VelocityY, torpedo.VelocityZ)));
            }
            Assert.IsFalse(hit, "A real dodge beats it");
            Assert.Greater(turned, 0.5f, "It does bend a little");
            Assert.Less(turned, 20f, "But not much");
        }

        [Test]
        public void ATorpedoLeavesItsOwnShipBehindEvenAtFullBurn()
        {
            var encounter = Sky("capture", new SkySpawn { key = "t", shipId = "scrap_raider", name = "T", target = true });
            var player = encounter.Player;
            player.Loadout = Encounter.PlayerLoadout(content, Started(), "mirage");
            player.Speed = 180f;
            encounter.Step(new ShipCommand { FireTorpedo = true, Throttle = 1f });
            ProjectileState torpedo = null;
            foreach (var shot in encounter.Sim.Projectiles)
            {
                if (shot.AmmoId == "torpedo") torpedo = shot;
            }
            Assert.NotNull(torpedo);
            var speed = Mathf.Sqrt(torpedo.VelocityX * torpedo.VelocityX + torpedo.VelocityZ * torpedo.VelocityZ + torpedo.VelocityY * torpedo.VelocityY);
            Assert.Greater(speed, player.Speed);
        }

        static IEnumerable<float[]> PhoneShapes()
        {
            // Width and height in pixels, then the left and right cutout insets.
            yield return new float[] { 1280f, 720f, 0f, 0f }; // 16:9
            yield return new float[] { 2160f, 1080f, 80f, 0f }; // 18:9
            yield return new float[] { 2340f, 1080f, 90f, 90f }; // 19.5:9
            yield return new float[] { 2400f, 1080f, 100f, 0f }; // 20:9
            yield return new float[] { 2992f, 1344f, 120f, 0f }; // Pixel 10 Pro XL
            yield return new float[] { 3200f, 1440f, 0f, 120f }; // 20:9, cutout on the other side
            yield return new float[] { 2520f, 1080f, 100f, 100f }; // 21:9
        }

        [Test]
        public void TheWholeCrewFitsBesideTheMenusOnEveryPhoneShape()
        {
            // Where the crew stand in the game: Vane, Rook, Lyra, Sable, Thorne, Morrow, and Cinder.
            var spots = new[] { new Vector2(-1.3f, 2f), new Vector2(0.9f, 0.5f), new Vector2(-2.7f, 0.6f), new Vector2(-5.5f, 0.9f), new Vector2(2.6f, -0.8f), new Vector2(-3f, -1.6f), new Vector2(-0.3f, -1.5f) };
            foreach (var phone in PhoneShapes())
            {
                var scale = 1080f / phone[1];
                var width = phone[0] * scale;
                var left = (phone[2] * scale + 40f) / width;
                var right = ((phone[0] - phone[3]) * scale - 28f - 760f - 40f) / width;
                var points = new List<HubPoint>();
                foreach (var spot in spots)
                {
                    points.Add(new HubPoint(spot.x - 0.7f, 0f, spot.y));
                    points.Add(new HubPoint(spot.x + 0.7f, 2.2f, spot.y));
                }
                var frame = HubFraming.Fit(points, phone[0] / phone[1], 46f, new HubClearArea(left, right, HubFraming.ClearBottom, HubFraming.ClearTop));
                Assert.IsTrue(frame.Fits, $"{phone[0]}x{phone[1]}");
                Assert.Greater(frame.CameraZ, -22f, "Close enough to still see their faces");

                // Independent check of the same camera with Unity's own projection maths.
                var view = Matrix4x4.LookAt(new Vector3(frame.CameraX, frame.CameraY, frame.CameraZ), new Vector3(frame.LookX, frame.LookY, frame.LookZ), Vector3.up);
                var eye = Quaternion.Inverse(view.rotation);
                var at = new Vector3(frame.CameraX, frame.CameraY, frame.CameraZ);
                var tall = Mathf.Tan(23f * Mathf.Deg2Rad);
                var wide = tall * phone[0] / phone[1];
                foreach (var point in points)
                {
                    var local = eye * (new Vector3(point.X, point.Y, point.Z) - at);
                    var screenX = (local.x / local.z / wide + 1f) * 0.5f;
                    Assert.GreaterOrEqual(screenX, left - 0.001f, $"{phone[0]}x{phone[1]}");
                    Assert.LessOrEqual(screenX, right + 0.001f, $"{phone[0]}x{phone[1]}");
                }
            }
        }

        [Test]
        public void HullDamageCarriesOverAndRepairsCostGold()
        {
            var save = Started();
            var max = Campaign.ShipHull(content, save, "rust_wing");
            Campaign.RecordDamage(content, save, "rust_wing", max - 100f);
            Assert.AreEqual(100f, Campaign.ShipDamage(save, "rust_wing"), 0.01f);
            Assert.AreEqual(max - 100f, Campaign.StartingHull(content, save, "rust_wing"), 0.01f);
            Assert.AreEqual(Mathf.CeilToInt(100f * content.skyRules.repairGoldPerHull), Campaign.RepairCost(content, save, "rust_wing"));

            save.gold = 1000;
            Assert.IsTrue(Campaign.Repair(content, save, "rust_wing"));
            Assert.AreEqual(0f, Campaign.ShipDamage(save, "rust_wing"));
            Assert.Less(save.gold, 1000);

            Campaign.RecordDamage(content, save, "rust_wing", 1f);
            Assert.AreEqual(max * content.skyRules.minimumStartHullShare, Campaign.StartingHull(content, save, "rust_wing"), 0.01f, "A broke player can still fly");
            save.gold = 1;
            var damage = Campaign.ShipDamage(save, "rust_wing");
            Campaign.Repair(content, save, "rust_wing");
            Assert.Less(Campaign.ShipDamage(save, "rust_wing"), damage, "Partial repairs for what gold there is");
            Assert.GreaterOrEqual(save.gold, 0);
        }

        [Test]
        public void DefendersProtectTheShipWhileThePartyBoards()
        {
            var alone = Started();
            Campaign.Recruit(alone, content, "rook", 1);
            var noDefenders = Campaign.BoardingExposure(content, alone);
            Assert.Greater(noDefenders, 0f);
            alone.party.Remove("rook");
            Assert.Less(Campaign.BoardingExposure(content, alone), noDefenders);
        }

        [Test]
        public void AssigningSplitsWhoFightsOnEachFront()
        {
            var save = Started();
            Campaign.Recruit(save, content, "rook", 1);
            Campaign.Recruit(save, content, "thorne", 1);
            save.party.Remove("thorne");
            var run = new CampaignRun(content, save, content.campaign.missions[0]);
            var defenders = run.BuildDeckParty(new PhaseDef { party = "defending" });
            Assert.AreEqual(1, defenders.Count);
            Assert.AreEqual("thorne", defenders[0].Id);
            var boarders = run.BuildDeckParty(new PhaseDef { party = "boarding" });
            Assert.IsFalse(boarders.Exists(c => c.Id == "thorne"));
            Assert.AreEqual(1, run.BuildDeckParty(new PhaseDef { party = "solo" }).Count);
        }

        [Test]
        public void NoMoreThanThreeFightAndTheRestDefendTheShip()
        {
            var save = Started();
            foreach (var id in new[] { "rook", "lyra", "sable", "thorne", "morrow" }) Campaign.Recruit(save, content, id, 1);
            Assert.AreEqual(BattleSetup.MaxPartySize, save.party.Count);
            Assert.AreEqual(3, BattleSetup.MaxPartySize);
            var run = new CampaignRun(content, save, content.campaign.missions[0]);
            var boarders = run.BuildDeckParty(new PhaseDef { party = "boarding" });
            var defenders = run.BuildDeckParty(new PhaseDef { party = "defending" });
            Assert.AreEqual(3, boarders.Count);
            Assert.AreEqual(3, defenders.Count, "Everyone left behind defends");
            foreach (var defender in defenders) Assert.IsFalse(boarders.Exists(b => b.Id == defender.Id));

            foreach (var mission in content.campaign.missions)
            {
                foreach (var phase in mission.phases)
                {
                    if (phase.kind != "deck") continue;
                    Assert.Contains(phase.party, new[] { "boarding", "defending", "solo" }, $"{mission.id}");
                }
            }
        }

        [Test]
        public void BoardersOnlyFightIfTheyLanded()
        {
            var save = Started();
            var phase = new PhaseDef { kind = "deck", scalesWithBoarders = true, crew = new[] { new DeckSpawn { id = "a", name = "A", classId = "armada_guard", level = 1 }, new DeckSpawn { id = "b", name = "B", classId = "armada_guard", level = 1 } } };
            var run = new CampaignRun(content, save, content.campaign.missions[0]);
            Assert.IsTrue(run.ShouldSkipDeck(phase));
            Assert.AreEqual(0, run.BuildDeckEnemies(phase).Count);
        }

        [Test]
        public void CrewWoundsCarryBetweenDeckPhasesWithPartialHealing()
        {
            var save = Started();
            var run = new CampaignRun(content, save, content.campaign.missions[0]);
            var first = run.BuildDeckParty(new PhaseDef { party = "boarding" });
            first[0].Hp = 40;
            run.FinishDeck(new PhaseDef(), first);
            var second = run.BuildDeckParty(new PhaseDef { party = "boarding" });
            Assert.AreEqual(40, second[0].Hp);
            var healed = run.BuildDeckParty(new PhaseDef { party = "boarding", healBefore = 0.5f });
            Assert.Greater(healed[0].Hp, 40);
        }

        [Test]
        public void FireShotWoundsTheCrewYouMeetOnDeck()
        {
            var save = Started();
            var mission = new MissionDef { id = "m", phases = new[] { new PhaseDef { kind = "sky", weather = "heist", objective = "capture", enemies = new[] { new SkySpawn { key = "t", shipId = "scrap_raider", name = "T", target = true, unsinkable = true, startHullShare = 0.2f } } } } };
            var run = new CampaignRun(content, save, mission);
            var encounter = new SkyEncounter(run.BuildSky(mission.phases[0], 3));
            encounter.Target.Ship.DeckDamage = encounter.Target.Ship.Loadout.Hull * 0.2f;
            encounter.Target.Ship.Hull = DogfightSim.BoardableHull;
            encounter.Target.Ship.X = encounter.Player.X;
            encounter.Target.Ship.Z = encounter.Player.Z + 30f;
            encounter.Step(new ShipCommand { Grapple = true });
            Assert.AreEqual(SkyOutcome.Boarded, encounter.Outcome);
            run.FinishSky(encounter);
            var deck = new PhaseDef { crew = new[] { new DeckSpawn { id = "g", name = "G", classId = "armada_guard", level = 1 } } };
            var crew = run.BuildDeckEnemies(deck);
            Assert.Less(crew[0].Hp, crew[0].Stats.hp);
        }

        [Test]
        public void CaptainsWhoFleeBecomeTougherNemeses()
        {
            var save = Started();
            Campaign.RecordNemesis(save, "gorse", "Gorse", "scrap_brig", 1);
            var entry = save.campaign.nemeses[0];
            Assert.AreEqual(1, entry.stacks);
            var first = Campaign.NemesisSpawn(content, entry, true);
            Campaign.RecordNemesis(save, "gorse", "Gorse", "scrap_brig", 1);
            Assert.AreEqual(1, save.campaign.nemeses.Count, "Same grudge, tougher captain");
            var second = Campaign.NemesisSpawn(content, entry, true);
            Assert.Greater(second.armorBonus, first.armorBonus);
            Assert.Greater(second.HullScale, first.HullScale);

            var bounty = Campaign.BountyMission(content, entry);
            Assert.Greater(bounty.gold, 0);
            Assert.AreEqual("bounty", bounty.kind);
            Assert.NotNull(Campaign.NemesisInSector(save, 1));
            Assert.IsNull(Campaign.NemesisInSector(save, 3), "They only haunt their own sector");
            Campaign.ClearNemesis(save, "gorse");
            Assert.AreEqual(0, save.campaign.nemeses.Count);
        }

        [Test]
        public void AMainEnemyRunsWhenBeatenButCantGetAway()
        {
            var save = Started();
            var mission = new MissionDef { id = "m", act = 1, phases = new[] { new PhaseDef { kind = "sky", weather = "heist", objective = "capture", enemies = new[] { new SkySpawn { key = "g", shipId = "scrap_raider", name = "Gorse", captain = "gorse", target = true, flees = true } } } } };
            var run = new CampaignRun(content, save, mission);
            var encounter = new SkyEncounter(run.BuildSky(mission.phases[0], 3));
            var main = encounter.Target.Ship;
            main.Hull = main.Loadout.Hull * 0.1f;
            for (var i = 0; i < 30 * 40 && encounter.Outcome == SkyOutcome.Ongoing; i++) encounter.Step(default);
            Assert.IsTrue(encounter.Target.Escaping, "They still make a run for it");
            Assert.IsTrue(main.Alive);
            Assert.AreNotEqual(SkyOutcome.TargetEscaped, encounter.Outcome);
            Assert.IsFalse(main.Departed);
            Assert.Less(main.Speed, main.Loadout.Speed * 0.75f, "And the player can catch them");
        }

        [Test]
        public void ConversationsBuildLoyaltyOnceAndEarnPerks()
        {
            var save = Started();
            Campaign.Recruit(save, content, "rook", 1);
            save.campaign.completed.Add("a1_rook");
            var talks = Campaign.AvailableTalks(save, content, "rook");
            Assert.AreEqual(1, talks.Count);
            var before = Encounter.PlayerLoadout(content, save, "rust_wing").BroadsideDamageScale;
            var perk = Campaign.CompleteTalk(save, content, talks[0]);
            Assert.NotNull(perk);
            Assert.AreEqual(1, Campaign.Loyalty(save, "rook"));
            Assert.Greater(Encounter.PlayerLoadout(content, save, "rust_wing").BroadsideDamageScale, before);
            Assert.IsNull(Campaign.CompleteTalk(save, content, talks[0]));
            Assert.AreEqual(1, Campaign.Loyalty(save, "rook"), "A conversation only counts once");
        }

        [Test]
        public void LoyaltyUnlocksRooksMissionAndIncendiaryGrapeshot()
        {
            var save = Started();
            var mission = content.campaign.Mission("s_rook");
            Assert.IsFalse(Campaign.IsAvailable(save, content, mission));
            Campaign.Recruit(save, content, "rook", 1);
            save.campaign.loyalty.Add(new LoyaltyProgress { crew = "rook", points = mission.loyaltyNeeded });
            Assert.IsTrue(Campaign.SideMissions(save, content).Contains(mission));
            Assert.IsFalse(Campaign.AvailableAmmo(content, save).Exists(a => a.id == "grapeshot"));
            Campaign.Complete(content, save, mission, false, null);
            Assert.IsTrue(Campaign.AvailableAmmo(content, save).Exists(a => a.id == "grapeshot"));
            Assert.IsFalse(Campaign.SideMissions(save, content).Contains(mission));
        }

        [Test]
        public void MorrowBringsRadarAndWeakPoints()
        {
            var save = Started();
            Assert.IsFalse(Campaign.HasMechanic(save, "radar"));
            Campaign.Recruit(save, content, "morrow", 1);
            Assert.IsTrue(Campaign.HasMechanic(save, "radar"));
            Assert.IsTrue(Campaign.HasMechanic(save, "weakpoints"));
        }

        [Test]
        public void ControlsStayHiddenUntilTaughtAndPulseUntilUsed()
        {
            var phase = System.Array.Find(content.campaign.missions[0].phases, p => p.kind == "sky");
            var save = SaveData.NewGame(content);
            var run = new CampaignRun(content, save, content.campaign.missions[0]);
            var encounter = new SkyEncounter(run.BuildSky(phase, 3));
            Assert.IsFalse(encounter.HasControl("grapple"), "No grapple until the betrayal");
            Assert.IsFalse(encounter.HasControl("broadside"), "Not yet");
            for (var i = 0; i < 30 * 5; i++) encounter.Step(default);
            Assert.IsTrue(encounter.HasControl("broadside"));
            Assert.IsTrue(encounter.IsTeaching("broadside"));
            encounter.MarkUsed("broadside");
            Assert.IsFalse(encounter.IsTeaching("broadside"));
            Assert.IsTrue(encounter.Player.AltitudeLocked, "Altitude waits for Act II");
        }

        [Test]
        public void ThePrologueTurnsOnTheBetrayal()
        {
            var save = SaveData.NewGame(content);
            var mission = content.campaign.missions[0];
            var run = new CampaignRun(content, save, mission);
            var sky = System.Array.FindAll(mission.phases, p => p.kind == "sky");
            Assert.AreEqual(2, sky.Length, "A fight, the mutiny, then the escape");

            var patrols = new SkyEncounter(run.BuildSky(sky[0], 3));
            Assert.AreEqual(2, patrols.Enemies.Count, "Two patrols first");
            foreach (var enemy in patrols.Enemies) enemy.Ship.TakeDamage(99999f);
            patrols.Step(default);
            Assert.AreEqual(SkyOutcome.Won, patrols.Outcome);
            Assert.IsFalse(patrols.BetrayalStarted, "The mutiny is a cutscene between the fights");

            var encounter = new SkyEncounter(run.BuildSky(sky[1], 3));
            Assert.IsFalse(encounter.HasControl("grapple"));
            encounter.Step(default);
            Assert.IsTrue(encounter.BetrayalStarted, "The Gale is burning from the start of the escape");
            Assert.AreEqual(SkyOutcome.Ongoing, encounter.Outcome, "The fight goes on");
            Assert.AreEqual("scout", encounter.Target.Spawn.key);
            Assert.IsTrue(encounter.HasControl("grapple"));
            var hull = encounter.Player.Hull;
            for (var i = 0; i < 30 * 200; i++) encounter.Step(default);
            Assert.Less(encounter.Player.Hull, hull, "The Gale burns");
            Assert.Greater(encounter.Player.Hull, 0f, "But the betrayal itself can't kill you");
        }

        [Test]
        public void AbandoningAMissionResumesFromItsCheckpoint()
        {
            var save = SaveData.NewGame(content);
            var mission = content.campaign.missions[0];
            var run = new CampaignRun(content, save, mission);
            run.Advance();
            Assert.AreEqual(mission.id, save.campaign.activeMission);
            Assert.AreEqual(1, save.campaign.activePhase);
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            var resumed = new CampaignRun(content, restored, mission, restored.campaign.activePhase);
            Assert.AreEqual(1, resumed.PhaseIndex);
        }

        [Test]
        public void OldSavesKeepTheirCaptainGoldAndUpgrades()
        {
            var old = new SaveData { version = 1, gold = 777, encountersWon = 4 };
            old.crew.Add(new CrewProgress { id = "captain", level = 6, xp = 12 });
            old.crew.Add(new CrewProgress { id = "mirren", level = 5 });
            old.party.Add("captain");
            old.party.Add("mirren");
            old.upgrades.Add(new UpgradeProgress { id = "hull", level = 2 });

            Assert.IsTrue(Campaign.Migrate(old, content));
            Assert.AreEqual(SaveData.CurrentVersion, old.version);
            Assert.AreEqual(777, old.gold);
            Assert.AreEqual(2, old.UpgradeLevel("hull"));
            Assert.AreEqual(1, old.crew.Count);
            Assert.AreEqual(Campaign.Protagonist, old.crew[0].id);
            Assert.AreEqual(6, old.crew[0].level);
            Assert.AreEqual(Campaign.Protagonist, old.party[0]);
            Assert.IsFalse(Campaign.Migrate(old, content), "A current save is left alone");
        }

        [Test]
        public void CampaignProgressSurvivesSaving()
        {
            var save = Started();
            Campaign.GrantSignature(save, "afterburner");
            Campaign.RecordNemesis(save, "gorse", "Gorse", "scrap_brig", 1);
            save.campaign.completed.Add("a1_leak");
            save.fleet[0].damage = 33f;
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.AreEqual("afterburner", restored.campaign.equippedSignature);
            Assert.AreEqual(1, restored.campaign.nemeses.Count);
            Assert.AreEqual("rust_wing", restored.activeShip);
            Assert.AreEqual(33f, restored.fleet[0].damage);
            Assert.IsTrue(restored.campaign.completed.Contains("a1_leak"));
        }

        [Test]
        public void ARollDodgesShotsAndHasACooldown()
        {
            var sim = Duel(out var a, out var b, 30f);
            sim.Step(new List<ShipCommand> { new ShipCommand { Evade = 1f }, default });
            Assert.Greater(a.ImmuneTimer, 0f);
            Assert.Greater(a.EvadeCooldown, 0f);
            var x = a.X;
            Run(sim, 0.2f);
            Assert.AreNotEqual(x, a.X, "The roll throws the ship sideways");
            var cooldown = a.EvadeCooldown;
            sim.Step(new List<ShipCommand> { new ShipCommand { Evade = -1f }, default });
            Assert.Less(a.EvadeCooldown, cooldown, "A second roll doesn't restart the timer");

            var shot = Duel(out var c, out var d, 40f);
            c.Heading = 0f;
            d.ImmuneTimer = 5f;
            var hull = d.Hull;
            Run(shot, 0.1f, new ShipCommand { FireTorpedo = true });
            Run(shot, 1f);
            Assert.AreEqual(hull, d.Hull, "Nothing lands during the roll");
        }

        [Test]
        public void ARollClearsTheBiggestShipOutOfTheLineOfFire()
        {
            var sim = Duel(out var a, out var b, 300f);
            a.Heading = 0f;
            a.Loadout.Speed = 0f;
            a.Speed = 0f;
            var startX = a.X;
            var startZ = a.Z;
            sim.Step(new List<ShipCommand> { new ShipCommand { Evade = 1f }, default });
            Run(sim, content.skyRules.evadeSeconds + 0.1f);
            var moved = Mathf.Sqrt((a.X - startX) * (a.X - startX) + (a.Z - startZ) * (a.Z - startZ));
            var largest = 0f;
            foreach (var ship in content.ships) largest = Mathf.Max(largest, ship.radius);
            Assert.GreaterOrEqual(moved, largest * 3f, "Even the Gale ends up well outside where the shots were aimed");
            Assert.GreaterOrEqual(content.skyRules.evadeImmunity, content.skyRules.evadeSeconds, "Nothing lands mid-roll");
        }

        [Test]
        public void ARollGoesWhicheverWayTheStickIsPushedFurthest()
        {
            void Check(float turn, float climb, float side, float lift)
            {
                DogfightSim.RollDirection(turn, climb, out var gotSide, out var gotLift);
                Assert.AreEqual(side, gotSide, $"side at {turn},{climb}");
                Assert.AreEqual(lift, gotLift, $"lift at {turn},{climb}");
            }
            Check(0f, 0.8f, 0f, 1f);
            Check(0f, -0.8f, 0f, -1f);
            Check(0.3f, 0.8f, 0f, 1f);
            Check(-0.3f, -0.8f, 0f, -1f);
            Check(-0.8f, 0f, -1f, 0f);
            Check(0.8f, 0f, 1f, 0f);
            Check(-0.8f, 0.3f, -1f, 0f);
            Check(0.8f, -0.3f, 1f, 0f);
            Check(0f, 0f, 1f, 0f);
        }

        [Test]
        public void ARollUpOrDownMovesAsFarAsARollSideways()
        {
            float[] Roll(float side, float lift, out ShipState ship)
            {
                var sim = Duel(out ship, out var other, 300f);
                ship.Heading = 0f;
                ship.Loadout.Speed = 0f;
                ship.Speed = 0f;
                ship.Y = 10f; // Mid-sky, so neither direction reaches a limit.
                other.Y = 10f;
                var before = new[] { ship.X, ship.Y, ship.Z };
                sim.Step(new List<ShipCommand> { new ShipCommand { Evade = side, EvadeLift = lift }, default });
                Assert.Greater(ship.ImmuneTimer, 0f, "Any roll gives the same immunity");
                Assert.Greater(ship.EvadeCooldown, 0f, "Any roll starts the same cooldown");
                Run(sim, content.skyRules.evadeSeconds + 0.1f);
                return new[] { ship.X - before[0], ship.Y - before[1], ship.Z - before[2] };
            }

            var up = Roll(0f, 1f, out _);
            var down = Roll(0f, -1f, out _);
            var right = Roll(1f, 0f, out _);
            Assert.Greater(up[1], 0f);
            Assert.Less(down[1], 0f);
            Assert.AreEqual(0f, up[0], 0.01f, "A roll up doesn't also go sideways");
            Assert.AreEqual(0f, right[1], 0.01f, "A roll sideways doesn't also climb");
            Assert.AreEqual(Mathf.Abs(right[0]), up[1], 0.01f);
            Assert.AreEqual(up[1], -down[1], 0.01f);
        }

        [Test]
        public void AShipThatCantClimbRollsSidewaysInstead()
        {
            var sim = Duel(out var a, out var b, 300f);
            a.AltitudeLocked = true;
            a.Y = 0f;
            var x = a.X;
            sim.Step(new List<ShipCommand> { new ShipCommand { EvadeLift = 1f }, default });
            Run(sim, 0.2f);
            Assert.AreEqual(0f, a.Y, 0.01f, "Altitude stays locked");
            Assert.AreNotEqual(x, a.X, "The button still rolls");

            var high = Duel(out var c, out var d, 300f);
            c.Y = DogfightSim.MaxAltitude - 1f;
            high.Step(new List<ShipCommand> { new ShipCommand { EvadeLift = 1f }, default });
            Run(high, content.skyRules.evadeSeconds + 0.1f);
            Assert.LessOrEqual(c.Y, DogfightSim.MaxAltitude, "A roll can't leave the sky");
        }

        [Test]
        public void EveryChapterAfterTheFirstHasASkyWhale()
        {
            foreach (var mission in content.campaign.missions)
            {
                foreach (var phase in mission.phases)
                {
                    if (phase.kind != "sky") continue;
                    if (mission.id == "p_mutiny") Assert.AreEqual(0, phase.whales, "The first level is for learning");
                    else if (mission.id == "s_currents") Assert.GreaterOrEqual(phase.whales, 3, "Lyra's Currents is the whale level");
                    else Assert.GreaterOrEqual(phase.whales, 1, mission.id);
                }
            }
        }

        [Test]
        public void ThreeWhalesComeFromAllSidesAndOnlyOneHuntsAtATime()
        {
            var def = JsonUtility.FromJson<HazardDef>(JsonUtility.ToJson(content.hazards));
            def.rockCount = def.debrisCount = def.jellyfishCount = def.fishCount = 0;
            def.whaleSenseRange = 100000f;
            var sim = Duel(out var a, out var b, 300f);
            var field = sim.AddHazards(def, -1, 5);
            Assert.AreEqual(0, field.Whales.Count);
            field.AddWhales(3);
            Assert.AreEqual(3, field.Whales.Count);
            Assert.Greater(field.Whales[1].Timer, field.Whales[0].Timer, "They wait their turns");
            Assert.Greater(field.Whales[2].Timer, field.Whales[1].Timer, "They wait their turns");

            foreach (var whale in field.Whales)
            {
                whale.Mode = WhaleMode.Roaming;
                whale.Timer = 0f;
                whale.Y = 500f; // Well out of reach, so nobody gets eaten during the count.
            }
            var seen = new HashSet<WhaleState>();
            for (var i = 0; i < 30 * 200 && seen.Count < 3; i++)
            {
                a.HuntedByWhale = true;
                sim.Step(new List<ShipCommand> { default, default });
                var hunting = 0;
                foreach (var whale in field.Whales)
                {
                    whale.Y = 500f;
                    if (whale.MouthOpen)
                    {
                        hunting++;
                        seen.Add(whale);
                    }
                }
                Assert.LessOrEqual(hunting, 1);
            }
            Assert.AreEqual(3, seen.Count, "Every whale gets its turn");
        }

        [Test]
        public void ARollSlipsOutOfAWhalesMouth()
        {
            var sim = Duel(out var a, out var b, 300f);
            var def = JsonUtility.FromJson<HazardDef>(JsonUtility.ToJson(content.hazards));
            def.rockCount = def.debrisCount = def.jellyfishCount = def.fishCount = 0;
            var field = sim.AddHazards(def, -1, 5);
            field.AddWhales(1);
            var whale = field.Whale;
            whale.Mode = WhaleMode.Lunging;
            whale.Timer = 2f;
            whale.X = a.X;
            whale.Y = a.Y;
            whale.Z = a.Z;
            a.ImmuneTimer = 1f;
            sim.Step(new List<ShipCommand> { default, default });
            Assert.IsFalse(a.Eaten);
        }

        [Test]
        public void AWhalesJawsOpenWiderTheCloserItGets()
        {
            var whale = new WhaleState { Mode = WhaleMode.Roaming };
            Assert.AreEqual(0f, whale.Gape(10f), "Shut while it isn't hunting, however close");
            whale.Mode = WhaleMode.Stalking;
            Assert.Greater(whale.Gape(500f), 0f, "Parted as soon as it hunts");
            Assert.Less(whale.Gape(500f), 0.5f);
            whale.Mode = WhaleMode.Lunging;
            Assert.Greater(whale.Gape(90f), whale.Gape(140f));
            Assert.AreEqual(1f, whale.Gape(20f), "Wide open by the time it reaches you");
        }

        [Test]
        public void JellyfishSwimAroundTheSkyAndStayInIt()
        {
            var sim = Duel(out var a, out var b, 300f);
            var def = JsonUtility.FromJson<HazardDef>(JsonUtility.ToJson(content.hazards));
            def.rockCount = def.debrisCount = def.fishCount = 0;
            def.jellyfishCount = 14;
            var field = sim.AddHazards(def, -1, 5);
            a.Y = 5000f; // Out of the way, so nothing pops them.
            b.Y = 5000f;
            var startX = new float[field.Hazards.Count];
            var startZ = new float[field.Hazards.Count];
            for (var i = 0; i < field.Hazards.Count; i++)
            {
                startX[i] = field.Hazards[i].X;
                startZ[i] = field.Hazards[i].Z;
            }
            var lowest = new float[field.Hazards.Count];
            var highest = new float[field.Hazards.Count];
            for (var i = 0; i < field.Hazards.Count; i++) { lowest[i] = float.MaxValue; highest[i] = float.MinValue; }

            for (var tick = 0; tick < 30 * 10; tick++)
            {
                field.Step(sim.Ships, new List<DogfightEvent>());
                for (var i = 0; i < field.Hazards.Count; i++)
                {
                    lowest[i] = Mathf.Min(lowest[i], field.Hazards[i].Y);
                    highest[i] = Mathf.Max(highest[i], field.Hazards[i].Y);
                }
            }
            for (var i = 0; i < field.Hazards.Count; i++)
            {
                var moved = Mathf.Sqrt(DogfightSim.DistanceSquared(field.Hazards[i].X, field.Hazards[i].Z, startX[i], startZ[i]));
                Assert.Greater(moved, 15f, "Even swimming in a curve, in ten seconds a jellyfish has gone somewhere");
                Assert.Greater(highest[i] - lowest[i], 18f, "And up and down");
            }

            for (var tick = 0; tick < 30 * 600; tick++) field.Step(sim.Ships, new List<DogfightEvent>());
            foreach (var jelly in field.Hazards)
            {
                Assert.Less(Mathf.Sqrt(jelly.X * jelly.X + jelly.Z * jelly.Z), DogfightSim.ArenaRadius, "Ten minutes on, still in the arena");
            }
        }

        [Test]
        public void ABadlyHurtShipPullsLeftAndRightAtRandomMoments()
        {
            var sim = Duel(out var a, out var b, 280f);
            a.DriftsWhenHurt = true;
            b.HuntedByWhale = false;

            // Healthy, or just above the line, it flies true.
            a.Hull = a.Loadout.Hull * (content.skyRules.damagedDriftHullShare + 0.01f);
            var heading = a.Heading;
            Run(sim, 10f);
            Assert.AreEqual(heading, a.Heading, 0.001f);

            a.Hull = a.Loadout.Hull * (content.skyRules.damagedDriftHullShare - 0.01f);
            var pulls = new List<float>();
            var gaps = new List<int>();
            var sinceLast = 0;
            for (var tick = 0; tick < 30 * 120; tick++)
            {
                a.X = 0f;
                a.Z = 0f;
                a.Heading = Mathf.PI * 0.5f;
                var before = a.DriftSide;
                sim.Step(new List<ShipCommand> { default, default });
                sinceLast++;
                if (a.DriftSide != 0f && before == 0f)
                {
                    pulls.Add(a.DriftSide);
                    gaps.Add(sinceLast);
                    sinceLast = 0;
                }
            }
            Assert.Greater(pulls.Count, 15, "It keeps happening");
            Assert.Contains(1f, pulls);
            Assert.Contains(-1f, pulls);
            gaps.RemoveAt(0);
            gaps.Sort();
            Assert.Greater(gaps[gaps.Count - 1], gaps[0], "Not on a steady beat");

            // Repairs fix it.
            a.Hull = a.Loadout.Hull;
            sim.Step(new List<ShipCommand> { default, default });
            Assert.AreEqual(0f, a.DriftSide);

            // Only the player's ship does this, and a full stick still beats the pull.
            Assert.IsFalse(b.DriftsWhenHurt);
            Assert.Less(content.skyRules.damagedDriftTurnShare, 1f);
        }

        [Test]
        public void FightsOpenWithTheEnemyOutOfRangeAndNotDeadAhead()
        {
            var range = content.Cannon(Encounter.PlayerTorpedoCannon).range;
            var lone = Sky("capture", new SkySpawn { key = "e", shipId = "scrap_raider", name = "E", target = true });
            var player = lone.Player;
            foreach (var count in new[] { 1, 2, 3, 7 })
            {
                var spawns = new SkySpawn[count];
                for (var i = 0; i < count; i++) spawns[i] = new SkySpawn { key = "e" + i, shipId = "scrap_raider", name = "E" + i };
                var encounter = Sky("sink_all", spawns);
                foreach (var enemy in encounter.Enemies)
                {
                    var distance = Mathf.Sqrt(DogfightSim.DistanceSquared(encounter.Player.X, encounter.Player.Z, enemy.Ship.X, enemy.Ship.Z));
                    Assert.Greater(distance, range, $"{count} enemies");
                }
            }
            var bearing = Mathf.Atan2(lone.Target.Ship.X - player.X, lone.Target.Ship.Z - player.Z) * Mathf.Rad2Deg;
            Assert.Greater(Mathf.Abs(bearing), content.Cannon(Encounter.PlayerTorpedoCannon).aimAssistDegrees, "A first shot has to be lined up");
        }

        [Test]
        public void AWindTunnelIsARoundTubeThatPushesShipsThroughIt()
        {
            var weather = new SkyWeather(content.Weather("maelstrom"), content.skyRules, 4);
            var sim = new DogfightSim(4, content.skyRules);
            var ship = sim.AddShip(0, Encounter.PlayerLoadout(content, Started(), "rust_wing"), 0f, 0f, 0f);
            sim.AddShip(1, Encounter.EnemyLoadout(content, "scrap_raider", 0), 300f, 300f, 0f);
            sim.SetWeather(weather);
            var scale = content.skyRules.windSpeedScale;
            var tube = new List<SkyZone>(weather.Zones).Find(z => z.Kind == SkyZoneKind.Wind);
            var forward = new Vector2(Mathf.Sin(tube.Heading), Mathf.Cos(tube.Heading));

            void Put(float along, float up, float side)
            {
                ship.X = tube.X + forward.x * along + forward.y * side;
                ship.Z = tube.Z + forward.y * along - forward.x * side;
                ship.Y = tube.Y + up;
                ship.Heading = tube.Heading;
            }
            Put(0f, 0f, 0f);
            Assert.AreEqual(scale, weather.WindScale(ship, scale), "Middle of the tube");
            Put(0f, tube.Radius * 0.7f, 0f);
            Assert.AreEqual(scale, weather.WindScale(ship, scale), "Near the top");
            Put(0f, tube.Radius + 1f, 0f);
            Assert.AreEqual(1f, weather.WindScale(ship, scale), "Just above it, out of the wind");
            Put(0f, -tube.Radius - 1f, 0f);
            Assert.AreEqual(1f, weather.WindScale(ship, scale), "Just below it");
            Put(0f, 0f, tube.Radius + 1f);
            Assert.AreEqual(1f, weather.WindScale(ship, scale), "Off to the side");
            Put(tube.Length * 0.5f + 1f, 0f, 0f);
            Assert.AreEqual(1f, weather.WindScale(ship, scale), "Past the end");

            // Inside, the wind carries the ship further along than it flies by itself.
            float Carried()
            {
                ship.Speed = 0f;
                var startX = ship.X;
                var startZ = ship.Z;
                for (var i = 0; i < 15; i++) sim.Step(new List<ShipCommand> { default, default });
                return (ship.X - startX) * forward.x + (ship.Z - startZ) * forward.y;
            }
            Put(-tube.Length * 0.25f, 0f, 0f);
            var inside = Carried();
            Put(0f, tube.Radius + 10f, 0f);
            var outside = Carried();
            Assert.Greater(inside, outside + 5f, "Felt the moment you're in it");

            var ground = new SkyWeather(content.Weather("maelstrom"), content.skyRules, 4, true);
            foreach (var zone in ground.Zones)
            {
                if (zone.Kind == SkyZoneKind.Wind) Assert.AreEqual(0f, zone.Y, "Reachable when ships can't climb");
            }
        }

        [Test]
        public void OldSavesKeepTheirTorpedoUpgrades()
        {
            var save = Started();
            save.version = 2;
            save.upgrades.Add(new UpgradeProgress { id = "bow", level = 3 });
            save.campaign.mechanics.Add("bow");
            Campaign.Migrate(save, content);
            Assert.AreEqual(3, save.UpgradeLevel("torpedo"));
            Assert.IsTrue(save.campaign.mechanics.Contains("torpedo"));
            Assert.IsFalse(save.campaign.mechanics.Contains("bow"));
        }

        [Test]
        public void EnemyTorpedoesGetFasterThroughTheGameWithThePlayersHoming()
        {
            var tube = content.Cannon(Encounter.PlayerTorpedoCannon);
            Assert.IsNull(Encounter.EnemyLoadout(content, "scrap_raider", 0).Torpedo, "None in the first level");
            Assert.IsNull(Encounter.EnemyLoadout(content, "assault_glider", 7).Torpedo, "Gliders are too small");
            var last = 0f;
            foreach (var tier in new[] { 1, 3, 5, 7 })
            {
                var torpedo = Encounter.EnemyLoadout(content, "scrap_raider", tier).Torpedo;
                Assert.Greater(torpedo.projectileSpeed, last, $"tier {tier}");
                last = torpedo.projectileSpeed;
                Assert.AreEqual(tube.homingTurn, torpedo.homingTurn, "Same homing as the player's");
                Assert.AreEqual(tube.aimAssistDegrees, torpedo.aimAssistDegrees);
                Assert.AreEqual(torpedo.projectileSpeed * 5f, torpedo.range, 0.01f, "Five seconds of flight");
            }
        }

        [Test]
        public void AnEnemyTorpedoHomesOnThePlayerAndARollDodgesIt()
        {
            ShipState Fire(DogfightSim sim, out ShipState enemy)
            {
                var player = sim.AddShip(0, Encounter.PlayerLoadout(content, Started(), "rust_wing"), 0f, 0f, 0f);
                enemy = sim.AddShip(1, Encounter.EnemyLoadout(content, "scrap_raider", 3), 0f, 120f, Mathf.PI);
                player.Loadout.Speed = 0f;
                enemy.Loadout.Speed = 0f;
                sim.Step(new List<ShipCommand> { default, new ShipCommand { FireTorpedo = true } });
                return player;
            }

            var first = new DogfightSim(5, content.skyRules);
            var hit = Fire(first, out var shooter);
            var torpedo = new List<ProjectileState>(first.Projectiles).Find(p => p.AmmoId == "torpedo");
            Assert.NotNull(torpedo, "It fires one");
            Assert.AreEqual(hit.Id, torpedo.HomingOn);
            Assert.AreEqual(content.Cannon("torpedo_steady").homingTurn, torpedo.HomingTurn);
            var hull = hit.Hull;
            Run(first, 3f);
            Assert.Less(hit.Hull, hull, "Standing still, it lands");

            var second = new DogfightSim(5, content.skyRules);
            var dodger = Fire(second, out _);
            var whole = dodger.Hull;
            var seconds = 120f / content.Cannon("torpedo_steady").projectileSpeed;
            Run(second, seconds - 0.8f);
            second.Step(new List<ShipCommand> { new ShipCommand { Evade = 1f }, default });
            Run(second, 3f);
            Assert.AreEqual(whole, dodger.Hull, "A roll at the last moment beats it");
        }

        [Test]
        public void TornadoesCarryAShipAllTheWayUpOrDown()
        {
            var def = content.Weather("maelstrom");
            Assert.Greater(def.tornadoUpCount, 0);
            Assert.Greater(def.tornadoDownCount, 0);
            float After(SkyZoneKind kind, out ShipState ship)
            {
                var weather = new SkyWeather(def, content.skyRules, 4);
                var sim = new DogfightSim(4, content.skyRules) { HoldOpen = true };
                ship = sim.AddShip(0, Encounter.PlayerLoadout(content, Started(), "rust_wing"), 0f, 0f, 0f);
                sim.AddShip(1, Encounter.EnemyLoadout(content, "scrap_raider", 0), 900f, 900f, 0f);
                sim.SetWeather(weather);
                var zone = new List<SkyZone>(weather.Zones).Find(z => z.Kind == kind);
                ship.X = zone.X;
                ship.Z = zone.Z;
                ship.Y = 0f;
                weather.Step(sim, new List<DogfightEvent>());
                Assert.AreNotEqual(0f, ship.TornadoDrag, "Caught");

                // Thrown clear of the funnel straight away, and it still goes all the way, even diving against it.
                ship.X += zone.Radius * 1.5f;
                ship.Heading = (float)Math.Atan2(ship.X - zone.X, ship.Z - zone.Z);
                var climb = kind == SkyZoneKind.TornadoUp ? -1f : 1f;
                for (var i = 0; i < 30 * 3 && ship.TornadoDrag != 0f; i++)
                {
                    sim.Step(new List<ShipCommand> { new ShipCommand { Climb = climb }, default });
                    if (i == 0) ship.Speed = 0f;
                }
                return ship.Y;
            }
            Assert.AreEqual(DogfightSim.MaxAltitude, After(SkyZoneKind.TornadoUp, out var up), 0.01f);
            Assert.AreEqual(0f, up.TornadoDrag, "Lets go at the top");
            Assert.AreEqual(DogfightSim.MinAltitude, After(SkyZoneKind.TornadoDown, out var down), 0.01f);
            Assert.AreEqual(0f, down.TornadoDrag, "Lets go at the bottom");

            var flat = new SkyWeather(def, content.skyRules, 4);
            var locked = new DogfightSim(4, content.skyRules);
            var grounded = locked.AddShip(0, Encounter.PlayerLoadout(content, Started(), "rust_wing"), 0f, 0f, 0f);
            locked.SetWeather(flat);
            grounded.AltitudeLocked = true;
            var funnel = new List<SkyZone>(flat.Zones).Find(z => z.Kind == SkyZoneKind.TornadoUp);
            grounded.X = funnel.X;
            grounded.Z = funnel.Z;
            flat.Step(locked, new List<DogfightEvent>());
            Assert.AreEqual(0f, grounded.TornadoDrag, "No effect in a fight where ships can't change height");
        }

        [Test]
        public void StoppingTheEnginesHoldsTheShipSoItCanLookAround()
        {
            var sim = Duel(out var a, out var b, 300f);
            a.Y = 0f;
            Run(sim, 2f);
            Assert.Greater(a.Speed, 0f);

            var stopped = new ShipCommand { EnginesStopped = true, Throttle = 1f };
            for (var i = 0; i < 60; i++) sim.Step(new List<ShipCommand> { stopped, default });
            Assert.AreEqual(0f, a.Speed, "Stops, whatever the throttle");
            var x = a.X;
            var z = a.Z;
            var y = a.Y;
            var heading = a.Heading;

            var looking = new ShipCommand { EnginesStopped = true, Turn = 1f, Climb = 1f };
            for (var i = 0; i < 30; i++) sim.Step(new List<ShipCommand> { looking, default });
            Assert.AreEqual(x, a.X, 0.01f, "Holds position");
            Assert.AreEqual(z, a.Z, 0.01f);
            Assert.AreEqual(y, a.Y, 0.01f, "The stick looks up instead of climbing");
            Assert.AreNotEqual(heading, a.Heading, "Looks right");
            Assert.Greater(a.LookPitch, 0f, "Looks up");
            for (var i = 0; i < 30 * 5; i++) sim.Step(new List<ShipCommand> { looking, default });
            Assert.LessOrEqual(a.LookPitch, DogfightSim.MaxLookPitch, "Only so far");

            sim.Step(new List<ShipCommand> { new ShipCommand { EnginesStopped = true, Climb = -1f }, default });
            Assert.Less(a.LookPitch, DogfightSim.MaxLookPitch, "And back down");

            // A shot with no lock goes where it's looking.
            a.TorpedoCooldown = 0f;
            a.Heading = b.Heading;
            sim.Step(new List<ShipCommand> { new ShipCommand { EnginesStopped = true, FireTorpedo = true }, default });
            var shot = new List<ProjectileState>(sim.Projectiles).Find(p => p.OwnerId == a.Id);
            Assert.Greater(shot.VelocityY, 0f, "Fired upward");

            Run(sim, 3f, new ShipCommand { Throttle = 1f });
            for (var i = 0; i < 30; i++) sim.Step(new List<ShipCommand> { new ShipCommand { Throttle = 1f }, default });
            Assert.Greater(a.Speed, 0f, "Starting the engines flies on");
            Assert.AreEqual(0f, a.LookPitch, 0.01f, "And the view levels out");
        }

        [Test]
        public void TunnelsAndTornadoesAreSpreadEvenlyAroundTheSky()
        {
            // Points along a zone: the length of a tunnel, or the one spot of a tornado.
            static List<Vector2> Points(SkyZone zone)
            {
                var points = new List<Vector2>();
                var steps = zone.Length > 0f ? 24 : 0;
                for (var i = 0; i <= steps; i++)
                {
                    var along = steps == 0 ? 0f : (i / (float)steps - 0.5f) * zone.Length;
                    points.Add(new Vector2(zone.X + Mathf.Sin(zone.Heading) * along, zone.Z + Mathf.Cos(zone.Heading) * along));
                }
                return points;
            }

            foreach (var def in content.weathers)
            {
                var total = def.windCount + def.tornadoUpCount + def.tornadoDownCount;
                Assert.LessOrEqual(total, 4, def.id);
                if (total < 2) continue;
                for (var seed = 0; seed < 50; seed++)
                {
                    var big = new List<SkyZone>(new SkyWeather(def, content.skyRules, seed).Zones).FindAll(z => z.Kind == SkyZoneKind.Wind || z.Kind == SkyZoneKind.TornadoUp || z.Kind == SkyZoneKind.TornadoDown);
                    Assert.AreEqual(total, big.Count, def.id);

                    var angles = new List<float>();
                    foreach (var zone in big) angles.Add(Mathf.Repeat(Mathf.Atan2(zone.X, zone.Z), Mathf.PI * 2f));
                    angles.Sort();
                    for (var i = 0; i < angles.Count; i++)
                    {
                        var gap = Mathf.Repeat(angles[(i + 1) % angles.Count] - angles[i], Mathf.PI * 2f);
                        Assert.Greater(gap, Mathf.PI * 2f / total * 0.8f, $"{def.id} seed {seed}: evenly around the sky");
                    }

                    for (var i = 0; i < big.Count; i++)
                    {
                        for (var j = i + 1; j < big.Count; j++)
                        {
                            var closest = float.MaxValue;
                            foreach (var a in Points(big[i]))
                            {
                                foreach (var b in Points(big[j])) closest = Mathf.Min(closest, (a - b).magnitude);
                            }
                            Assert.Greater(closest - big[i].Radius - big[j].Radius, 40f, $"{def.id} seed {seed}: open sky between them");
                        }
                    }
                }
            }
        }

        [Test]
        public void CripplingComesFromTheAmmoYouLoad()
        {
            var round = FireOneVolley("round");
            Assert.AreEqual(round.MaxSubsystem, round.Engines, "Round only hurts the hull");
            Assert.AreEqual(round.MaxSubsystem, round.Guns);
            Assert.AreEqual(round.MaxSubsystem, round.Envelope);

            var chain = FireOneVolley("chain");
            Assert.Less(chain.Engines, chain.MaxSubsystem, "Chain wrecks engines");
            Assert.AreEqual(chain.MaxSubsystem, chain.Guns);

            var flak = FireOneVolley("flak");
            Assert.Less(flak.Envelope, flak.MaxSubsystem, "Flak shreds balloons");
            Assert.AreEqual(flak.MaxSubsystem, flak.Engines);

            var mortar = FireOneVolley("mortar");
            Assert.Less(mortar.Guns, mortar.MaxSubsystem, "Mortars wreck guns");
            Assert.Less(mortar.Engines, mortar.MaxSubsystem, "And engines");
        }

        [Test]
        public void EnemyVolleysAreAnnouncedBeforeTheyFire()
        {
            var encounter = Sky("sink_all", new SkySpawn { key = "e", shipId = "scrap_raider", name = "E", accuracy = 1f });
            var warned = -1f;
            var fired = -1f;
            encounter.Player.HullFloor = 1f;
            for (var i = 0; i < 30 * 60 && fired < 0f; i++)
            {
                encounter.Step(default);
                foreach (var e in encounter.Sim.Events)
                {
                    if (e.Kind == DogfightEventKind.VolleyWarning && warned < 0f) warned = encounter.Time;
                    if (e.Kind == DogfightEventKind.Fired && e.Heavy && e.ShipId != encounter.Player.Id) fired = encounter.Time;
                }
            }
            Assert.Greater(warned, 0f);
            Assert.GreaterOrEqual(fired - warned, content.skyRules.volleyWarning * 0.9f, "There's time to react");
        }

        [Test]
        public void ShotsFromBehindRake()
        {
            bool Raked(bool stern)
            {
                var sim = Duel(out var a, out var b, 0f);
                a.Heading = 0f;
                a.Loadout.Speed = 0f;
                a.Speed = 0f;
                b.X = 30f;
                b.Z = 0f;
                b.Heading = stern ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f;
                b.Loadout.Speed = 0f;
                b.Speed = 0f;
                var commands = new List<ShipCommand> { new ShipCommand { FireBroadside = true }, default };
                for (var i = 0; i < 60; i++)
                {
                    sim.Step(commands);
                    commands[0] = default;
                    foreach (var e in sim.Events)
                    {
                        if (e.Kind == DogfightEventKind.Raked) return true;
                    }
                }
                return false;
            }
            Assert.IsTrue(Raked(true), "Hitting the stern rakes");
            Assert.IsFalse(Raked(false), "Hitting the nose doesn't");
            Assert.Greater(content.skyRules.rakeScale, 1f);
        }

        [Test]
        public void ALostEnvelopeSinksAShip()
        {
            var sim = Duel(out var a, out var b);
            b.Envelope = 0f;
            b.Y = 20f;
            Run(sim, 1f);
            Assert.Less(b.Y, 20f);
            var aimed = Duel(out var c, out var d, 30f);
            c.Ammo = content.Ammo("flak");
            Run(aimed, 0.1f, new ShipCommand { FireBroadside = true });
            Run(aimed, 1.5f);
            Assert.Less(d.Envelope, d.MaxSubsystem);
        }

        [Test]
        public void MissilesHomeInAndRunOutOfFuel()
        {
            var sim = Duel(out var a, out var b, 100f);
            b.AltitudeLocked = true;
            var hull = a.Hull;
            sim.LaunchMissile(b, a);
            Run(sim, 4f, new ShipCommand { Throttle = 0.4f });
            Assert.Less(a.Hull, hull, "It steers onto a ship that doesn't dodge");

            var far = Duel(out var c, out var d, 2000f);
            far.LaunchMissile(d, c);
            Assert.AreEqual(1, far.Projectiles.Count);
            Run(far, content.skyRules.missileSeconds + 0.5f);
            Assert.AreEqual(0, far.Projectiles.Count);
        }

        [Test]
        public void MinesBlowUpWhenTouchedAndHurtEveryoneNearby()
        {
            var sim = Duel(out var a, out var b, 15f);
            var field = sim.AddHazards(new HazardDef { whaleMinTier = 99 }, 0, 1);
            field.MineBlastRadius = content.skyRules.mineBlastRadius;
            field.MineBlastDamage = content.skyRules.mineBlastDamage;
            field.AddMines(1, 10f, 30f, new List<ShipState>());
            var mine = field.Hazards[0];
            mine.X = a.X;
            mine.Y = a.Y;
            mine.BaseY = a.Y;
            mine.Z = a.Z;
            var hullA = a.Hull;
            var hullB = b.Hull;
            Run(sim, 0.1f);
            Assert.IsFalse(mine.Alive);
            Assert.Less(a.Hull, hullA);
            Assert.Less(b.Hull, hullB, "Enemies caught in the blast suffer too");
        }

        [Test]
        public void ToxicCloudsEatHulls()
        {
            var def = content.Weather("scrapwinds");
            Assert.Greater(def.toxicCount, 0);
            var sim = new DogfightSim(4, content.skyRules);
            var save = Started();
            var ship = sim.AddShip(0, Encounter.PlayerLoadout(content, save, "rust_wing"), 0f, 0f, 0f);
            var weather = new SkyWeather(def, content.skyRules, 4);
            sim.SetWeather(weather);
            var zone = new List<SkyZone>(weather.Zones).Find(z => z.Kind == SkyZoneKind.Toxic);
            ship.X = zone.X;
            ship.Z = zone.Z;
            ship.Y = zone.Y;
            var hull = ship.Hull;
            var events = new List<DogfightEvent>();
            for (var i = 0; i < 30; i++) weather.Step(sim, events);
            Assert.Less(ship.Hull, hull);
        }

        [Test]
        public void CollectObjectivesEndWhenEverythingIsGathered()
        {
            var save = Started();
            var mission = new MissionDef { id = "m", phases = new[] { new PhaseDef { kind = "sky", weather = "heist", objective = "collect", pickups = 3, enemies = new SkySpawn[0] } } };
            var run = new CampaignRun(content, save, mission);
            var encounter = new SkyEncounter(run.BuildSky(mission.phases[0], 3));
            Assert.AreEqual(3, encounter.Pickups.Count);
            foreach (var pickup in encounter.Pickups)
            {
                Assert.AreEqual(SkyOutcome.Ongoing, encounter.Outcome);
                encounter.Player.X = pickup.X;
                encounter.Player.Y = pickup.Y;
                encounter.Player.Z = pickup.Z;
                encounter.Step(default);
            }
            Assert.AreEqual(3, encounter.PickupsTaken);
            Assert.AreEqual(SkyOutcome.Won, encounter.Outcome);
        }

        SkyEncounter Escort(float seconds, params SkySpawn[] extra)
        {
            var save = Started();
            var spawns = new List<SkySpawn> { new SkySpawn { key = "friend", shipId = "scrap_raider", name = "Friend", ally = true, target = true, accuracy = 5f } };
            spawns.AddRange(extra);
            var mission = new MissionDef { id = "m", phases = new[] { new PhaseDef { kind = "sky", weather = "heist", objective = "escort", seconds = seconds, enemies = spawns.ToArray() } } };
            var run = new CampaignRun(content, save, mission);
            return new SkyEncounter(run.BuildSky(mission.phases[0], 3));
        }

        [Test]
        public void AlliesFightOnYourSideAndLosingYourEscortFails()
        {
            var encounter = Escort(60f, new SkySpawn { key = "foe", shipId = "scrap_raider", name = "Foe" });
            var friend = encounter.Enemies[0].Ship;
            var foe = encounter.Enemies[1].Ship;
            Assert.AreEqual(encounter.Player.Team, friend.Team);
            Assert.AreNotEqual(foe.Team, friend.Team);
            friend.TakeDamage(99999f);
            encounter.Step(default);
            Assert.AreEqual(SkyOutcome.Lost, encounter.Outcome);

            var saved = Escort(60f, new SkySpawn { key = "foe", shipId = "scrap_raider", name = "Foe" });
            saved.Enemies[1].Ship.TakeDamage(99999f);
            saved.Step(default);
            Assert.AreEqual(SkyOutcome.Won, saved.Outcome, "Clearing the sky wins early");

            var timed = Escort(1f, new SkySpawn { key = "foe", shipId = "scrap_raider", name = "Foe" });
            timed.Player.HullFloor = 1f;
            for (var i = 0; i < 40 && timed.Outcome == SkyOutcome.Ongoing; i++) timed.Step(default);
            Assert.AreEqual(SkyOutcome.Won, timed.Outcome, "Or holding on long enough does");
        }

        [Test]
        public void ReinforcementsArriveWhenAWaveIsBeaten()
        {
            var encounter = Sky("sink_all", new SkySpawn { key = "a", shipId = "scrap_raider", name = "A" }, new SkySpawn { key = "b", shipId = "scrap_raider", name = "B", wave = 1 });
            Assert.AreEqual(1, encounter.Enemies.Count, "The second wave waits");
            encounter.Enemies[0].Ship.TakeDamage(99999f);
            encounter.Step(default);
            Assert.AreEqual(2, encounter.Enemies.Count);
            Assert.AreEqual(SkyOutcome.Ongoing, encounter.Outcome);
            encounter.Enemies[1].Ship.TakeDamage(99999f);
            encounter.Step(default);
            Assert.AreEqual(SkyOutcome.Won, encounter.Outcome);
        }

        PartyBattle Deck(out Combatant hero, out Combatant foe, int level = 5)
        {
            hero = Combatant.Create("vane", "Vane", content.Class("buccaneer"), content.Crew("vane").baseStats, level, content, true, 0);
            hero.AddSignature(content.Crew("vane").signature, content);
            foe = Combatant.Create("foe", "Foe", content.Class("brigand"), content.Class("brigand").baseStats, level, content, false, 0);
            return new PartyBattle(new List<Combatant> { hero }, new List<Combatant> { foe }, 3, content.timing);
        }

        [Test]
        public void EnemiesAnnounceWhatTheyWillDo()
        {
            var battle = Deck(out var hero, out var foe);
            Assert.IsTrue(battle.TryGetIntent(foe, out var intent));
            Assert.AreEqual(hero, intent.Target);
            var chosen = battle.ChooseEnemyAction(foe);
            Assert.AreEqual(intent.Kind, chosen.Kind);
            Assert.AreEqual(intent.Skill, chosen.Skill);
            Assert.AreEqual(intent.Target, chosen.Target, "It does what it said");
        }

        [Test]
        public void BurnsTickStunsSkipAndExposureHurts()
        {
            var burning = Deck(out var hero, out var foe);
            foe.BurnTurns = 2;
            var hp = foe.Hp;
            WaitForTurn(burning, foe);
            for (var i = 0; i < 6 && foe.BurnTurns == 2; i++) burning.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            Assert.Less(foe.Hp, hp, "Burn damage lands at the start of a turn");

            var stunned = Deck(out var hero2, out var foe2);
            foe2.StunTurns = 1;
            WaitForTurn(stunned, hero2);
            stunned.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            Assert.AreEqual(hero2, stunned.Current, "A stunned enemy loses its turn");
            Assert.AreEqual(0, foe2.StunTurns);

            var plain = Deck(out var hero3, out var foe3);
            var exposed = Deck(out var hero4, out var foe4);
            foe4.ExposedTurns = 5;
            WaitForTurn(plain, hero3);
            WaitForTurn(exposed, hero4);
            foe3.Hp = foe4.Hp = 9999;
            foe3.Stats.hp = foe4.Stats.hp = 9999f;
            var normal = plain.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = foe3, Timing = TimingGrade.Good })[0].Amount;
            var harder = exposed.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = foe4, Timing = TimingGrade.Good })[0].Amount;
            Assert.Greater(harder, normal);
        }

        [Test]
        public void GuardRestoresMpAndAPerfectBlockCounters()
        {
            var battle = Deck(out var hero, out var foe);
            WaitForTurn(battle, hero);
            hero.Mp = 0;
            battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            Assert.Greater(hero.Mp, 0);
            WaitForTurn(battle, foe);
            var hp = foe.Hp;
            var results = battle.Submit(new BattleAction { Kind = BattleActionKind.Attack, Target = hero, Timing = TimingGrade.Perfect });
            Assert.IsTrue(results.Exists(r => r.Kind == BattleEventKind.Counter));
            Assert.Less(foe.Hp, hp);
        }

        [Test]
        public void ItemsHealAndDamageAndCanBeBought()
        {
            var battle = Deck(out var hero, out var foe);
            WaitForTurn(battle, hero);
            hero.Hp = 10;
            battle.Submit(new BattleAction { Kind = BattleActionKind.Item, Item = content.Item("rum"), Target = hero });
            Assert.Greater(hero.Hp, 10);
            WaitForTurn(battle, hero);
            var hp = foe.Hp;
            battle.Submit(new BattleAction { Kind = BattleActionKind.Item, Item = content.Item("charge") });
            Assert.Less(foe.Hp, hp);

            var save = Started();
            save.gold = content.Item("rum").cost;
            Assert.IsTrue(save.BuyItem(content.Item("rum")));
            Assert.AreEqual(1, save.ItemCount("rum"));
            Assert.AreEqual(0, save.gold);
            Assert.IsFalse(save.BuyItem(content.Item("rum")), "Not without the gold");
        }

        [Test]
        public void BossesRageWhenHurt()
        {
            var battle = Deck(out var hero, out var foe);
            foe.EnrageAt = 0.5f;
            var attack = foe.Stats.attack;
            WaitForTurn(battle, hero);
            foe.Hp = (int)(foe.Stats.hp * 0.6f);
            var results = battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            Assert.IsFalse(foe.Enraged);
            WaitForTurn(battle, hero);
            foe.Hp = (int)(foe.Stats.hp * 0.3f);
            results = battle.Submit(new BattleAction { Kind = BattleActionKind.Guard });
            Assert.IsTrue(foe.Enraged);
            Assert.IsTrue(results.Exists(r => r.Kind == BattleEventKind.Enraged));
            Assert.Greater(foe.Stats.attack, attack);
        }

        [Test]
        public void RooksHoundBreathesFireOnEveryEnemy()
        {
            Assert.AreEqual("SK_Cinder", content.Look("rook").companion);
            var rook = Combatant.Create("rook", "Rook", content.Class("gunner"), content.Crew("rook").baseStats, 2, content, true, 0);
            rook.AddSignature(content.Crew("rook").signature, content);
            var skill = rook.Skills.Find(s => s.id == "cinder_flame");
            Assert.NotNull(skill, "Rook has it as soon as he joins");
            Assert.IsTrue(skill.companion);
            Assert.AreEqual(SkillTarget.AllEnemies, skill.Target);
            Assert.AreEqual("burn", skill.status);
        }

        [Test]
        public void ThorneIsATankWhoHealsAndHitsSoftly()
        {
            var thorne = content.Crew("thorne");
            Assert.AreEqual("sorcerer", thorne.classId);
            foreach (var other in content.crew)
            {
                if (other.id == "thorne") continue;
                Assert.Greater(thorne.baseStats.hp, other.baseStats.hp, $"More health than {other.id}");
                Assert.Greater(thorne.baseStats.defense, other.baseStats.defense, $"More defense than {other.id}");
                Assert.Less(thorne.baseStats.attack, other.baseStats.attack, $"Less attack than {other.id}");
            }
            var combatant = Combatant.Create("thorne", "Thorne", content.Class("sorcerer"), thorne.baseStats, 5, content, true, 0);
            combatant.AddSignature(thorne.signature, content);
            Assert.IsTrue(combatant.Skills.Exists(s => s.revive), "Can bring back the fallen");
            Assert.IsTrue(combatant.Skills.Exists(s => s.kind == "Heal" && s.target == "Ally" && !s.revive), "Can heal one ally");
            Assert.IsTrue(combatant.Skills.Exists(s => s.target == "AllAllies"), "Can heal everyone");
        }

        [Test]
        public void ThorneCanBringBackTheFallen()
        {
            var thorne = Combatant.Create("thorne", "Thorne", content.Class("sorcerer"), content.Crew("thorne").baseStats, 5, content, true, 0);
            thorne.AddSignature(content.Crew("thorne").signature, content);
            var ally = Combatant.Create("vane", "Vane", content.Class("buccaneer"), content.Crew("vane").baseStats, 5, content, true, 1);
            var foe = Combatant.Create("foe", "Foe", content.Class("brigand"), content.Class("brigand").baseStats, 1, content, false, 0);
            var battle = new PartyBattle(new List<Combatant> { thorne, ally }, new List<Combatant> { foe }, 3, content.timing);
            ally.Hp = 0;
            WaitForTurn(battle, thorne);
            var surgery = thorne.Skills.Find(s => s.revive);
            Assert.NotNull(surgery);
            var results = battle.Submit(new BattleAction { Kind = BattleActionKind.Skill, Skill = surgery, Target = ally });
            Assert.IsTrue(results.Exists(r => r.Kind == BattleEventKind.Revived));
            Assert.Greater(ally.Hp, 0);
        }

        [Test]
        public void EveryCrewAndEnemyLookExists()
        {
            foreach (var member in content.crew) Assert.NotNull(content.Look(member.look), member.id);
            foreach (var speaker in content.campaign.speakers)
            {
                if (!string.IsNullOrEmpty(speaker.look)) Assert.NotNull(content.Look(speaker.look), speaker.id);
            }
            foreach (var mission in content.campaign.missions)
            {
                foreach (var phase in mission.phases)
                {
                    if (phase.kind != "deck") continue;
                    foreach (var spawn in phase.crew) Assert.NotNull(content.Look(spawn.look), $"{mission.id}: {spawn.id}");
                    StringAssert.StartsWith("SM_Env_", phase.arena, mission.id);
                }
            }
            foreach (var ship in content.ships) StringAssert.StartsWith("SM_Ship_", ship.model, ship.id);
        }

        [Test]
        public void ReplayingAMissionPaysHalfAndGrantsNothingNew()
        {
            var save = Started();
            var mission = content.campaign.Mission("a1_rook");
            Campaign.Complete(content, save, mission, false, null);
            Assert.IsNotNull(save.crew.Find(c => c.id == "rook"));
            var gold = save.gold;
            save.crew.RemoveAll(c => c.id == "rook");
            Campaign.Complete(content, save, mission, false, null);
            Assert.AreEqual(gold + mission.gold / 2, save.gold);
            Assert.IsNull(save.crew.Find(c => c.id == "rook"), "Rewards only come once");
        }

        [Test]
        public void CiphersCountForTheHangar()
        {
            var save = Started();
            Assert.AreEqual(3, Campaign.CiphersTotal(content));
            Assert.AreEqual(0, Campaign.CiphersFound(save, content));
            save.campaign.completed.Add("a2_ashgrave");
            Assert.AreEqual(1, Campaign.CiphersFound(save, content));
        }

        [Test]
        public void ThePrologueIntroducesTheWorldAndTheCrew()
        {
            var cards = new HashSet<string>();
            var shots = 0;
            foreach (var phase in content.campaign.Mission("p_mutiny").phases)
            {
                if (phase.shots == null) continue;
                foreach (var shot in phase.shots)
                {
                    shots++;
                    Assert.AreEqual(3, shot.cam.Length, "A shot needs a camera");
                    Assert.AreEqual(3, shot.look.Length, "A shot needs somewhere to look");
                    if (!string.IsNullOrEmpty(shot.cardName)) cards.Add(shot.cardName);
                    if (!string.IsNullOrEmpty(shot.speaker)) Assert.NotNull(content.campaign.Speaker(shot.speaker), shot.speaker);
                    if (shot.ships != null) foreach (var ship in shot.ships) Assert.NotNull(content.Ship(ship.ship), ship.ship);
                    if (shot.actors != null) foreach (var actor in shot.actors) Assert.NotNull(content.Look(actor.look), actor.look);
                    if (shot.props != null) foreach (var prop in shot.props) Assert.NotNull(Resources.Load<GameObject>($"Art/{prop.asset}_HQ"), prop.asset);
                    if (!string.IsNullOrEmpty(shot.arena)) Assert.NotNull(Resources.Load<GameObject>($"Art/{shot.arena}_HQ"), shot.arena);
                    if (!string.IsNullOrEmpty(shot.music)) CollectionAssert.Contains(GameMusic.Songs, shot.music);
                }
            }
            Assert.GreaterOrEqual(shots, 15);
            foreach (var name in new[] { "VANE", "CAPTAIN CASSIAN", "MIRREN", "ODESSA", "TOBIN", "MORROW" }) CollectionAssert.Contains(cards, name);
        }

        [Test]
        public void EverySongTheGameAsksForExists()
        {
            foreach (var song in GameMusic.Songs)
            {
                Assert.NotNull(Resources.Load<AudioClip>($"Music/{song}_base_loop"), song);
            }
            Assert.NotNull(Resources.Load<AudioClip>("Music/" + GameMusic.Victory));
            Assert.NotNull(Resources.Load<AudioClip>("Music/" + GameMusic.Defeat));
            foreach (var mission in content.campaign.missions)
            {
                foreach (var phase in mission.phases)
                {
                    if (!string.IsNullOrEmpty(phase.music)) CollectionAssert.Contains(GameMusic.Songs, phase.music, mission.id);
                }
            }
            foreach (GameState state in System.Enum.GetValues(typeof(GameState)))
            {
                var song = GameMusic.For(state, null);
                if (song != null) CollectionAssert.Contains(GameMusic.Songs, song);
            }
        }

        [Test]
        public void EverySoundEffectClipExists()
        {
            foreach (Sfx sound in System.Enum.GetValues(typeof(Sfx)))
            {
                if (sound >= AudioBank.FirstClipSound) Assert.NotNull(Resources.Load<AudioClip>("Sfx/" + sound), sound.ToString());
            }
            Assert.NotNull(Resources.Load<AudioClip>("Sfx/" + AudioBank.EngineLoopClip));
        }

        [Test]
        public void AnAutomatedCaptainCanFinishTheWholeStory()
        {
            var save = SaveData.NewGame(content);
            var seed = 4000;
            foreach (var mission in content.campaign.missions)
            {
                if (!mission.IsStory) continue;
                var run = new CampaignRun(content, save, mission);
                var retries = 0;
                while (!run.Finished)
                {
                    var phase = run.Phase;
                    seed += 13;
                    var won = true;
                    switch (phase.kind)
                    {
                        case "sky": won = PlaySky(run, phase, seed, save); break;
                        case "deck": won = run.ShouldSkipDeck(phase) || PlayDeck(run, phase, seed); break;
                        case "choice": Campaign.ApplyChoice(content, save, phase, DavyJonesChoice.SeizeTheHelm); break;
                        case "assign":
                            if (save.crew.Count > 1 && save.crew.Count <= save.party.Count) save.party.RemoveAt(save.party.Count - 1);
                            break;
                    }
                    if (won)
                    {
                        run.Advance();
                    }
                    else
                    {
                        retries++;
                        Assert.Less(retries, 15, $"Stuck on {mission.id} phase {run.PhaseIndex}");
                        Campaign.Repair(content, save, save.activeShip);
                    }
                }
                run.CompleteMission();
                Campaign.Repair(content, save, save.activeShip);
                foreach (var upgrade in content.upgrades)
                {
                    if (save.gold > 60) Shop.Buy(save, upgrade);
                }
            }
            Assert.IsNull(Campaign.NextStoryMission(save, content), "The story ran to its end");
            Assert.AreEqual("sovereign_gale", save.activeShip);
            Assert.IsNotNull(save.crew.Find(c => c.id == "thorne"));
            CollectionAssert.Contains(save.campaign.signatures, "thunder_volley");
        }

        bool PlaySky(CampaignRun run, PhaseDef phase, int seed, SaveData save)
        {
            var encounter = new SkyEncounter(run.BuildSky(phase, seed));
            var bot = new AutoCaptain(seed + 1, 6f);
            var ammo = Campaign.AvailableAmmo(content, save);
            for (var i = 0; i < 30 * 300 && encounter.Outcome == SkyOutcome.Ongoing; i++)
            {
                bot.ChooseAmmo(encounter, ammo);
                encounter.Step(bot.Think(encounter));
            }
            Assert.AreNotEqual(SkyOutcome.Ongoing, encounter.Outcome, $"{run.Mission.id} never ended: player {encounter.Player.Hull:F0}/{encounter.Player.Loadout.Hull:F0}, target {encounter.Target?.Ship.Hull:F0}/{encounter.Target?.Ship.Loadout.Hull:F0}, seed {seed}");
            return run.FinishSky(encounter);
        }

        bool PlayDeck(CampaignRun run, PhaseDef phase, int seed)
        {
            var party = run.BuildDeckParty(phase);
            var enemies = run.BuildDeckEnemies(phase);
            var battle = new PartyBattle(party, enemies, seed, content.timing)
            {
                SpareTarget = run.SpareIn(phase, enemies),
                SurrenderAfterRound = run.SabotageRounds(party, phase),
            };
            var random = new GameRandom(seed + 7);
            var guard = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && guard++ < 2000)
            {
                var actor = battle.Current;
                BattleAction action;
                if (actor.IsPlayer)
                {
                    Combatant target = null;
                    foreach (var enemy in battle.Enemies)
                    {
                        if (enemy.Alive && enemy != battle.SpareTarget && (target == null || enemy.Hp < target.Hp)) target = enemy;
                    }
                    action = new BattleAction { Kind = BattleActionKind.Attack, Target = target };
                }
                else
                {
                    action = battle.ChooseEnemyAction(actor);
                }
                var roll = random.Value();
                action.Timing = roll < 0.25f ? TimingGrade.Perfect : roll < 0.7f ? TimingGrade.Good : TimingGrade.Miss;
                battle.Submit(action);
            }
            if (battle.Outcome != BattleOutcome.Victory) return false;
            run.FinishDeck(phase, battle.Party);
            return true;
        }
    }
}
