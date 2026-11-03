using System;
using System.Collections.Generic;

namespace Airships.Core
{
    [Serializable]
    public class CrewProgress
    {
        public string id;
        public int level = 1;
        public int xp;
    }

    [Serializable]
    public class UpgradeProgress
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class ItemStack
    {
        public string id;
        public int count;
    }

    [Serializable]
    public class PlayerSettingsData
    {
        public bool highDefinition = true;
        public bool definitionChosen;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.9f;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public int gold;
        public int encountersWon;
        public List<CrewProgress> crew = new List<CrewProgress>();
        public List<string> party = new List<string>(); // Crew who board, in order. The captain always leads.
        public List<UpgradeProgress> upgrades = new List<UpgradeProgress>();
        public PlayerSettingsData settings = new PlayerSettingsData();
        public List<ShipProgress> fleet = new List<ShipProgress>();
        public string activeShip = "";
        public CampaignProgress campaign = new CampaignProgress();
        public List<ItemStack> items = new List<ItemStack>();

        // A new game has one crew member and no ship of their own. The prologue changes both.
        public static SaveData NewGame(GameContent content)
        {
            var save = new SaveData { gold = content.economy.startingGold };
            save.crew.Add(new CrewProgress { id = Campaign.Protagonist });
            Campaign.Begin(save, content);
            save.EnsureValidParty();
            return save;
        }

        // Keeps the party valid for saves from before party choice, and after crew changes.
        public void EnsureValidParty(bool fill = true)
        {
            party.RemoveAll(id => crew.Find(c => c.id == id) == null);
            if (crew.Count == 0) return;

            var captain = crew[0].id;
            party.Remove(captain);
            party.Insert(0, captain);
            foreach (var member in crew)
            {
                if (!fill || party.Count >= BattleSetup.MaxPartySize) break;
                if (!party.Contains(member.id)) party.Add(member.id);
            }
            if (party.Count > BattleSetup.MaxPartySize) party.RemoveRange(BattleSetup.MaxPartySize, party.Count - BattleSetup.MaxPartySize);
        }

        public bool IsInParty(string id) => party.Contains(id);

        public int ItemCount(string id) => items.Find(i => i.id == id)?.count ?? 0;

        public void AddItem(string id, int amount = 1)
        {
            var stack = items.Find(i => i.id == id);
            if (stack == null)
            {
                stack = new ItemStack { id = id };
                items.Add(stack);
            }
            stack.count = Math.Max(0, stack.count + amount);
        }

        public bool BuyItem(ItemDef item)
        {
            if (gold < item.cost) return false;
            gold -= item.cost;
            AddItem(item.id);
            return true;
        }

        // Sends a party member to the reserve and brings in the first crew member waiting there.
        // The captain can't be swapped out. Returns whether anything changed.
        public bool SwapOut(string id)
        {
            var slot = party.IndexOf(id);
            if (slot <= 0) return false;
            var reserve = crew.Find(c => !party.Contains(c.id));
            if (reserve == null) return false;
            party[slot] = reserve.id;
            return true;
        }

        public int UpgradeLevel(string id)
        {
            var found = upgrades.Find(u => u.id == id);
            return found?.level ?? 0;
        }
    }

    public static class Growth
    {
        // Each stat grows at the class rate, scaled by how far this individual's base value sits
        // above or below the class's base value. See claude_logs for the design notes.
        public static float Rate(float classGrowth, float classBase, float individualBase, float influence)
        {
            if (classBase <= 0f) return classGrowth;
            var ratio = individualBase / classBase;
            return classGrowth * (1f + (ratio - 1f) * influence);
        }

        public static int StatAtLevel(float individualBase, float classBase, float classGrowth, int level, float influence)
        {
            var rate = Rate(classGrowth, classBase, individualBase, influence);
            return (int)Math.Floor(individualBase + rate * (level - 1));
        }

        public static StatBlock StatsAtLevel(StatBlock individual, ClassDef cls, int level, float influence)
        {
            return new StatBlock
            {
                hp = StatAtLevel(individual.hp, cls.baseStats.hp, cls.growth.hp, level, influence),
                mp = StatAtLevel(individual.mp, cls.baseStats.mp, cls.growth.mp, level, influence),
                attack = StatAtLevel(individual.attack, cls.baseStats.attack, cls.growth.attack, level, influence),
                defense = StatAtLevel(individual.defense, cls.baseStats.defense, cls.growth.defense, level, influence),
                spirit = StatAtLevel(individual.spirit, cls.baseStats.spirit, cls.growth.spirit, level, influence),
                speed = StatAtLevel(individual.speed, cls.baseStats.speed, cls.growth.speed, level, influence),
            };
        }

        public static int XpToNext(EconomyDef economy, int level)
        {
            return (int)Math.Round(economy.xpCurveBase * Math.Pow(level, economy.xpCurveExponent));
        }

        // Returns the number of levels gained.
        public static int AddXp(EconomyDef economy, CrewProgress member, int amount)
        {
            var gained = 0;
            member.xp += amount;
            while (member.level < economy.maxLevel && member.xp >= XpToNext(economy, member.level))
            {
                member.xp -= XpToNext(economy, member.level);
                member.level++;
                gained++;
            }
            if (member.level >= economy.maxLevel) member.xp = 0;
            return gained;
        }
    }

