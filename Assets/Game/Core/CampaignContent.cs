using System;

namespace Airships.Core
{
    // Story and mission data loaded from Resources/campaign_content.json. Field names match the JSON keys.
    // Numbers left out of the JSON read as zero, so properties supply the defaults.

    [Serializable]
    public class SpeakerDef
    {
        public string id;
        public string name;
        public string classId;
        public string look;
    }

    // One line of dialogue. In flight, Trigger says when it plays. In a story phase it's ignored.
    [Serializable]
    public class CommsLine
    {
        public string speaker;
        public string text;
        public string trigger; // start, time, hullBelow, enemyHullBelow, enginesOut, gunsOut, grappleReady, tethered, hook, boarder, stealth, enemyDown, escaping, lightning, betrayal, partyLow, enemyLow, sabotage.
        public float value;
    }

    // Reveals one control with a pulse and a crew line, and repeats the nudge until it's been used.
    [Serializable]
    public class TeachDef
    {
        public string control; // broadside, bow, boost, altitude, grapple, aim, ammo, signature, patch, overdrive, stick.
        public string speaker;
        public string text;
        public float delay;
        public bool afterBetrayal; // Waits for the prologue's turning point instead of the clock.
    }

    [Serializable]
    public class SkySpawn
    {
        public string key; // Names this ship across phases, so damage to it carries over.
        public string shipId;
        public string name;
        public string captain; // A named captain can become a nemesis.
        public string ai; // raider, patrol, corsair, krag or warden.
        public float hullScale;
        public float damageScale;
        public float armorBonus;
        public float accuracy; // Aim wobble in degrees. Higher misses more.
        public bool target; // The ship the phase's objective is about.
        public bool unsinkable;
        public bool flees;
        public bool easyToBoard;
        public int hooks;
        public float harpoonAfter;
        public bool ally; // Flies on the player's side.
        public int wave; // Above zero, arrives once the waves before it are beaten.
        public float missileEvery; // Above zero, launches homing missiles this often.
        public float signatureEvery; // Above zero, uses its ship's signature weapon this often.
        public float standoff; // How close it closes before turning side-on. Zero means the default.

        public float HullScale => hullScale > 0f ? hullScale : 1f;
        public float DamageScale => damageScale > 0f ? damageScale : 1f;
        public int Hooks => hooks > 0 ? hooks : 1;
        public float Accuracy => accuracy > 0f ? accuracy : 10f;
    }

    [Serializable]
    public class DeckSpawn
    {
        public string id;
        public string name;
        public string classId;
        public int level;
        public float hpScale;
        public bool spare;
        public string look; // A class whose model to wear, if not its own.
        public float enrageAt; // Above zero, turns dangerous when health falls below this share.

        public float HpScale => hpScale > 0f ? hpScale : 1f;
    }

    // A character placed in a 3D story scene.
    [Serializable]
    public class StageActor
    {
        public string speaker;
        public string look;
        public string mark; // A socket on the arena.
        public string clip;
        public float yaw;
    }

    // Cutscene data. Positions are world coordinates, or offsets from a socket when a mark is given.
    [Serializable]
    public class ShotShip
    {
        public string ship;
        public float[] at;
        public float[] to;
        public float yaw;
        public float pitch;
        public float bank;
        public bool burning;
    }

    [Serializable]
    public class ShotActor
    {
        public string look;
        public string mark;
        public float[] at;
        public string toMark; // Walks here, or to "to".
        public float[] to;
        public bool run;
        public float yaw;
        public float endYaw; // Which way to face on arrival, when it isn't "yaw".
        public float delay;
        public string clip; // Plays after any walk.
        public string clip2;
        public float clip2Delay;
    }

    [Serializable]
    public class ShotProp
    {
        public string asset;
        public string faction;
        public string mark;
        public float[] at;
        public float yaw;
        public float scale;
    }

    [Serializable]
    public class ShotEffect
    {
        public string preset;
        public float[] at;
        public float delay;
        public float scale;
        public float shake;
        public string sfx;
    }

