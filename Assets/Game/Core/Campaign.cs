using System;
using System.Collections.Generic;

namespace Airships.Core
{
    [Serializable]
    public class ShipProgress
    {
        public string id;
        public float damage;
    }

    [Serializable]
    public class LoyaltyProgress
    {
        public string crew;
        public int points;
    }

    // A captain who fled or beat the player. They come back tougher, and carry a bounty.
    [Serializable]
    public class NemesisEntry
    {
        public string id;
        public string name;
        public string shipId;
        public int act;
        public int stacks;
    }

    [Serializable]
    public class CrewHealth
    {
        public string id;
        public int hp;
        public int mp;
    }

    [Serializable]
    public class CampaignProgress
    {
        public List<string> completed = new List<string>();
        public List<string> mechanics = new List<string>();
        public List<string> talksSeen = new List<string>();
        public List<string> signatures = new List<string>();
        public List<string> modules = new List<string>();
        public List<LoyaltyProgress> loyalty = new List<LoyaltyProgress>();
        public List<NemesisEntry> nemeses = new List<NemesisEntry>();
        public string equippedSignature = "";

        // The checkpoint: quitting mid-mission resumes at the start of the current phase.
        public string activeMission = "";
        public int activePhase;
        public List<CrewHealth> crewHealth = new List<CrewHealth>();
        public bool targetLost;
    }

    public enum DavyJonesChoice { SeizeTheHelm, PressGang, ScuttleAndPlunder }

    // Campaign rules that change the save. No Unity dependency, so every rule can be tested.
    public static class Campaign
    {
        public const string Protagonist = "vane";

        // TESTING: every story chapter can be started from the Voyage Log at any time, from the very first launch.
        // Set to false before release, and the log goes back to unlocking chapters as they're played.
        public static bool AllChaptersUnlocked = true;

        public static bool IsDone(SaveData save, string missionId) => save.campaign.completed.Contains(missionId);
        public static bool HasMechanic(SaveData save, string mechanic) => save.campaign.mechanics.Contains(mechanic);
        public static bool PrologueDone(SaveData save, GameContent content) => IsDone(save, content.campaign.missions[0].id);

        // Ciphers found so far, shown in the hangar during Act II.
        public static int CiphersFound(SaveData save, GameContent content)
        {
            var found = 0;
            foreach (var mission in content.campaign.missions)
            {
                if (mission.cipher && IsDone(save, mission.id)) found++;
            }
            return found;
        }

        public static int CiphersTotal(GameContent content)
        {
            var total = 0;
            foreach (var mission in content.campaign.missions)
            {
                if (mission.cipher) total++;
            }
            return total;
        }

        public static void Begin(SaveData save, GameContent content)
        {
            var campaign = content.campaign;
            if (campaign == null) return;
            if (campaign.startingMechanics != null) save.campaign.mechanics.AddRange(campaign.startingMechanics);
        }

        // The next unfinished story mission, or null once the story is over.
        public static MissionDef NextStoryMission(SaveData save, GameContent content)
        {
            foreach (var mission in content.campaign.missions)
            {
                if (mission.IsStory && !IsDone(save, mission.id)) return mission;
            }
            return null;
        }

        // Brings a save up to the start of a story mission, as though every story mission before it had been played:
        // their gold, XP, items, recruits, ships, mechanics and signature weapons, plus both the officer and the ship
        // each Davy Jones choice offers. Missions already played are left alone.
        public static void FastForwardTo(SaveData save, GameContent content, MissionDef target)
        {
            foreach (var mission in content.campaign.missions)
            {
                if (mission == target) break;
                if (!mission.IsStory || IsDone(save, mission.id)) continue;
                if (mission.phases != null)
                {
                    foreach (var phase in mission.phases)
                    {
                        if (phase.kind != "choice") continue;
                        // Both the officer and the ship, so a jump never leaves out what either answer would give.
                        ApplyChoice(content, save, phase, DavyJonesChoice.PressGang);
                        if (!string.IsNullOrEmpty(phase.shipId)) AddShip(save, phase.shipId, true);
                    }
                }
                Complete(content, save, mission, false, new List<string>());
            }
        }

