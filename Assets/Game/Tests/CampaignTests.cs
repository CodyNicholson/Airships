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
        public void ShootingEnginesOutSlowsAShipAndLetsHooksLand()
        {
            var sim = Duel(out var a, out var b);
            Assert.IsFalse(sim.CanBoard(a, b), "A healthy ship can't be boarded");
            b.Engines = 0f;
            Assert.IsTrue(sim.CanBoard(a, b));
            b.X = 500f;
            Assert.IsFalse(sim.CanBoard(a, b), "Out of range");
        }

        [Test]
        public void AimingAtEnginesDamagesThemAndLessHull()
        {
            var aimed = FireOneVolley(ShipPart.Engines, "round");
            var plain = FireOneVolley(ShipPart.Hull, "round");
            Assert.Less(aimed.Engines, aimed.MaxSubsystem);
            Assert.AreEqual(plain.MaxSubsystem, plain.Engines);
            Assert.Greater(aimed.Hull, plain.Hull, "Aiming trades hull damage for part damage");
        }

        [Test]
        public void AmmoTypesDoDifferentJobs()
        {
            var chain = FireOneVolley(ShipPart.Hull, "chain");
            var round = FireOneVolley(ShipPart.Hull, "round");
            var fire = FireOneVolley(ShipPart.Hull, "fire");
            Assert.Less(chain.Engines, chain.MaxSubsystem, "Chain-shot wrecks sails");
            Assert.Greater(chain.Hull, round.Hull, "Chain-shot barely hurts the hull");
            Assert.Greater(fire.DeckDamage, 0f, "Fire-shot wounds the crew on deck");
            Assert.Greater(fire.BurnTimer, 0f);
            Assert.AreEqual(0f, round.DeckDamage);
        }

        ShipState FireOneVolley(ShipPart aim, string ammo)
        {
            var sim = Duel(out var a, out var b, 30f);
            a.Aim = aim;
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
            b.Engines = 0f;
            b.HooksRequired = 3;
            b.Speed = 0f;
            Run(sim, 0.1f, new ShipCommand { Grapple = true });
            Assert.AreEqual(1, b.Hooks);
            Assert.AreEqual(-1, sim.BoardedShipId);
            Run(sim, 0.6f, new ShipCommand { Grapple = true });
            Run(sim, 0.6f, new ShipCommand { Grapple = true });
            Assert.AreEqual(b.Id, sim.BoardedShipId, "The third hook boards");

            var other = Duel(out var c, out var d);
            d.Engines = 0f;
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
            Assert.IsTrue(encounter.Sim.CanBoard(encounter.Player, encounter.Target.Ship), "The line can be climbed");
            for (var i = 0; i < 30 * 25 && encounter.Outcome == SkyOutcome.Ongoing; i++) encounter.Step(default);
            Assert.Less(encounter.Player.Hull, hull, "Staying on the line ends in the crush");

            var rider = Sky("capture", new SkySpawn { key = "krag", shipId = "iron_maw", name = "Krag", ai = "krag", target = true, unsinkable = true, harpoonAfter = 0.1f, hooks = 3 });
            for (var i = 0; i < 90 && rider.Target.Ship.TetheredTo < 0; i++) rider.Step(default);
            rider.Step(new ShipCommand { Grapple = true });
            Assert.AreEqual(SkyOutcome.Boarded, rider.Outcome, "One grapple climbs the tether");
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
            ship.Z = wind.Z;
            ship.Heading = wind.Heading;
            Assert.AreEqual(content.skyRules.windSpeedScale, weather.WindScale(ship, content.skyRules.windSpeedScale), "With the wind");
            ship.Heading = wind.Heading + Mathf.PI;
            Assert.AreEqual(1f, weather.WindScale(ship, content.skyRules.windSpeedScale), "Against the wind");

            var updraft = new List<SkyZone>(weather.Zones).Find(z => z.Kind == SkyZoneKind.Updraft);
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
            for (var i = 0; i < 30 * 2; i++) weather.Step(sim, events);
            Assert.Less(ship.Hull, hull, "Standing where the warning was costs you");
        }

        [Test]
        public void ThornesAbilitiesPatchOnceAndOverdriveCosts()
        {
            var sim = Duel(out var a, out _);
            a.PatchesLeft = 1;
            a.Hull = 100f;
            Run(sim, 0.1f, new ShipCommand { Patch = true });
            Assert.Greater(a.Hull, 100f);
            var patched = a.Hull;
            Run(sim, 0.1f, new ShipCommand { Patch = true });
            Assert.AreEqual(patched, a.Hull, 1f, "Only one patch per fight");

            var driven = Duel(out var c, out _);
            var hull = c.Hull;
            Run(driven, 0.1f, new ShipCommand { Overdrive = true });
            Assert.Greater(c.OverdriveTimer, 0f);
            Assert.Less(c.Hull, hull, "Overdrive costs hull");
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
                a.Loadout.BowDamageScale = 3f;
                Run(sim, 0.1f, new ShipCommand { FireBow = true });
                Run(sim, 0.5f);
                return before - b.Hull;
            }
            Assert.Greater(Damage(true, true), Damage(true, false), "A shot at the stern finds the weak point");
            Assert.AreEqual(Damage(false, false), Damage(false, true), 0.5f, "A shot at the bow doesn't");
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

        [Test]
        public void SinkingTheTargetSkipsItsDeckAndChoice()
        {
            var save = Started();
            var mission = new MissionDef
            {
                id = "m",
                phases = new[]
                {
                    new PhaseDef { kind = "sky", objective = "capture", weather = "heist", enemies = new[] { new SkySpawn { key = "t", shipId = "scrap_raider", name = "T", target = true } } },
                    new PhaseDef { kind = "deck", boardOnly = true, crew = new DeckSpawn[0] },
                    new PhaseDef { kind = "choice", boardOnly = true, shipId = "scrap_raider" },
                },
            };
            var run = new CampaignRun(content, save, mission);
            var encounter = new SkyEncounter(run.BuildSky(mission.phases[0], 3));
            encounter.Target.Ship.Hull = 1f;
            encounter.Target.Ship.TakeDamage(10f);
            encounter.Step(default);
            Assert.AreEqual(SkyOutcome.TargetSunk, encounter.Outcome);
            Assert.IsTrue(run.FinishSky(encounter));
            run.Advance();
            Assert.IsTrue(run.Finished);
            Assert.IsTrue(run.TargetLost);
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
            Assert.AreEqual(3, run.BuildDeckParty(new PhaseDef { party = "full" }).Count);
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
            var mission = new MissionDef { id = "m", phases = new[] { new PhaseDef { kind = "sky", weather = "heist", objective = "capture", enemies = new[] { new SkySpawn { key = "t", shipId = "scrap_raider", name = "T", target = true, unsinkable = true, easyToBoard = true } } } } };
            var run = new CampaignRun(content, save, mission);
            var encounter = new SkyEncounter(run.BuildSky(mission.phases[0], 3));
            encounter.Target.Ship.DeckDamage = encounter.Target.Ship.Loadout.Hull * 0.2f;
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
        public void AFleeingCaptainEscapesAndBecomesANemesis()
        {
            var save = Started();
            var mission = new MissionDef { id = "m", act = 1, phases = new[] { new PhaseDef { kind = "sky", weather = "heist", objective = "capture", enemies = new[] { new SkySpawn { key = "g", shipId = "scrap_raider", name = "Gorse", captain = "gorse", target = true, flees = true } } } } };
            var run = new CampaignRun(content, save, mission);
            var encounter = new SkyEncounter(run.BuildSky(mission.phases[0], 3));
            encounter.Target.Ship.Hull = encounter.Target.Ship.Loadout.Hull * 0.1f;
            encounter.Target.Ship.Engines = encounter.Target.Ship.MaxSubsystem;
            for (var i = 0; i < 30 * 40 && encounter.Outcome == SkyOutcome.Ongoing; i++) encounter.Step(default);
            Assert.AreEqual(SkyOutcome.TargetEscaped, encounter.Outcome);
            Assert.IsTrue(run.FinishSky(encounter));
            Assert.AreEqual("gorse", save.campaign.nemeses[0].id);
            Assert.IsTrue(run.TargetLost);
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
            Run(shot, 0.1f, new ShipCommand { FireBow = true });
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
            Assert.IsFalse(Raked(false), "Hitting the bow doesn't");
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
            c.Aim = ShipPart.Envelope;
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
        public void GoodTimingBuildsMomentumForABoardingRush()
        {
            var battle = Deck(out var hero, out var foe, 1);
            foe.Stats.hp = 99999f;
            foe.Hp = 99999;
            hero.Stats.hp = 99999f;
            hero.Hp = 99999;
            Assert.IsFalse(battle.RushReady);
            for (var i = 0; i < 40 && !battle.RushReady; i++)
            {
                var action = battle.Current == hero ? new BattleAction { Kind = BattleActionKind.Attack, Target = foe, Timing = TimingGrade.Perfect } : new BattleAction { Kind = BattleActionKind.Guard };
                battle.Submit(action);
            }
            Assert.IsTrue(battle.RushReady);
            WaitForTurn(battle, hero);
            var hp = foe.Hp;
            battle.Submit(new BattleAction { Kind = BattleActionKind.Rush, Timing = TimingGrade.Good });
            Assert.Less(foe.Hp, hp);
            Assert.AreEqual(0, battle.Momentum, "A rush spends it all");
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
        public void ThorneCanBringBackTheFallen()
        {
            var thorne = Combatant.Create("thorne", "Thorne", content.Class("surgeon"), content.Crew("thorne").baseStats, 5, content, true, 0);
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
        public void CiphersCountForTheQuarterdeck()
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
            Assert.AreNotEqual(SkyOutcome.Ongoing, encounter.Outcome, $"{run.Mission.id} never ended");
            return run.FinishSky(encounter);
        }

        bool PlayDeck(CampaignRun run, PhaseDef phase, int seed)
        {
            var party = run.BuildDeckParty(phase);
            var enemies = run.BuildDeckEnemies(phase);
            var battle = new PartyBattle(party, enemies, seed, content.timing, BattleSetup.MaxFullCrew)
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
