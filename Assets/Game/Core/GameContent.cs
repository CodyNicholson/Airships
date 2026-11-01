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
        public bool ranged; // Attacks from where they stand instead of lunging.
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
        public string status; // burn, stun, exposed or haste.
        public int statusTurns;
        public float statusChance;
        public int hits; // Above one, it strikes this many times.
        public bool revive; // Brings a fallen ally back.

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
        public string[] grants; // Campaign mechanics this person brings aboard.
        public string look; // How they appear on deck. See LookDef.
        public SkillUnlock[] signature; // Skills only this person knows.
    }

    // A character's model, palette and what they carry.
    [Serializable]
    public class LookDef
    {
        public string id;
        public string asset;
        public string faction;
        public string weaponR;
        public string weaponL;
        public string gunR;
        public string gunL;
        public string portrait;
    }

    // Used from a deck battle's menu. Bought in port or found as loot.
    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string description;
        public string icon;
        public string kind; // heal, damage or patch.
        public float power;
        public int cost;
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
        public float elevationDegrees; // How far up or down the guns can angle toward a target.
    }

    // The sky hazards in a dogfight. Collision damage is each hazard's radius times its damage per meter.
    [Serializable]
    public class HazardDef
    {
        public int rockCount;
        public float rockMinRadius;
        public float rockMaxRadius;
        public float rockDamagePerMeter;
        public int debrisCount;
        public float debrisMinRadius;
        public float debrisMaxRadius;
        public float debrisDamagePerMeter;
        public float debrisToughnessPerMeter;
        public int jellyfishCount;
        public float jellyfishMinRadius;
        public float jellyfishMaxRadius;
        public float jellyfishDamagePerMeter;
        public int fishCount;
        public float fishRadius;
        public float fishDamagePerMeter;
        public float fishSpeed;
        public int whaleMinTier;
        public float whaleFirstDelayMin;
        public float whaleFirstDelayMax;
        public float whaleRestMin;
        public float whaleRestMax;
        public float whaleAlertSeconds;
        public float whaleLungeSeconds;
        public float whaleLungeSpeed;
        public float whaleCruiseSpeed;
        public float whaleMouthRadius;
        public float whaleSenseRange;
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
        public string model; // Sloop, Brigantine or Galleon.
        public string faction; // The kit palette: Crimson, Azure, Emerald or Obsidian.
        public string signature; // The weapon this ship is known for, if any.
        public string description;
        public float modelScale; // Zero means the model's own size.
        public string icon;
    }

    // Broadside shot types. Scales multiply the cannon's damage against each part of the target.
    [Serializable]
    public class AmmoDef
    {
        public string id;
        public string name;
        public string icon;
        public string mechanic; // The campaign unlock that makes it available. Empty means always.
        public float hullScale;
        public float engineScale;
        public float gunScale;
        public float deckScale; // Share of the damage that wounds the crew they'll meet on deck.
        public float burnSeconds;
        public float burnPerSecond;
        public float hitRadiusBonus;
        public float reloadScale;
        public bool ignoresElevation;
    }

    // A captured flagship's weapon, fired with its own button.
    [Serializable]
    public class SignatureDef
    {
        public string id;
        public string name;
        public string kind; // harpoon, afterburner, ram or thunder.
        public string description;
        public float cooldown;
        public float range; // Harpoon reach, or a ram's damage multiplier.
        public float seconds;
        public float power;
    }

    // A part stripped from a scuttled ship. It improves whichever ship the player flies.
    [Serializable]
    public class ModuleDef
    {
        public string id;
        public string name;
        public string description;
        public string stat;
        public float amount;
    }

    // Rules for ship parts, boarding and crew abilities in the sky.
    [Serializable]
    public class SkyRulesDef
    {
        public float subsystemHullShare;
        public float disabledSpeedScale;
        public float disabledReloadScale;
        public float aimHullScale;
        public float aimSubsystemScale;
        public float grappleRange;
        public float grappleAltitude;
        public float grappleHullShare;
        public float grappleCooldown;
        public float snapRangeScale;
        public float hookedSpeedScale;
        public float reelSpeed;
        public float tetherBoardShare; // A harpooned ship can be boarded once its hull is this low.
        public float tetherSinkSpeed;
        public float crushPerSecond;
        public float patchShare;
        public float overdriveSeconds;
        public float overdriveCooldown;
        public float overdriveSpeedScale;
        public float overdriveReloadScale;
        public float overdriveHullCost;
        public float ambushScale;
        public float weakPointScale;
        public float windSpeedScale;
        public float lightningWarning;
        public float lightningRadius;
        public float lightningDamage;
        public float repairGoldPerHull;
        public float minimumStartHullShare;
        public float scrapShare;
        public float nemesisArmorPerStack;
        public float nemesisHullPerStack;
        public float boardingExposure;
        public float exposurePerDefender;
        public float evadeSeconds;
        public float evadeSpeed;
        public float evadeCooldown;
        public float evadeImmunity;
        public float volleyWarning;
        public float rakeScale;
        public float envelopeSinkSpeed;
        public float toxicPerSecond;
        public float missileDamage;
        public float missileSpeed;
        public float missileTurn;
        public float missileSeconds;
        public float mineBlastRadius;
        public float mineBlastDamage;
        public float pickupRadius;
        public float campaignEnemyDamage; // Story enemies hit harder, because their volleys now give warning.
    }

    // A sky's look and dangers. Colors are hex strings such as "1F6FCF".
    [Serializable]
    public class WeatherDef
    {
        public string id;
        public string skyTop;
        public string skyHorizon;
        public string skyBelow;
        public string fog;
        public string sun;
        public float sunIntensity;
        public float hazardScale;
        public float debrisScale;
        public bool whale;
        public int windCount;
        public int cloudCount;
        public int updraftCount;
        public int toxicCount;
        public float lightningEvery;
        public string sky; // The panorama and light from the art kit.
        public float lightBoost; // Above zero, multiplies that light. Interiors need more than the sky gives.
        public bool rain;
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

    // Timed touch prompts in boarding battles. Multipliers apply to damage or healing, and windows are
    // how many seconds a tap may land from the exact moment.
    [Serializable]
    public class TimingDef
    {
        public float attackMiss;
        public float attackGood;
        public float attackPerfect;
        public float defendMiss;
        public float defendGood;
        public float defendPerfect;
        public float goodWindow;
        public float perfectWindow;
        public float targetRadius; // How close a press must land, in prompt units.
        public float swipeDistance; // How far a flick must travel to count.
        public float swipeAngleDegrees; // How far off the shown direction a flick may be.
        public float dragTolerance; // How far a finger may stray from a drag's marker.
        public TimingPattern[] patterns;
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
        public HazardDef hazards;
        public TimingDef timing;
        public AmmoDef[] ammo;
        public SignatureDef[] signatures;
        public ModuleDef[] modules;
        public WeatherDef[] weathers;
        public SkyRulesDef skyRules;
        public LookDef[] looks;
        public ItemDef[] items;

        // Loaded from its own file, because story and mission data is far larger than balance data.
        [NonSerialized] public CampaignDef campaign;

        public ClassDef Class(string id) => Find(classes, c => c.id == id, id);
        public SkillDef Skill(string id) => Find(skills, s => s.id == id, id);
        public CannonDef Cannon(string id) => Find(cannons, c => c.id == id, id);
        public ShipDef Ship(string id) => Find(ships, s => s.id == id, id);
        public UpgradeDef Upgrade(string id) => Find(upgrades, u => u.id == id, id);
        public AmmoDef Ammo(string id) => Find(ammo, a => a.id == id, id);
        public SignatureDef Signature(string id) => Find(signatures, s => s.id == id, id);
        public ModuleDef Module(string id) => Find(modules, m => m.id == id, id);
        public WeatherDef Weather(string id) => Find(weathers, w => w.id == id, id);
        public CrewDef Crew(string id) => Find(crew, c => c.id == id, id);
        public LookDef Look(string id) => Array.Find(looks ?? new LookDef[0], l => l.id == id);
        public ItemDef Item(string id) => Find(items, i => i.id == id, id);

        static T Find<T>(T[] items, Predicate<T> match, string id)
        {
            var found = Array.Find(items, match);
            if (found == null) throw new KeyNotFoundException($"No {typeof(T).Name} with id '{id}'");
            return found;
        }
    }
}