        public static bool IsAvailable(SaveData save, GameContent content, MissionDef mission)
        {
            if (!string.IsNullOrEmpty(mission.requires) && !IsDone(save, mission.requires)) return false;
            if (!string.IsNullOrEmpty(mission.loyaltyCrew) && Loyalty(save, mission.loyaltyCrew) < mission.loyaltyNeeded) return false;
            return true;
        }

        public static List<MissionDef> SideMissions(SaveData save, GameContent content)
        {
            var found = new List<MissionDef>();
            foreach (var mission in content.campaign.missions)
            {
                if (mission.kind == "side" && !IsDone(save, mission.id) && IsAvailable(save, content, mission)) found.Add(mission);
            }
            return found;
        }

        public static int Loyalty(SaveData save, string crew)
        {
            return save.campaign.loyalty.Find(l => l.crew == crew)?.points ?? 0;
        }

        public static List<TalkDef> AvailableTalks(SaveData save, GameContent content, string crew)
        {
            var found = new List<TalkDef>();
            if (content.campaign.talks == null) return found;
            foreach (var talk in content.campaign.talks)
            {
                if (talk.crew != crew || save.campaign.talksSeen.Contains(talk.id)) continue;
                if (!string.IsNullOrEmpty(talk.after) && !IsDone(save, talk.after)) continue;
                found.Add(talk);
            }
            return found;
        }

        // Finishes a conversation, returning any perk it earned.
        public static PerkDef CompleteTalk(SaveData save, GameContent content, TalkDef talk)
        {
            if (save.campaign.talksSeen.Contains(talk.id)) return null;
            save.campaign.talksSeen.Add(talk.id);
            var entry = save.campaign.loyalty.Find(l => l.crew == talk.crew);
            if (entry == null)
            {
                entry = new LoyaltyProgress { crew = talk.crew };
                save.campaign.loyalty.Add(entry);
            }
            entry.points++;
            return content.campaign.perks == null ? null : Array.Find(content.campaign.perks, p => p.crew == talk.crew && p.loyalty == entry.points);
        }

        public static IEnumerable<PerkDef> ActivePerks(SaveData save, GameContent content)
        {
            if (content.campaign?.perks == null) yield break;
            foreach (var perk in content.campaign.perks)
            {
                if (Loyalty(save, perk.crew) >= perk.loyalty) yield return perk;
            }
        }

        public static bool Recruit(SaveData save, GameContent content, string crewId, int level)
        {
            if (save.crew.Find(c => c.id == crewId) != null) return false;
            var leader = save.crew.Find(c => c.id == Protagonist);
            var joinLevel = Math.Max(level, leader != null ? Math.Max(1, leader.level - 1) : 1);
            joinLevel = Math.Min(joinLevel, leader != null ? leader.level : joinLevel);
            save.crew.Add(new CrewProgress { id = crewId, level = joinLevel });
            var grants = content.Crew(crewId).grants;
            if (grants != null)
            {
                foreach (var mechanic in grants)
                {
                    if (!save.campaign.mechanics.Contains(mechanic)) save.campaign.mechanics.Add(mechanic);
                }
            }
            save.EnsureValidParty();
            return true;
        }

        public static void AddShip(SaveData save, string shipId, bool makeActive)
        {
            if (save.fleet.Find(s => s.id == shipId) == null) save.fleet.Add(new ShipProgress { id = shipId });
            if (makeActive) save.activeShip = shipId;
        }

        public static void GrantSignature(SaveData save, string signature)
        {
            if (string.IsNullOrEmpty(signature) || save.campaign.signatures.Contains(signature)) return;
            save.campaign.signatures.Add(signature);
            save.campaign.equippedSignature = signature;
        }

        public static float ShipHull(GameContent content, SaveData save, string shipId)
        {
            return Encounter.PlayerLoadout(content, save, shipId).Hull;
        }

        public static float ShipDamage(SaveData save, string shipId) => save.fleet.Find(s => s.id == shipId)?.damage ?? 0f;

