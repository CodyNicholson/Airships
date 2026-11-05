using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public enum SkyOutcome { Ongoing, Won, Boarded, TargetEscaped, Lost, Eaten }

    public class SkyEnemy
    {
        public SkySpawn Spawn;
        public ShipState Ship;
        public DogfightAi Ai;
        public bool Escaping;
        public bool IsMain; // The chapter's main enemy: it can't sink or get away, and only it can be boarded.
        public float VolleyTimer;
        public float SignatureTimer;
        public float MissileTimer;
    }

    public class SkyPickup
    {
        public int Id;
        public float X;
        public float Y;
        public float Z;
        public bool Taken;
    }

    // Everything one air battle needs, whichever mode set it up.
    public class SkyConfig
    {
        public GameContent Content;
        public int Seed;
        public int Tier;
        public ShipLoadout Player;
        public string PlayerShipName;
        public float PlayerHull;
        public HashSet<string> Mechanics = new HashSet<string>();
        public bool AllControls; // Free skies shows every basic control from the start.
        public PhaseDef Phase;
        public List<SkySpawn> Enemies = new List<SkySpawn>();
        public Dictionary<string, float> HullShares = new Dictionary<string, float>();
        public string Objective = "sink_all";
        public string Weather = "clear";
        public int Patches;
        public List<CommsLine> ExtraLines = new List<CommsLine>();
    }

    // One air battle: the combat sim plus enemy pilots, scripted story beats, live barks and the phase's
    // objective. Rules only, so a server or a test can run it without Unity.
    public class SkyEncounter
    {
        const float SpawnDistance = 230f; // From the center, so fights open out of torpedo range.
        const float LoneEnemyOffset = 100f; // A single enemy isn't dead ahead at the start.
        const float KragSpawnGap = 220f; // Krag starts close, to show off the harpoon.
        const float KragSpawnOffset = 40f;
        const float FleeShare = 0.28f;
        const float EscapeDistance = 330f;
        const float ReminderSeconds = 14f;
        const float BetrayalBurnPerSecond = 16f;
        const float BetrayalHullFloorShare = 0.2f;
        const float BetrayalSinkFloor = -30f;
        const float BetrayalSinkSpeed = 3f;
        const float MineRadius = 11f;
        const float MineToughness = 30f;
        const float AllyOffset = 70f;
        const float PickupSpread = 200f;
        const float WaveDistance = 240f;
        const float SignatureRange = 110f;
        const float MainFleeThrottle = 0.7f; // Slow enough for the player to catch.

        readonly SkyConfig config;
        readonly List<SkyEnemy> enemies = new List<SkyEnemy>();
        readonly List<ShipCommand> commands = new List<ShipCommand>();
        readonly List<CommsLine> lines = new List<CommsLine>();
        readonly Queue<CommsLine> pending = new Queue<CommsLine>();
        readonly HashSet<string> usedControls = new HashSet<string>();
        readonly HashSet<CommsLine> played = new HashSet<CommsLine>();
        readonly Dictionary<string, float> teachTimers = new Dictionary<string, float>();
        readonly List<SkyPickup> pickups = new List<SkyPickup>();
        readonly List<SkySpawn> laterWaves = new List<SkySpawn>();
        readonly GameRandom random;
        bool betraying;
        int glidersLeft;
        float gliderTimer;
        float harpoonTimer;

        public DogfightSim Sim { get; }
        public ShipState Player { get; }
        public IReadOnlyList<SkyEnemy> Enemies => enemies;
        public IReadOnlyList<SkyPickup> Pickups => pickups;
        public int PickupsTaken { get; private set; }
        public bool VolleyIncoming { get; private set; }
        public SkyOutcome Outcome { get; private set; }
        public SkyEnemy Target { get; private set; }
        public SkyEnemy Main => enemies.Find(e => e.IsMain);
        public SkyEnemy BoardedEnemy { get; private set; }
        public float Time { get; private set; }
        public string BoardedKey => BoardedEnemy?.Spawn.key;
        public SkyConfig Config => config;
        public bool BetrayalStarted => betraying;

        public SkyEncounter(SkyConfig config)
        {
            this.config = config;
            var content = config.Content;
            random = new GameRandom(config.Seed + 11);
            Sim = new DogfightSim(config.Seed, content.skyRules) { HoldOpen = true };

            Player = Sim.AddShip(0, config.Player, 0f, -SpawnDistance, 0f);
            Player.Hull = Math.Min(config.PlayerHull, config.Player.Hull);
            Player.Ammo = content.Ammo("round");
            Player.PatchesLeft = config.Patches;
            Player.AltitudeLocked = !CanClimb;
            Player.DriftsWhenHurt = true;
            Player.WeakPoints = config.Mechanics.Contains("weakpoints");
            commands.Add(default);

            var first = config.Enemies.FindAll(e => e.wave <= 0);
            for (var i = 0; i < first.Count; i++) AddEnemy(first[i], i, first.Count);
            laterWaves.AddRange(config.Enemies.FindAll(e => e.wave > 0));
            laterWaves.Sort((a, b) => a.wave.CompareTo(b.wave));
            Target = enemies.Find(e => e.Spawn.target) ?? enemies.Find(e => !e.Spawn.ally) ?? (enemies.Count > 0 ? enemies[0] : null);

            var weather = content.Weather(config.Weather);
            // The campaign says how many whales each fight has. Free Skies leaves it to the weather.
            var field = Sim.AddHazards(ScaledHazards(content.hazards, weather), config.Phase == null && weather.whale ? config.Tier : -1, config.Seed + 3);
            if (config.Phase != null && config.Phase.whales > 0) field.AddWhales(config.Phase.whales);
            field.MineBlastRadius = content.skyRules.mineBlastRadius;
            field.MineBlastDamage = content.skyRules.mineBlastDamage;
            if (config.Phase != null && config.Phase.mines > 0) field.AddMines(config.Phase.mines, MineRadius, MineToughness, Sim.Ships);
            if (config.Phase != null && config.Phase.pickups > 0) PlacePickups(config.Phase.pickups);
            var sky = new SkyWeather(weather, content.skyRules, config.Seed + 7, !CanClimb) { LightningTargetId = Player.Id };
            Sim.SetWeather(sky);

            var phase = config.Phase;
            if (phase != null)
            {
                if (phase.lines != null) lines.AddRange(phase.lines);
                glidersLeft = phase.gliders;
                gliderTimer = phase.gliderEvery * 0.5f;
            }
            lines.AddRange(config.ExtraLines);
            Fire("start", 0f);
        }

        public bool CanClimb => config.AllControls || config.Mechanics.Contains("altitude");
        public bool IsTeaching(string control) => CurrentTeach(control) != null && !usedControls.Contains(control);
        public bool HasControl(string control) => config.AllControls && IsBasicControl(control) || config.Mechanics.Contains(control) || CurrentTeach(control) != null
            || control == "patch" && config.Patches > 0; // A repair kit in the hold brings the patch button, taught or not.

        static bool IsBasicControl(string control) => control == "broadside" || control == "torpedo" || control == "boost" || control == "altitude" || control == "stick";

        TeachDef CurrentTeach(string control)
        {
            var teach = config.Phase?.teach;
            if (teach == null) return null;
            foreach (var item in teach)
            {
                if (item.control == control && IsDue(item)) return item;
            }
            return null;
        }

        public void MarkUsed(string control) => usedControls.Add(control);

        bool IsDue(TeachDef item) => Time >= item.delay && (!item.afterBetrayal || betraying);

        // Nothing here can be finished from the sim's point of view: the encounter decides.
        void AddEnemy(SkySpawn spawn, int index, int count)
        {
            var content = config.Content;
            var loadout = Encounter.EnemyLoadout(content, spawn.shipId, config.Tier);
            loadout.Hull *= spawn.HullScale;
            loadout.Armor += spawn.armorBonus;
            var story = config.Phase != null ? content.skyRules.campaignEnemyDamage : 1f;
            loadout.TorpedoDamageScale *= spawn.DamageScale * story;
            loadout.ChaseDamageScale *= spawn.DamageScale * story;
            loadout.BroadsideDamageScale *= spawn.DamageScale * story;
            var signature = content.Ship(spawn.shipId).signature;
            if (!string.IsNullOrEmpty(signature)) loadout.Signature = content.Signature(signature);

            if (spawn.ally) loadout.Torpedo = null;
            var spread = count <= 1 ? LoneEnemyOffset : (index - (count - 1) * 0.5f) * 90f;
            var ally = spawn.ally;
            var ship = ally
                ? Sim.AddShip(0, loadout, Player.X + (index % 2 == 0 ? -1f : 1f) * AllyOffset * (1 + index / 2), Player.Z - 20f, 0f)
                : Sim.AddShip(1, loadout, spread, SpawnDistance, (float)Math.PI);
            if (spawn.ai == "krag" && !ally)
            {
                ship.X = Player.X + KragSpawnOffset;
                ship.Z = Player.Z + KragSpawnGap;
            }
            ship.HuntedByWhale = false;
            ship.AltitudeLocked = Player.AltitudeLocked;
            if (loadout.Torpedo != null) ship.TorpedoCooldown = loadout.Torpedo.reload * random.Range(0.3f, 1f);
            var isMain = config.Phase != null && config.Objective == "capture" && spawn.target && !ally;
            if (config.Phase != null) ship.Boarding = isMain ? BoardingRule.Main : BoardingRule.Never;

            // A ship that can be boarded is never sunk. It's beaten down to its last point of hull instead.
            var boardable = !ally && ship.Boarding != BoardingRule.Never;
            ship.HullFloor = spawn.unsinkable || boardable ? DogfightSim.BoardableHull : 0f;
            ship.HooksRequired = config.Mechanics.Contains("multihook") ? spawn.Hooks : 1;
            ship.TetherDrags = spawn.ai == "krag";
            if (spawn.startHullShare > 0f) ship.Hull = loadout.Hull * spawn.startHullShare;
            if (!string.IsNullOrEmpty(spawn.key) && config.HullShares.TryGetValue(spawn.key, out var share)) ship.Hull = Math.Max(1f, loadout.Hull * share);
            enemies.Add(new SkyEnemy
            {
                Spawn = spawn,
                Ship = ship,
                IsMain = isMain,
                Ai = new DogfightAi(config.Seed + 100 + ship.Id, spawn.Accuracy, spawn.standoff),
                SignatureTimer = spawn.signatureEvery * 0.5f,
                MissileTimer = spawn.missileEvery * 0.6f,
            });
            commands.Add(default);
        }

        void PlacePickups(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = (i + random.Range(0f, 0.6f)) / count * (float)Math.PI * 2f;
                var distance = random.Range(0.5f, 1f) * PickupSpread;
                pickups.Add(new SkyPickup
                {
                    Id = i,
                    X = (float)Math.Sin(angle) * distance,
                    Z = (float)Math.Cos(angle) * distance,
                    Y = CanClimb ? random.Range(-18f, 26f) : 0f,
                });
            }
        }

        void CheckPickups()
        {
            var reach = config.Content.skyRules.pickupRadius;
            foreach (var pickup in pickups)
            {
                if (pickup.Taken) continue;
                var dx = pickup.X - Player.X;
                var dy = pickup.Y - Player.Y;
                var dz = pickup.Z - Player.Z;
                if (dx * dx + dy * dy + dz * dz > reach * reach) continue;
                pickup.Taken = true;
                PickupsTaken++;
                Sim.AnnouncePickup(pickup.Id, pickup.X, pickup.Y, pickup.Z);
                Fire("pickup", 0f);
            }
        }

        // The next wave flies in once everyone hostile has been dealt with.
        void CheckWaves()
        {
            if (laterWaves.Count == 0 || enemies.Exists(e => e.Ship.Alive && !e.Spawn.ally && !e.Ship.BoardsOnContact)) return;
            var wave = laterWaves[0].wave;
            var arriving = laterWaves.FindAll(e => e.wave == wave);
            laterWaves.RemoveAll(e => e.wave == wave);
            for (var i = 0; i < arriving.Count; i++)
            {
                AddEnemy(arriving[i], i, arriving.Count);
                var ship = enemies[enemies.Count - 1].Ship;
                var angle = (float)Math.Atan2(Player.ForwardX, Player.ForwardZ) + (i - (arriving.Count - 1) * 0.5f) * 0.5f;
                ship.X = Player.X + (float)Math.Sin(angle) * WaveDistance;
                ship.Z = Player.Z + (float)Math.Cos(angle) * WaveDistance;
                ship.Heading = angle + (float)Math.PI;
                ship.Y = Player.Y;
                if (arriving[i].target) Target = enemies[enemies.Count - 1];
            }
            Sim.AnnounceWave(Player.X, Player.Y, Player.Z);
            Fire("wave", 0f);
        }

        static HazardDef ScaledHazards(HazardDef source, WeatherDef weather)
        {
            var scale = weather.hazardScale;
            var debris = weather.debrisScale > 0f ? weather.debrisScale : scale;
            return new HazardDef
            {
                rockCount = (int)(source.rockCount * scale),
                rockMinRadius = source.rockMinRadius,
                rockMaxRadius = source.rockMaxRadius,
                rockDamagePerMeter = source.rockDamagePerMeter,
                debrisCount = (int)(source.debrisCount * debris),
                debrisMinRadius = source.debrisMinRadius,
                debrisMaxRadius = source.debrisMaxRadius,
                debrisDamagePerMeter = source.debrisDamagePerMeter,
                debrisToughnessPerMeter = source.debrisToughnessPerMeter,
                jellyfishCount = (int)(source.jellyfishCount * scale),
                jellyfishMinRadius = source.jellyfishMinRadius,
                jellyfishMaxRadius = source.jellyfishMaxRadius,
                jellyfishDamagePerMeter = source.jellyfishDamagePerMeter,
                fishCount = (int)(source.fishCount * scale),
                fishRadius = source.fishRadius,
                fishDamagePerMeter = source.fishDamagePerMeter,
                fishSpeed = source.fishSpeed,
                whaleMinTier = source.whaleMinTier,
                whaleFirstDelayMin = source.whaleFirstDelayMin,
                whaleFirstDelayMax = source.whaleFirstDelayMax,
                whaleRestMin = source.whaleRestMin,
                whaleRestMax = source.whaleRestMax,
                whaleAlertSeconds = source.whaleAlertSeconds,
                whaleLungeSeconds = source.whaleLungeSeconds,
                whaleLungeSpeed = source.whaleLungeSpeed,
                whaleCruiseSpeed = source.whaleCruiseSpeed,
                whaleMouthRadius = source.whaleMouthRadius,
                whaleSenseRange = source.whaleSenseRange,
            };
        }

        // The next line for the comms bar, or null.
        public CommsLine NextComms() => pending.Count > 0 ? pending.Dequeue() : null;

        public void Step(ShipCommand playerCommand)
        {
            if (Outcome != SkyOutcome.Ongoing) return;
            Time += DogfightSim.TickSeconds;

            commands[Player.Id] = playerCommand;
            foreach (var enemy in enemies)
            {
                if (enemy.Ship.Alive) commands[enemy.Ship.Id] = Pilot(enemy);
            }
            LaunchGliders();
            Sim.Step(commands);
            CheckPickups();
            CheckWaves();
            ReadEvents();
            RunBetrayal();
            CheckEscapes();
            CheckBarks();
            CheckTeaching();
            CheckOutcome();
        }

        ShipCommand Pilot(SkyEnemy enemy)
        {
            var ship = enemy.Ship;
            if (ship.BoardsOnContact) return PilotGlider(ship);

            var command = enemy.Ai.Think(Sim, ship);
            command = Announce(enemy, command);
            if (enemy.Spawn.flees && ship.Hull < ship.Loadout.Hull * FleeShare && ship.Hooks == 0 && !ship.EnginesOut)
            {
                if (!enemy.Escaping)
                {
                    enemy.Escaping = true;
                    Fire("escaping", 0f);
                }
                var away = (float)Math.Atan2(ship.X - Player.X, ship.Z - Player.Z);
                command.Turn = DogfightSim.WrapAngle(away - ship.Heading) * 2.2f;
                command.Throttle = enemy.IsMain ? MainFleeThrottle : 1f;
                command.FireTorpedo = false;
                command.FireChaseGuns = false;
                command.FireBroadside = false;
                return command;
            }
            if (ship.TetheredTo >= 0) command.Throttle = 0.4f;

            // A captain with a harpoon waits for the chance, then won't let go.
            if (enemy.Spawn.harpoonAfter > 0f && Time >= enemy.Spawn.harpoonAfter && ship.TetheredTo < 0) command.Signature = true;
            return command;
        }

        // Enemy volleys are announced before they fire, and special weapons follow each ship's habits.
        ShipCommand Announce(SkyEnemy enemy, ShipCommand command)
        {
            var ship = enemy.Ship;
            var rules = config.Content.skyRules;
            if (!enemy.Spawn.ally && ship.BroadsideCooldown <= 0f && !ship.Stunned)
            {
                if (enemy.VolleyTimer > 0f)
                {
                    enemy.VolleyTimer -= DogfightSim.TickSeconds;
                    command.FireBroadside = enemy.VolleyTimer <= 0f;
                }
                else if (command.FireBroadside)
                {
                    enemy.VolleyTimer = rules.volleyWarning;
                    command.FireBroadside = false;
                    Sim.WarnVolley(ship);
                }
            }
            else
            {
                enemy.VolleyTimer = 0f;
            }

            var target = Sim.Opponent(ship);
            if (target == null) return command;
            var distance = (float)Math.Sqrt(DogfightSim.DistanceSquared(ship.X, ship.Z, target.X, target.Z));
            if (enemy.Spawn.signatureEvery > 0f && ship.Loadout.Signature != null)
            {
                enemy.SignatureTimer -= DogfightSim.TickSeconds;
                if (enemy.SignatureTimer <= 0f && distance < SignatureRange)
                {
                    enemy.SignatureTimer = enemy.Spawn.signatureEvery;
                    command.Signature = true;
                }
            }
            if (enemy.Spawn.missileEvery > 0f)
            {
                enemy.MissileTimer -= DogfightSim.TickSeconds;
                if (enemy.MissileTimer <= 0f && !ship.Stunned)
                {
                    enemy.MissileTimer = enemy.Spawn.missileEvery;
                    Sim.LaunchMissile(ship, target);
                    Fire("missile", 0f);
                }
            }
            return command;
        }

        ShipCommand PilotGlider(ShipState glider)
        {
            var toPlayer = (float)Math.Atan2(Player.X - glider.X, Player.Z - glider.Z);
            return new ShipCommand
            {
                Turn = DogfightSim.WrapAngle(toPlayer - glider.Heading) * 2.2f,
                Throttle = 1f,
                Climb = Math.Max(-1f, Math.Min(1f, (Player.Y - glider.Y) / 8f)),
            };
        }

        void LaunchGliders()
        {
            if (glidersLeft <= 0 || Target == null || !Target.Ship.Alive) return;
            gliderTimer -= DogfightSim.TickSeconds;
            if (gliderTimer > 0f) return;
            gliderTimer = config.Phase.gliderEvery;
            glidersLeft--;
            var loadout = Encounter.EnemyLoadout(config.Content, "assault_glider", config.Tier);
            var glider = Sim.LaunchGlider(Target.Ship, loadout);
            enemies.Add(new SkyEnemy { Spawn = new SkySpawn { shipId = "assault_glider", name = "Assault Glider", ai = "glider" }, Ship = glider });
            commands.Add(default);
        }

        void ReadEvents()
        {
            VolleyIncoming = false;
            foreach (var enemy in enemies)
            {
                if (enemy.VolleyTimer > 0f && enemy.Ship.Alive && Sim.Opponent(enemy.Ship) == Player) VolleyIncoming = true;
            }
            foreach (var e in Sim.Events)
            {
                switch (e.Kind)
                {
                    case DogfightEventKind.PartDisabled:
                        if (e.ShipId != Player.Id) Fire(e.Part == ShipPart.Engines ? "enginesOut" : "gunsOut", 0f);
                        break;
                    case DogfightEventKind.Tethered: Fire("tethered", 0f); break;
                    case DogfightEventKind.VolleyWarning: Fire("volley", 0f); break;
                    case DogfightEventKind.HookLanded:
                        MarkUsed("grapple");
                        MarkUsed("multihook");
                        Fire("hook", 0f);
                        break;
                    case DogfightEventKind.BoarderLanded: Fire("boarder", 0f); break;
                    case DogfightEventKind.LightningWarning: Fire("lightning", 0f); break;
                    case DogfightEventKind.Destroyed:
                        if (e.ShipId != Player.Id) Fire("enemyDown", 0f);
                        break;
                }
            }
        }

        // The prologue's turn: once the patrols are gone, the Lift-Core burns and a scout closes in.
        void RunBetrayal()
        {
            if (config.Phase?.script != "betrayal") return;
            if (!betraying)
            {
                if (enemies.Exists(e => e.Ship.Alive && e.Spawn.key != "scout")) return;
                betraying = true;
                Player.HullFloor = Player.Loadout.Hull * BetrayalHullFloorShare;
                Fire("betrayal", 0f);
                var spawn = Array.Find(config.Phase.enemies, s => s.key == "scout");
                if (spawn != null)
                {
                    AddEnemy(spawn, 0, 1);
                    Target = enemies[enemies.Count - 1];
                    var scout = Target.Ship;
                    scout.X = Player.X + Player.ForwardX * 70f + 40f;
                    scout.Z = Player.Z + Player.ForwardZ * 70f;
                    scout.Y = Player.Y;
                    scout.Heading = Player.Heading;
                }
                return;
            }
            Player.TakeDamage(BetrayalBurnPerSecond * DogfightSim.TickSeconds);
            if (Player.Y > BetrayalSinkFloor) Player.Y -= BetrayalSinkSpeed * DogfightSim.TickSeconds;
            if (Target.Ship.Alive) Target.Ship.Y = Player.Y;
        }

        void CheckEscapes()
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.Escaping || !enemy.Ship.Alive || enemy.IsMain) continue;
                var dx = enemy.Ship.X - Player.X;
                var dz = enemy.Ship.Z - Player.Z;
                if (dx * dx + dz * dz >= EscapeDistance * EscapeDistance) Sim.Depart(enemy.Ship);
            }
        }

        void Fire(string trigger, float _)
        {
            foreach (var line in lines)
            {
                if (line.trigger != trigger || played.Contains(line)) continue;
                played.Add(line);
                pending.Enqueue(line);
            }
        }

        void CheckBarks()
        {
            foreach (var line in lines)
            {
                if (played.Contains(line)) continue;
                var hit = false;
                switch (line.trigger)
                {
                    case "time": hit = Time >= line.value; break;
                    case "hullBelow": hit = Player.Hull < Player.Loadout.Hull * line.value; break;
                    case "enemyHullBelow": hit = Target != null && Target.Ship.Hull < Target.Ship.Loadout.Hull * line.value; break;
                    case "grappleReady": hit = Sim.BoardingTarget(Player) != null; break;
                    case "stealth": hit = Player.Hidden; break;
                }
                if (!hit) continue;
                played.Add(line);
                pending.Enqueue(line);
            }
        }

        // A control's line plays when it appears, and again if the player hasn't tried it for a while.
        void CheckTeaching()
        {
            var teach = config.Phase?.teach;
            if (teach == null) return;
            foreach (var item in teach)
            {
                if (!IsDue(item) || usedControls.Contains(item.control)) continue;
                if (!teachTimers.TryGetValue(item.control, out var next) || Time >= next)
                {
                    teachTimers[item.control] = Time + ReminderSeconds;
                    if (!string.IsNullOrEmpty(item.text)) pending.Enqueue(new CommsLine { speaker = item.speaker, text = item.text });
                }
            }
        }

        void CheckOutcome()
        {
            if (!Player.Alive)
            {
                Outcome = Player.Eaten ? SkyOutcome.Eaten : SkyOutcome.Lost;
                return;
            }
            if (Sim.BoardedShipId >= 0)
            {
                BoardedEnemy = enemies.Find(e => e.Ship.Id == Sim.BoardedShipId);
                Outcome = SkyOutcome.Boarded;
                return;
            }

            var anyAlive = laterWaves.Count > 0;
            foreach (var enemy in enemies)
            {
                if (enemy.Ship.Alive && !enemy.Ship.BoardsOnContact && !enemy.Spawn.ally) anyAlive = true;
            }
            var glidersFlying = glidersLeft > 0 || enemies.Exists(e => e.Ship.BoardsOnContact && e.Ship.Alive);

            switch (config.Objective)
            {
                case "capture":
                    break; // Only boarding the main enemy ends this, and that's handled above.
                case "disable_engines":
                    if (Target != null && (Target.Ship.EnginesOut || !Target.Ship.Alive)) Outcome = SkyOutcome.Won;
                    break;
                case "survive":
                    if (Time >= config.Phase.seconds || !anyAlive) Outcome = SkyOutcome.Won;
                    break;
                case "repel":
                    if (!glidersFlying) Outcome = SkyOutcome.Won;
                    break;
                case "escort":
                    var protectedShip = enemies.Find(e => e.Spawn.ally && e.Spawn.target);
                    if (protectedShip != null && !protectedShip.Ship.Alive) Outcome = SkyOutcome.Lost;
                    else if (Time >= config.Phase.seconds && config.Phase.seconds > 0f || !anyAlive && !glidersFlying) Outcome = SkyOutcome.Won;
                    break;
                case "collect":
                    if (PickupsTaken >= pickups.Count) Outcome = SkyOutcome.Won;
                    break;
                default:
                    if (!anyAlive && !glidersFlying) Outcome = Target != null && Target.Ship.Departed && enemies.Count == 1 ? SkyOutcome.TargetEscaped : SkyOutcome.Won;
                    break;
            }
        }

        // What the crew they'll fight on deck has already suffered, from 0 to 1.
        public float DeckWounds(SkyEnemy enemy)
        {
            if (enemy == null) return 0f;
            return Math.Min(0.4f, enemy.Ship.DeckDamage / Math.Max(1f, enemy.Ship.Loadout.Hull));
        }
    }

    // A competent automated captain for campaign fights, used by tests and automated runs. It only
    // produces the commands a player could, so whatever it wins, a person can win too.
    public class AutoCaptain
    {
        const float TorpedoDodgeRange = 70f;

        static bool TorpedoClose(DogfightSim sim, ShipState ship)
        {
            foreach (var shot in sim.Projectiles)
            {
                if (shot.Team != ship.Team && shot.HomingOn == ship.Id && DogfightSim.DistanceSquared(shot.X, shot.Z, ship.X, ship.Z) < TorpedoDodgeRange * TorpedoDodgeRange) return true;
            }
            return false;
        }

        readonly DogfightAi pilot;
        readonly GameRandom random;
        readonly float evadeSkill;

        public AutoCaptain(int seed, float aimJitter = 4f, float evadeSkill = 0.05f)
        {
            pilot = new DogfightAi(seed, aimJitter);
            random = new GameRandom(seed + 31);
            this.evadeSkill = evadeSkill;
        }

        // Picks the shot a thoughtful player would use for the job at hand.
        public void ChooseAmmo(SkyEncounter encounter, List<AmmoDef> available)
        {
            var objective = encounter.Config.Objective;
            var target = encounter.Target?.Ship;
            var wanted = "round";
            if (objective == "repel") wanted = "flak";
            else if (objective == "disable_engines" && target != null && !target.EnginesOut) wanted = "chain";
            var pick = available.Find(a => a.id == wanted) ?? available.Find(a => a.id == "round");
            if (pick != null) encounter.Player.Ammo = pick;
        }

        public ShipCommand Think(SkyEncounter encounter)
        {
            var sim = encounter.Sim;
            var ship = encounter.Player;
            var command = pilot.Think(sim, ship);
            if (encounter.HasControl("broadside")) encounter.MarkUsed("broadside");
            if (encounter.HasControl("stick")) encounter.MarkUsed("stick");

            var objective = encounter.Config.Objective;
            var target = encounter.Target?.Ship;

            // Roll out of the way of an announced volley, when the roll is ready.
            if ((encounter.VolleyIncoming || TorpedoClose(sim, ship)) && ship.EvadeCooldown <= 0f && random.Value() < evadeSkill) command.Evade = random.Value() < 0.5f ? -1f : 1f;

            // Fly to the nearest thing that still needs collecting.
            if (objective == "collect")
            {
                SkyPickup nearest = null;
                var best = float.MaxValue;
                foreach (var pickup in encounter.Pickups)
                {
                    if (pickup.Taken) continue;
                    var d = DogfightSim.DistanceSquared(ship.X, ship.Z, pickup.X, pickup.Z);
                    if (d >= best) continue;
                    best = d;
                    nearest = pickup;
                }
                if (nearest != null)
                {
                    var bearing = (float)Math.Atan2(nearest.X - ship.X, nearest.Z - ship.Z);
                    command.Turn = DogfightSim.WrapAngle(bearing - ship.Heading) * 2.2f;
                    command.Throttle = 1f;
                    command.Climb = Math.Max(-1f, Math.Min(1f, (nearest.Y - ship.Y) / 6f));
                }
                return command;
            }

            var wantsBoard = encounter.HasControl("grapple") && target != null && target.Alive && (objective == "capture" || sim.IsBoardable(target));

            if (encounter.HasControl("signature") && ship.Loadout.Signature != null)
            {
                var signature = ship.Loadout.Signature;
                var inRange = target != null && target.Alive && (signature.kind != "harpoon" || (DogfightSim.DistanceSquared(ship.X, ship.Z, target.X, target.Z) <= signature.range * signature.range && target.Hull <= target.Loadout.Hull * sim.Rules.tetherBoardShare));
                command.Signature = inRange && ship.SignatureCooldown <= 0f && encounter.Time > 6f;
                if (command.Signature) encounter.MarkUsed("signature");
            }
            if (encounter.HasControl("patch") && ship.Hull < ship.Loadout.Hull * 0.5f)
            {
                command.Patch = true;
                encounter.MarkUsed("patch");
            }

            // Once a ship can be boarded, fly straight at it and throw hooks.
            if (wantsBoard && target != null && sim.IsBoardable(target) || encounter.HasControl("grapple") && sim.BoardingTarget(ship) != null)
            {
                var chase = target != null && target.Alive ? target : sim.BoardingTarget(ship);
                if (chase != null)
                {
                    var to = (float)Math.Atan2(chase.X - ship.X, chase.Z - ship.Z);
                    command.Turn = DogfightSim.WrapAngle(to - ship.Heading) * 2.2f;
                    command.Throttle = 1f;
                    command.Climb = Math.Max(-1f, Math.Min(1f, (chase.Y - ship.Y) / 6f));
                }
                command.Grapple = sim.BoardingTarget(ship) != null;
                if (command.Grapple) encounter.MarkUsed("grapple");
            }

            // Nothing is worth flying into a whale's mouth for.
            if (sim.Field != null) DogfightAi.FleeWhale(sim.Field, ship, ref command);
            return command;
        }
    }
}