    public static class Shop
    {
        public static int Cost(UpgradeDef upgrade, int currentLevel)
        {
            return (int)Math.Round(upgrade.baseCost * Math.Pow(upgrade.costGrowth, currentLevel));
        }

        public static bool CanBuy(SaveData save, UpgradeDef upgrade)
        {
            var level = save.UpgradeLevel(upgrade.id);
            return level < upgrade.maxLevel && save.gold >= Cost(upgrade, level);
        }

        public static bool Buy(SaveData save, UpgradeDef upgrade)
        {
            if (!CanBuy(save, upgrade)) return false;
            var level = save.UpgradeLevel(upgrade.id);
            save.gold -= Cost(upgrade, level);
            var entry = save.upgrades.Find(u => u.id == upgrade.id);
            if (entry == null)
            {
                entry = new UpgradeProgress { id = upgrade.id };
                save.upgrades.Add(entry);
            }
            entry.level = level + 1;
            return true;
        }
    }

    public struct EncounterReward
    {
        public int Gold;
        public int Xp;
    }

    public static class Encounter
    {
        public static int Tier(SaveData save) => save.encountersWon;

        public static EncounterReward DogfightWin(EconomyDef e, int tier) => new EncounterReward
        {
            Gold = e.dogfightGold + e.dogfightGoldPerTier * tier,
            Xp = e.dogfightXp + e.dogfightXpPerTier * tier,
        };

        public static EncounterReward BoardingWin(EconomyDef e, int tier) => new EncounterReward
        {
            Gold = e.boardingGold + e.boardingGoldPerTier * tier,
            Xp = e.boardingXp + e.boardingXpPerTier * tier,
        };

        public static EncounterReward Defeat(EconomyDef e) => new EncounterReward { Gold = e.defeatGold };

        public static void Apply(SaveData save, GameContent content, EncounterReward reward, List<string> levelUps = null)
        {
            save.gold += reward.Gold;
            if (reward.Xp <= 0) return;
            foreach (var member in save.crew)
            {
                if (Growth.AddXp(content.economy, member, reward.Xp) > 0) levelUps?.Add(member.id);
            }
        }

        // Applies the player's purchased upgrades to their ship definition.
        public const string PlayerTorpedoCannon = "torpedo_tube"; // Every ship the player flies mounts one. Enemies keep their ship's guns.

        public static ShipLoadout PlayerLoadout(GameContent content, SaveData save, string shipId)
        {
            var ship = content.Ship(shipId);
            var signature = !string.IsNullOrEmpty(save.campaign.equippedSignature) ? save.campaign.equippedSignature : ship.signature;
            var loadout = new ShipLoadout
            {
                Hull = ship.hull,
                Armor = ship.armor,
                Speed = ship.speed,
                TurnDegrees = ship.turnDegrees,
                Radius = ship.radius,
                Torpedo = content.Cannon(PlayerTorpedoCannon),
                Broadside = content.Cannon(ship.broadsideCannon),
                TorpedoDamageScale = 1f,
                BroadsideDamageScale = 1f,
                Signature = !string.IsNullOrEmpty(signature) ? content.Signature(signature) : null,
            };
            foreach (var upgrade in content.upgrades) ApplyStat(loadout, upgrade.stat, upgrade.perLevel * save.UpgradeLevel(upgrade.id));
            foreach (var id in save.campaign.modules)
            {
                var module = content.Module(id);
                ApplyStat(loadout, module.stat, module.amount);
            }
            foreach (var perk in Campaign.ActivePerks(save, content)) ApplyStat(loadout, perk.stat, perk.amount);
            return loadout;
        }

        static void ApplyStat(ShipLoadout loadout, string stat, float amount)
        {
            switch (stat)
            {
                case "hull": loadout.Hull *= 1f + amount; break;
                case "armor": loadout.Armor += amount; break;
                case "speed": loadout.Speed *= 1f + amount; break;
                case "turn": loadout.TurnDegrees *= 1f + amount; break;
                case "torpedoDamage": loadout.TorpedoDamageScale += amount; break;
                case "broadsideDamage": loadout.BroadsideDamageScale += amount; break;
                default: throw new InvalidOperationException($"Unknown upgrade stat '{stat}'");
            }
        }

        // The later in the game, the faster the torpedoes coming at you. The first level has none.
        public static CannonDef EnemyTorpedo(GameContent content, int tier)
        {
            if (tier <= 0) return null;
            return content.Cannon(tier <= 1 ? "torpedo_slow" : tier <= 3 ? "torpedo_steady" : tier <= 5 ? "torpedo_fast" : "torpedo_rapid");
        }

        public static ShipLoadout EnemyLoadout(GameContent content, string shipId, int tier)
        {
            var ship = content.Ship(shipId);
            var scale = 1f + content.economy.enemyShipScalePerTier * tier;
            var damageScale = content.economy.enemyStartingDamageScale * scale;
            return new ShipLoadout
            {
                Hull = ship.hull * scale,
                Armor = ship.armor,
                Speed = ship.speed,
                TurnDegrees = ship.turnDegrees,
                Radius = ship.radius,
                Torpedo = ship.noTorpedo ? null : EnemyTorpedo(content, tier),
                ChaseGuns = content.Cannon(ship.chaseCannon),
                Broadside = content.Cannon(ship.broadsideCannon),
                TorpedoDamageScale = damageScale,
                ChaseDamageScale = damageScale,
                BroadsideDamageScale = damageScale,
            };
        }
    }
}