        // A ship always limps out with some hull, so a player with no gold is never stranded.
        public static float StartingHull(GameContent content, SaveData save, string shipId)
        {
            var max = ShipHull(content, save, shipId);
            return Math.Max(max * content.skyRules.minimumStartHullShare, max - ShipDamage(save, shipId));
        }

        public static void RecordDamage(GameContent content, SaveData save, string shipId, float hullLeft)
        {
            var ship = save.fleet.Find(s => s.id == shipId);
            if (ship == null) return;
            ship.damage = Math.Max(0f, ShipHull(content, save, shipId) - hullLeft);
        }

        // Hull patches for one air fight: Thorne's free one once he's taught it, and one more if you carry a
        // repair kit. Only one kit counts per fight.
        public static int PatchesFor(SaveData save) => (HasMechanic(save, "patch") ? 1 : 0) + Math.Min(1, save.ItemCount("repair_kit"));

        // A patch beyond Thorne's free one used up a repair kit. A kit you never needed stays in the hold.
        public static void SpendRepairKit(SaveData save, SkyEncounter encounter)
        {
            var freePatches = HasMechanic(save, "patch") ? 1 : 0;
            if (encounter.Config.Patches > freePatches && encounter.Player.PatchesLeft < encounter.Config.Patches - freePatches) save.AddItem("repair_kit", -1);
        }

        public static int RepairCost(GameContent content, SaveData save, string shipId)
        {
            return (int)Math.Ceiling(ShipDamage(save, shipId) * content.skyRules.repairGoldPerHull);
        }

        // Spends what gold there is, repairing as much as it pays for.
        public static bool Repair(GameContent content, SaveData save, string shipId)
        {
            var ship = save.fleet.Find(s => s.id == shipId);
            if (ship == null || ship.damage <= 0f || save.gold <= 0) return false;
            var affordable = save.gold / content.skyRules.repairGoldPerHull;
            var repaired = Math.Min(ship.damage, affordable);
            save.gold -= Math.Min(save.gold, (int)Math.Ceiling(repaired * content.skyRules.repairGoldPerHull));
            ship.damage -= repaired;
            return true;
        }

        // Every defender left aboard protects the ship while the boarding party is away.
        public static float BoardingExposure(GameContent content, SaveData save)
        {
            var rules = content.skyRules;
            var defenders = Math.Max(0, save.crew.Count - save.party.Count);
            return Math.Max(0f, rules.boardingExposure - rules.exposurePerDefender * defenders);
        }

        public static void ApplyBoardingExposure(GameContent content, SaveData save, string shipId)
        {
            var ship = save.fleet.Find(s => s.id == shipId);
            if (ship == null) return;
            var max = ShipHull(content, save, shipId);
            ship.damage = Math.Min(max * (1f - content.skyRules.minimumStartHullShare), ship.damage + max * BoardingExposure(content, save));
        }

        public static void ApplyChoice(GameContent content, SaveData save, PhaseDef phase, DavyJonesChoice choice)
        {
            // Flagship weapons always come with the win, whichever way the player settles the ship.
            GrantSignature(save, phase.signature);
            switch (choice)
            {
                case DavyJonesChoice.SeizeTheHelm:
                    AddShip(save, phase.shipId, true);
                    break;
                case DavyJonesChoice.PressGang:
                    if (!string.IsNullOrEmpty(phase.officer)) Recruit(save, content, phase.officer, 1);
                    var ship = save.fleet.Find(s => s.id == save.activeShip);
                    if (ship != null) ship.damage *= 0.5f;
                    break;
                case DavyJonesChoice.ScuttleAndPlunder:
                    save.gold += phase.gold;
                    if (!string.IsNullOrEmpty(phase.module) && !save.campaign.modules.Contains(phase.module)) save.campaign.modules.Add(phase.module);
                    break;
            }
        }

        public static void RecordNemesis(SaveData save, string captainId, string name, string shipId, int act)
        {
            var entry = save.campaign.nemeses.Find(n => n.id == captainId);
            if (entry == null)
            {
                entry = new NemesisEntry { id = captainId, name = name, shipId = shipId, act = act };
                save.campaign.nemeses.Add(entry);
            }
            entry.stacks = Math.Min(3, entry.stacks + 1);
        }

