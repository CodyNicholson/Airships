using System;
using System.Collections.Generic;

namespace Airships.Core
{
    // Balance data loaded from Resources/game_content.json. Field names match the JSON keys.

    [Serializable]
    public class StatBlock
    {
        public float hp;
        public float mp;
        public float attack;
        public float defense;
        public float spirit;
        public float speed;
    }

    [Serializable]
    public class SkillUnlock
    {
        public string skill;
        public int level;
    }

    [Serializable]
    public class ClassDef
    {
        public string id;
        public string name;
        public string role;
        public StatBlock baseStats;
        public StatBlock growth;
        public SkillUnlock[] skills;
    }

    public enum SkillKind { Physical, Magic, Heal }
    public enum SkillTarget { Enemy, AllEnemies, Ally, AllAllies }

    [Serializable]
    public class SkillDef
    {
        public string id;
        public string name;
        public string description;
        public int mpCost;
        public string kind;
        public string target;
        public float power;

        public SkillKind Kind => (SkillKind)Enum.Parse(typeof(SkillKind), kind);
        public SkillTarget Target => (SkillTarget)Enum.Parse(typeof(SkillTarget), target);
    }

    [Serializable]
    public class CrewDef
    {
        public string id;
        public string name;
        public string classId;
        public StatBlock baseStats;
    }

    [Serializable]
    public class EnemyDef
    {
        public string id;
        public string name;
        public string classId;
        public StatBlock baseStats;
        public int minTier;
    }

    [Serializable]
    public class CannonDef
    {
        public string id;
        public string name;
        public string mount; // "Bow" fires forward, "Broadside" fires from the side facing the target.
        public float damage;
        public float reload;
        public int shots;
        public float spreadDegrees;
        public float projectileSpeed;
        public float range;
        public float aimAssistDegrees;
    }

    [Serializable]
    public class ShipDef
    {
        public string id;
        public string name;
        public float hull;
        public float armor;
        public float speed;
        public float turnDegrees;
        public float radius;
        public string bowCannon;
        public string broadsideCannon;
    }

    [Serializable]
    public class UpgradeDef
    {
        public string id;
        public string name;
        public string description;
        public string stat; // hull, armor, speed, turn, bowDamage, broadsideDamage
        public float perLevel;
        public int maxLevel;
        public int baseCost;
        public float costGrowth;
    }

    [Serializable]
    public class EconomyDef
    {
        public int startingGold;
        public int dogfightGold;
        public int dogfightGoldPerTier;
        public int boardingGold;
        public int boardingGoldPerTier;
        public int defeatGold;
        public int boardingXp;
        public int boardingXpPerTier;
        public int dogfightXp;
        public int dogfightXpPerTier;
        public float enemyShipScalePerTier;
        public float enemyStartingDamageScale;
        public int maxLevel;
        public float xpCurveBase;
        public float xpCurveExponent;
        public float baseStatGrowthInfluence;
    }

    [Serializable]
    public class GameContent
    {
        public ClassDef[] classes;
        public SkillDef[] skills;
        public CrewDef[] crew;
        public EnemyDef[] enemies;
        public CannonDef[] cannons;
        public ShipDef[] ships;
        public UpgradeDef[] upgrades;
        public EconomyDef economy;

        public ClassDef Class(string id) => Find(classes, c => c.id == id, id);
        public SkillDef Skill(string id) => Find(skills, s => s.id == id, id);
        public CannonDef Cannon(string id) => Find(cannons, c => c.id == id, id);
        public ShipDef Ship(string id) => Find(ships, s => s.id == id, id);
        public UpgradeDef Upgrade(string id) => Find(upgrades, u => u.id == id, id);

        static T Find<T>(T[] items, Predicate<T> match, string id)
        {
            var found = Array.Find(items, match);
            if (found == null) throw new KeyNotFoundException($"No {typeof(T).Name} with id '{id}'");
            return found;
        }
    }
}