    [Serializable]
    public class ShotDef
    {
        public string arena; // Empty for open sky.
        public string faction;
        public string weather;
        public string music;
        public bool danger;
        public float seconds; // At least this long, and longer if the line needs reading.
        public float fov;
        public float[] cam;
        public float[] camTo;
        public float[] look;
        public float[] lookTo;
        public string speaker; // Empty for narration.
        public string line;
        public string cardName; // Introduces someone.
        public string cardRole;
        public string title;
        public string subtitle;
        public ShotShip[] ships;
        public ShotActor[] actors;
        public ShotProp[] props;
        public ShotEffect[] effects;
    }

    [Serializable]
    public class PhaseDef
    {
        public string kind; // story, sky, deck, assign or choice.
        public string music; // A song from GameMusic, instead of the default for this kind of phase.
        public ShotDef[] shots; // A story phase with shots plays as a cutscene.
        public string title;

        // A phase that needs the enemy ship afloat. It's skipped when the target was sunk or got away.
        public bool boardOnly;

        // Story: comic panels. Sky and deck: live barks.
        public CommsLine[] lines;

        // Sky.
        public string ship; // Fly this ship instead of the active one, for hops and the prologue.
        public float shipHullScale;
        public string weather;
        public string objective; // sink_all, capture, disable_engines, survive, repel, escort or collect.
        public int pickups; // Things to gather on a collect objective.
        public int mines;
        public string pickupModel;
        public string script; // betrayal, or empty.
        public float seconds;
        public int gliders;
        public float gliderEvery;
        public SkySpawn[] enemies;
        public TeachDef[] teach;

        // Deck.
        public string party; // boarding, defending, solo or full.
        public string location;
        public string arena; // The kit model to fight on.
        public string arenaFaction;
        public string backdrop; // Scenery beside the ship in a story phase.
        public StageActor[] actors; // Story phases with an arena play as a 3D scene.
        public bool below;
        public bool scalesWithBoarders;
        public float healBefore;
        public DeckSpawn[] crew;

        // Choice.
        public string captain;
        public string captainLook;
        public string shipId;
        public string officer;
        public string module;
        public string signature;
        public int gold;
        public int minDefenders;
    }

    [Serializable]
    public class MissionDef
    {
        public string id;
        public int act;
        public string title;
        public string blurb;
        public string kind; // story or side.
        public string requires;
        public string loyaltyCrew; // Side missions unlock when this crew member's loyalty is high enough.
        public int loyaltyNeeded;
        public int gold;
        public int xp;
        public string[] recruits;
        public int recruitLevel;
        public string[] mechanics;
        public string[] ships;
        public string[] signatures;
        public PhaseDef[] phases;
        public bool cipher; // Counts toward the three ciphers shown on the quarterdeck.
        public string[] items; // Loot, one entry per item.

        public bool IsStory => kind != "side" && kind != "bounty";
    }

    [Serializable]
    public class ActDef
    {
        public int number;
        public string name;
        public string setting;
    }

    // Quick conversations on the quarterdeck. Each one builds loyalty once.
    [Serializable]
    public class TalkDef
    {
        public string id;
        public string crew;
        public string after; // The mission that makes this conversation available.
        public string activity; // What they're doing on deck, such as "cleaning a cannon".
        public CommsLine[] lines;
    }

    // A passive reward for a crew member's loyalty, using the same stats as ship upgrades.
    [Serializable]
    public class PerkDef
    {
        public string id;
        public string crew;
        public int loyalty;
        public string name;
        public string description;
        public string stat;
        public float amount;
    }

    [Serializable]
    public class CampaignDef
    {
        public string title;
        public string startingShip;
        public string[] startingMechanics;
        public SpeakerDef[] speakers;
        public ActDef[] acts;
        public MissionDef[] missions;
        public TalkDef[] talks;
        public PerkDef[] perks;
        public string ending;

        public MissionDef Mission(string id)
        {
            var found = Array.Find(missions, m => m.id == id);
            if (found == null) throw new System.Collections.Generic.KeyNotFoundException($"No mission with id '{id}'");
            return found;
        }

        public SpeakerDef Speaker(string id) => Array.Find(speakers, s => s.id == id);
    }
}