        public static void ClearNemesis(SaveData save, string captainId) => save.campaign.nemeses.RemoveAll(n => n.id == captainId);

        public static int NemesisBounty(NemesisEntry entry) => 120 + 80 * entry.stacks;

        // A nemesis's ship grows tougher each time they survive.
        public static SkySpawn NemesisSpawn(GameContent content, NemesisEntry entry, bool target)
        {
            var rules = content.skyRules;
            return new SkySpawn
            {
                key = "nemesis_" + entry.id,
                shipId = entry.shipId,
                name = entry.name,
                captain = entry.id,
                ai = "raider",
                hullScale = 1f + rules.nemesisHullPerStack * entry.stacks,
                armorBonus = rules.nemesisArmorPerStack * entry.stacks,
                accuracy = 8f,
                target = target,
                flees = true,
                hooks = 2,
            };
        }

        // The next story fight in a nemesis's own sector brings them back for another go.
        public static NemesisEntry NemesisInSector(SaveData save, int act)
        {
            return save.campaign.nemeses.Find(n => n.act == act);
        }

        // Builds a bounty hunt from a nemesis entry, with the same phases as any capture mission.
        public static MissionDef BountyMission(GameContent content, NemesisEntry entry)
        {
            var level = 2 + entry.act * 3 + entry.stacks;
            return new MissionDef
            {
                id = "bounty_" + entry.id,
                act = entry.act,
                title = $"Bounty: {entry.name}",
                blurb = $"{entry.name} has armored up and is waiting for you.",
                kind = "bounty",
                gold = NemesisBounty(entry),
                xp = 40 + 15 * level,
                phases = new[]
                {
                    new PhaseDef
                    {
                        kind = "sky",
                        objective = "capture",
                        weather = entry.act >= 3 ? "storm" : entry.act == 2 ? "citadel" : entry.act == 1 ? "jetstream" : "scrapwinds",
                        enemies = new[] { NemesisSpawn(content, entry, true) },
                        lines = new[]
                        {
                            new CommsLine { speaker = "nemesis", text = "Back for more? I've had my hull plated for you.", trigger = "start" },
                        },
                    },
                    new PhaseDef
                    {
                        kind = "deck",
                        boardOnly = true,
                        party = "boarding",
                        crew = new[]
                        {
                            new DeckSpawn { id = "nemesis_captain", name = entry.name, classId = "pirate_captain", level = level + 1 },
                            new DeckSpawn { id = "nemesis_crew", name = "Deckhand", classId = "brigand", level = level },
                        },
                    },
                    new PhaseDef { kind = "choice", boardOnly = true, captain = entry.name, shipId = entry.shipId, gold = 150 + 50 * entry.stacks },
                },
            };
        }

        // Gives a mission's rewards once. Replaying a finished side mission pays only its gold and XP.
        public static void Complete(GameContent content, SaveData save, MissionDef mission, bool targetLost, List<string> levelUps)
        {
            var scrap = targetLost ? content.skyRules.scrapShare : 1f;
            var replay = save.campaign.completed.Contains(mission.id);
            Encounter.Apply(save, content, new EncounterReward { Gold = (int)Math.Round(mission.gold * scrap * (replay ? 0.5f : 1f)), Xp = replay ? mission.xp / 2 : mission.xp }, levelUps);
            save.campaign.activeMission = "";
            save.campaign.activePhase = 0;
            save.campaign.crewHealth.Clear();
            save.campaign.targetLost = false;
            if (replay) return;
            if (mission.kind != "bounty") save.campaign.completed.Add(mission.id);
            if (mission.items != null)
            {
                foreach (var item in mission.items) save.AddItem(item);
            }

            if (mission.recruits != null)
            {
                foreach (var recruit in mission.recruits) Recruit(save, content, recruit, mission.recruitLevel);
            }
            if (mission.mechanics != null)
            {
                foreach (var mechanic in mission.mechanics)
                {
                    if (!save.campaign.mechanics.Contains(mechanic)) save.campaign.mechanics.Add(mechanic);
                }
            }
            if (mission.ships != null)
            {
                foreach (var ship in mission.ships) AddShip(save, ship, true);
            }
            if (mission.signatures != null)
            {
                foreach (var signature in mission.signatures) GrantSignature(save, signature);
            }
            save.campaign.activeMission = "";
            save.campaign.activePhase = 0;
            save.campaign.crewHealth.Clear();
            save.campaign.targetLost = false;
        }

        public static List<AmmoDef> AvailableAmmo(GameContent content, SaveData save)
        {
            var found = new List<AmmoDef>();
            foreach (var ammo in content.ammo)
            {
                if (string.IsNullOrEmpty(ammo.mechanic) || HasMechanic(save, ammo.mechanic)) found.Add(ammo);
            }
            return found;
        }

        // Brings a save from before the campaign up to date. The old crew are gone, but the captain's
        // experience, gold and upgrades all carry over. Returns whether anything changed.
        public static bool Migrate(SaveData save, GameContent content)
        {
            if (save.version >= SaveData.CurrentVersion) return false;
            if (save.version < 2)
            {
                var oldCaptain = save.crew.Find(c => c.id == "captain");
                save.crew.Clear();
                save.crew.Add(new CrewProgress { id = Protagonist, level = oldCaptain?.level ?? 1, xp = oldCaptain?.xp ?? 0 });
                save.party.Clear();
                save.campaign = new CampaignProgress();
                save.fleet = new List<ShipProgress>();
                save.activeShip = "";
                Begin(save, content);
                save.EnsureValidParty();
            }
            if (save.version < 3)
            {
                // The bow gun became the torpedo.
                foreach (var upgrade in save.upgrades)
                {
                    if (upgrade.id == "bow") upgrade.id = "torpedo";
                }
                var old = save.campaign.mechanics.IndexOf("bow");
                if (old >= 0) save.campaign.mechanics[old] = "torpedo";
                save.campaign.mechanics.Remove("overdrive"); // Overdrive and AIM were taken out of the game.
                save.campaign.mechanics.Remove("aim");
            }
            save.version = SaveData.CurrentVersion;
            return true;
        }
    }

    // One playthrough of a mission: which phase is next, what carries between phases, and what the
    // result of each phase means. Screens read this and report back.
    public class CampaignRun
    {
        readonly GameContent content;
        readonly SaveData save;

        public MissionDef Mission { get; }
        public int PhaseIndex { get; private set; }
        public Dictionary<string, float> HullShares { get; } = new Dictionary<string, float>();
        public int BoardersLanded { get; private set; }
        public float DeckWounds { get; private set; }
        public bool TargetLost { get; private set; }
        public string LastShipFlown { get; private set; }
        public List<string> LevelUps { get; } = new List<string>();

        public CampaignRun(GameContent content, SaveData save, MissionDef mission, int startPhase = 0)
        {
            this.content = content;
            this.save = save;
            Mission = mission;
            PhaseIndex = startPhase;
            TargetLost = save.campaign.activeMission == mission.id && save.campaign.targetLost;
            SkipUnreachable();
            Checkpoint();
        }

        public bool Finished => PhaseIndex >= Mission.phases.Length;
        public PhaseDef Phase => Finished ? null : Mission.phases[PhaseIndex];

        void Checkpoint()
        {
            save.campaign.activeMission = Mission.kind == "bounty" ? "" : Mission.id;
            save.campaign.activePhase = PhaseIndex;
            save.campaign.targetLost = TargetLost;
        }

        void SkipUnreachable()
        {
            while (!Finished && Phase.boardOnly && TargetLost) PhaseIndex++;
        }

        public void Advance()
        {
            PhaseIndex++;
            SkipUnreachable();
            Checkpoint();
        }

        public string ShipFor(PhaseDef phase) => !string.IsNullOrEmpty(phase.ship) ? phase.ship : save.activeShip;

        // Sets up a sky phase, including a returning nemesis in this sector.
        public SkyConfig BuildSky(PhaseDef phase, int seed)
        {
            var shipId = ShipFor(phase);
            var usesFleet = string.IsNullOrEmpty(phase.ship);
            var loadout = Encounter.PlayerLoadout(content, save, shipId);
            if (phase.shipHullScale > 0f) loadout.Hull *= phase.shipHullScale;
            var hull = usesFleet ? Campaign.StartingHull(content, save, shipId) : loadout.Hull;
            LastShipFlown = shipId;

            var config = new SkyConfig
            {
                Content = content,
                Seed = seed,
                Tier = Math.Max(0, Mission.act * 2 - 1),
                Player = loadout,
                PlayerShipName = content.Ship(shipId).name,
                PlayerHull = hull,
                Phase = phase,
                Objective = string.IsNullOrEmpty(phase.objective) ? "sink_all" : phase.objective,
                Weather = string.IsNullOrEmpty(phase.weather) ? "clear" : phase.weather,
                Patches = Campaign.PatchesFor(save),
                HullShares = HullShares,
            };
            foreach (var mechanic in save.campaign.mechanics) config.Mechanics.Add(mechanic);
            if (phase.teach != null)
            {
                foreach (var item in phase.teach)
                {
                    if (item.control == "altitude" || item.control == "multihook") config.Mechanics.Add(item.control);
                }
            }

            if (phase.enemies != null)
            {
                foreach (var spawn in phase.enemies)
                {
                    // The prologue's scout arrives later, by script.
                    if (phase.script == "betrayal" && spawn.key == "scout") continue;
                    config.Enemies.Add(spawn);
                }
            }

            var nemesis = Mission.kind == "bounty" || phase.script == "betrayal" ? null : Campaign.NemesisInSector(save, Mission.act);
            if (nemesis != null && Mission.IsStory && (config.Objective == "sink_all" || phase.nemesisWelcome) && config.Enemies.Find(e => e.captain == nemesis.id) == null)
            {
                config.Enemies.Add(Campaign.NemesisSpawn(content, nemesis, false));
                config.ExtraLines.Add(new CommsLine { speaker = "nemesis", text = $"{nemesis.name} here. Remember me? My armor remembers you.", trigger = "start" });
            }
            return config;
        }

        // Applies a finished sky phase to the mission and the save. Returns false when it must be retried.
        public bool FinishSky(SkyEncounter encounter)
        {
            foreach (var enemy in encounter.Enemies)
            {
                if (string.IsNullOrEmpty(enemy.Spawn.key)) continue;
                HullShares[enemy.Spawn.key] = enemy.Ship.Hull / enemy.Ship.Loadout.Hull;
            }

            if (encounter.Outcome == SkyOutcome.Lost || encounter.Outcome == SkyOutcome.Eaten)
            {
                foreach (var enemy in encounter.Config.Enemies)
                {
                    if (!string.IsNullOrEmpty(enemy.captain)) Campaign.RecordNemesis(save, enemy.captain, enemy.name, enemy.shipId, Mission.act);
                }
                return false;
            }

            BoardersLanded = encounter.Sim.BoardersLanded;

            Campaign.SpendRepairKit(save, encounter);
            DeckWounds = encounter.DeckWounds(encounter.BoardedEnemy ?? encounter.Target);
            var phase = Phase;
            if (string.IsNullOrEmpty(phase.ship)) Campaign.RecordDamage(content, save, save.activeShip, encounter.Player.Hull);

            var target = encounter.Target;
            if (encounter.Outcome == SkyOutcome.TargetEscaped && target != null)
            {
                TargetLost = true;
                if (!string.IsNullOrEmpty(target.Spawn.captain)) Campaign.RecordNemesis(save, target.Spawn.captain, target.Spawn.name, target.Spawn.shipId, Mission.act);
            }
            else if (encounter.Outcome == SkyOutcome.Boarded && encounter.BoardedEnemy != null && !string.IsNullOrEmpty(encounter.BoardedEnemy.Spawn.captain))
            {
                Campaign.ClearNemesis(save, encounter.BoardedEnemy.Spawn.captain);
            }

            // Nemeses caught up in a fight count as settled if they went down.
            foreach (var enemy in encounter.Enemies)
            {
                if (!string.IsNullOrEmpty(enemy.Spawn.captain) && enemy.Spawn.key != null && enemy.Spawn.key.StartsWith("nemesis_") && !enemy.Ship.Alive && !enemy.Ship.Departed) Campaign.ClearNemesis(save, enemy.Spawn.captain);
            }
            return true;
        }

        public bool ShouldSkipDeck(PhaseDef phase) => phase.scalesWithBoarders && BoardersLanded <= 0;

        public List<Combatant> BuildDeckParty(PhaseDef phase)
        {
            var ids = new List<string>();
            switch (phase.party)
            {
                case "solo":
                    ids.Add(Campaign.Protagonist);
                    break;
                case "defending":
                    foreach (var member in save.crew)
                    {
                        if (!save.party.Contains(member.id)) ids.Add(member.id);
                    }
                    if (ids.Count == 0) ids.AddRange(save.party);
                    if (ids.Count > BattleSetup.MaxPartySize) ids.RemoveRange(BattleSetup.MaxPartySize, ids.Count - BattleSetup.MaxPartySize);
                    break;
                default:
                    ids.AddRange(save.party);
                    break;
            }

            var party = new List<Combatant>();
            foreach (var id in ids)
            {
                var member = content.Crew(id);
                var progress = save.crew.Find(c => c.id == id);
                if (progress == null) continue;
                var combatant = BattleSetup.CreateCrew(content, member, progress.level, party.Count);
                var carried = save.campaign.crewHealth.Find(h => h.id == id);
                if (carried != null)
                {
                    combatant.Hp = Math.Max(1, Math.Min((int)combatant.Stats.hp, carried.hp + (int)(combatant.Stats.hp * phase.healBefore)));
                    combatant.Mp = Math.Min((int)combatant.Stats.mp, carried.mp + (int)(combatant.Stats.mp * phase.healBefore));
                }
                party.Add(combatant);
            }
            return party;
        }

        public List<Combatant> BuildDeckEnemies(PhaseDef phase)
        {
            var enemies = new List<Combatant>();
            var spawns = phase.crew ?? new DeckSpawn[0];
            var count = phase.scalesWithBoarders ? Math.Min(spawns.Length, BoardersLanded) : spawns.Length;
            for (var i = 0; i < count; i++)
            {
                var spawn = spawns[i];
                var cls = content.Class(spawn.classId);
                var combatant = Combatant.Create($"{spawn.id}_{i}", spawn.name, cls, cls.baseStats, Math.Max(1, spawn.level), content, false, i);
                combatant.Stats.hp *= spawn.HpScale;
                combatant.Hp = (int)Math.Round(combatant.Stats.hp * (1f - DeckWounds));
                combatant.Look = spawn.look;
                combatant.EnrageAt = spawn.enrageAt;
                enemies.Add(combatant);
            }
            return enemies;
        }

        public Combatant SpareIn(PhaseDef phase, List<Combatant> enemies)
        {
            if (phase.crew == null) return null;
            for (var i = 0; i < enemies.Count && i < phase.crew.Length; i++)
            {
                if (phase.crew[i].spare) return enemies[i];
            }
            return null;
        }

        // Sable slips below deck and sets charges, so the enemy gives up after a few rounds.
        public int SabotageRounds(List<Combatant> party, PhaseDef phase)
        {
            if (phase.party == "solo" || phase.below || phase.party == "defending") return 0;
            return party.Exists(c => c.Id == "sable") && Campaign.HasMechanic(save, "sabotage") ? 5 : 0;
        }

        public void FinishDeck(PhaseDef phase, IReadOnlyList<Combatant> party)
        {
            foreach (var member in party)
            {
                var entry = save.campaign.crewHealth.Find(h => h.id == member.Id);
                if (entry == null)
                {
                    entry = new CrewHealth { id = member.Id };
                    save.campaign.crewHealth.Add(entry);
                }
                entry.hp = Math.Max(1, member.Hp);
                entry.mp = member.Mp;
            }
            DeckWounds = 0f;
        }

        public void CompleteMission()
        {
            Campaign.Complete(content, save, Mission, TargetLost, LevelUps);
        }
    }
}
