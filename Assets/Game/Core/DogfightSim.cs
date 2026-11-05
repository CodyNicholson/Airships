using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public class ShipLoadout
    {
        public float Hull;
        public float Armor;
        public float Speed;
        public float TurnDegrees;
        public float Radius;
        public CannonDef Torpedo; // Null on ships that don't carry any.
        public CannonDef ChaseGuns; // Enemy ships' forward guns. The player's ships have torpedoes instead.
        public float ChaseDamageScale = 1f;
        public CannonDef Broadside;
        public float TorpedoDamageScale = 1f;
        public float BroadsideDamageScale = 1f;
        public SignatureDef Signature;
    }

    // One tick of input for one ship. Both the player's touch controls, the AI, and later a network
    // client produce this same structure, so the simulation never knows who is flying.
    public struct ShipCommand
    {
        public float Turn;     // -1 left .. 1 right
        public float Throttle; // 0 .. 1
        public float Climb;    // -1 dive .. 1 climb
        public bool FireTorpedo;
        public bool FireChaseGuns;
        public bool EnginesStopped; // The ship hangs in the air. Turn still turns it, and Climb looks up or down.
        public bool FireBroadside;
        public bool Signature;
        public bool Grapple;
        public bool Patch;
        public float Evade; // -1 rolls left, 1 rolls right.
        public float EvadeLift; // -1 rolls down, 1 rolls up. Takes priority over Evade.
    }

    public enum ShipPart { Hull, Engines, Guns, Envelope }

    // Standard is Free Skies. In the campaign, only a chapter's main enemy can be boarded.
    public enum BoardingRule { Standard, Main, Never }

    public class ShipState
    {
        public int Id;
        public int Team;
        public ShipLoadout Loadout;
        public float X;
        public float Y; // Altitude.
        public float Z;
        public float Heading; // Radians, 0 faces +Z, positive turns toward +X.
        public float Speed;
        public float ClimbRate; // Meters per second, positive climbing.
        public float TurnRate; // Radians per second this tick, used for visual banking.
        public float Hull;
        public float TorpedoCooldown;
        public float ChaseCooldown;
        public float TornadoDrag; // 1 while a tornado carries the ship up, -1 down, until it reaches the end of the sky.
        public float LookPitch; // Radians up from level, while the engines are stopped.
        public float BroadsideCooldown;
        public float CollisionCooldown; // Grace period after a crash, so one rock can't hit every tick.
        public float StunTimer; // Seconds left hanging dead in the air after a jellyfish's shock.
        public bool Stunned => StunTimer > 0f;
        public bool Eaten;
        public bool HuntedByWhale = true; // False for AI enemies, which the whale steers around instead.

        public float Engines = 1f;
        public float Guns = 1f;
        public float MaxSubsystem = 1f;
        public float Envelope = 1f;
        public float EvadeTimer;
        public float EvadeSide;
        public bool DriftsWhenHurt; // The player's ship pulls left or right once it's badly damaged.
        public float DriftSide; // -1 left, 1 right, 0 while it's flying true.
        public float DriftTimer; // Time left in this pull, or until the next one.
        public float DriftSeconds; // The length of this pull.
        public float EvadeLift;
        public float EvadeCooldown;
        public float ImmuneTimer;
        public AmmoDef Ammo;
        public float HullFloor; // Above zero, this ship can be crippled but never sunk.
        public bool AltitudeLocked;
        public bool Departed; // Fled the fight or landed its boarders, so it no longer takes part.
        public bool Hidden; // Inside a cloud bank, where enemies can't find it.
        public bool BoardsOnContact; // An assault glider.
        public BoardingRule Boarding;
        public bool WeakPoints; // This ship's shots find marked weak points.
        public int HooksRequired = 1;
        public int Hooks;
        public float GrappleCooldown;
        public int TetheredTo = -1;
        public float TetherTimer;
        public bool TetherDrags;
        public float SignatureCooldown;
        public float BoostTimer;
        public float BoostScale = 1f;
        public bool Ramming;
        public float BurnTimer;
        public float BurnPerSecond;
        public float DeckDamage;
        public int PatchesLeft;

        public bool Alive => Hull > 0f && !Departed;
        public bool EnginesOut => Engines <= 0f;
        public bool GunsOut => Guns <= 0f;
        public bool EnvelopeOut => Envelope <= 0f;

        public void TakeDamage(float amount) => Hull = Math.Max(HullFloor, Hull - amount);

        public float ForwardX => (float)Math.Sin(Heading);
        public float ForwardZ => (float)Math.Cos(Heading);
    }

    public class ProjectileState
    {
        public int Id;
        public int OwnerId;
        public int Team;
        public float X;
        public float Y;
        public float Z;
        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;
        public float Damage;
        public float Remaining;
        public bool Heavy;
        public float EngineDamage;
        public float GunDamage;
        public float DeckDamage;
        public float BurnSeconds;
        public float BurnPerSecond;
        public float HitRadiusBonus;
        public bool FindsWeakPoints;
        public float EnvelopeDamage;
        public string AmmoId;
        public int HomingOn = -1; // A missile's or torpedo's target.
        public float HomingSpeed;
        public float HomingTurn;
    }

    public enum DogfightEventKind { Fired, Hit, Missed, Destroyed, HazardHit, HazardBroken, ShotBlocked, ShipBump, WhaleAlert, WhaleLunge, Eaten, PartDisabled, HookLanded, HooksSnapped, Boarded, Tethered, SignatureFired, BoarderLanded, Departed, Patched, Crushing, LightningWarning, LightningStrike, GliderLaunched, Evaded, DamageDrift, TornadoCaught, VolleyWarning, Raked, MissileLaunched, MineBlast, PickupTaken, WaveArrived }

    public struct DogfightEvent
    {
        public DogfightEventKind Kind;
        public int ShipId;
        public int ProjectileId;
        public int HazardId;
        public HazardKind HazardKind;
        public float X;
        public float Y;
        public float Z;
        public float Amount;
        public bool Heavy;
        public ShipPart Part;
        public string AmmoId;
    }

    // Dogfight rules with no Unity dependency. Fixed-step and driven only by commands and a seed, so
    // a server can run the same rules and decide the outcome for online matches.
    public class DogfightSim
    {
        public const int TicksPerSecond = 30;
        public const float TickSeconds = 1f / TicksPerSecond;
        public const float MinimumDamageShare = 0.2f;
        public const float ArenaRadius = 300f;
        public const float MinAltitude = -45f;
        public const float MaxAltitude = 65f;
        public const float ClimbSpeed = 18f;
        public const float MaxLookPitch = 0.6f;
        const float LookRate = 0.9f;
        const float StopDeceleration = 60f;
        public const int NoWinner = -2;
        const float ProjectileRadius = 1f;
        const float HullHalfHeight = 6f;
        const float MinimumThrottle = 0.35f;
        const float Acceleration = 16f;
        const float ClimbAcceleration = 26f;
        const float ClimbSpeedEffect = 0.25f; // Climbing bleeds speed, and diving gains it.
        const float BoundaryTurnStrength = 2.2f;
        const float BumpDistanceShare = 1.4f;
        const float BumpDamage = 12f;
        const float BumpCooldown = 1f;
        const float TorpedoLaunchMargin = 1.25f; // Always leaves its own ship behind, even under full burn.

        readonly List<ShipState> ships = new List<ShipState>();
        readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        readonly List<DogfightEvent> events = new List<DogfightEvent>();

        // Things announced between steps, delivered with the next step's events.
        readonly List<DogfightEvent> announced = new List<DogfightEvent>();
        readonly GameRandom random;
        int nextProjectileId = 1;

        public IReadOnlyList<ShipState> Ships => ships;
        public IReadOnlyList<ProjectileState> Projectiles => projectiles;
        public IReadOnlyList<DogfightEvent> Events => events;
        public int Tick { get; private set; }
        public int WinningTeam { get; private set; } = -1;
        public bool Finished => WinningTeam != -1;
        public HazardField Field { get; private set; }
        public SkyWeather Weather { get; private set; }
        public SkyRulesDef Rules { get; }
        public int BoardedShipId { get; private set; } = -1;
        public int BoardersLanded { get; private set; }

        // Set when a mission decides for itself when the fight is over.
        public bool HoldOpen { get; set; }

        public DogfightSim(int seed, SkyRulesDef rules = null)
        {
            random = new GameRandom(seed);
            Rules = rules;
        }

        public ShipState AddShip(int team, ShipLoadout loadout, float x, float z, float heading)
        {
            var ship = new ShipState
            {
                Id = ships.Count,
                Team = team,
                Loadout = loadout,
                X = x,
                Z = z,
                Heading = heading,
                Speed = loadout.Speed * 0.75f,
                Hull = loadout.Hull,
            };
            if (Rules != null)
            {
                ship.MaxSubsystem = loadout.Hull * Rules.subsystemHullShare;
                ship.Engines = ship.MaxSubsystem;
                ship.Guns = ship.MaxSubsystem;
                ship.Envelope = ship.MaxSubsystem;
            }
            ships.Add(ship);
            return ship;
        }

        public void SetWeather(SkyWeather weather) => Weather = weather;

        // Call after adding ships, so nothing spawns on top of them.
        public HazardField AddHazards(HazardDef def, int tier, int seed)
        {
            Field = new HazardField(def, tier, seed, ships);
            return Field;
        }

        public static float ClampAltitude(float y) => Clamp(y, MinAltitude, MaxAltitude);

        public ShipState Opponent(ShipState ship)
        {
            ShipState best = null;
            var bestDistance = float.MaxValue;
            foreach (var other in ships)
            {
                if (other.Team == ship.Team || !other.Alive || other.Hidden) continue;
                var distance = DistanceSquared(ship.X, ship.Z, other.X, other.Z);
                if (distance < bestDistance)
                {
                    best = other;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // commands is indexed by ship id. Events are valid until the next Step.
        public void Step(IReadOnlyList<ShipCommand> commands)
        {
            events.Clear();
            events.AddRange(announced);
            announced.Clear();
            if (Finished) return;
            Tick++;
            for (var i = 0; i < ships.Count; i++)
            {
                if (ships[i].Alive) StepShip(ships[i], commands[i]);
            }
            Field?.Step(ships, events);
            Weather?.Step(this, events);
            StepTethers();
            BumpShips();
            StepProjectiles();
            CheckFinished();
        }

        // Ships that can be boarded never sink: they're beaten down to this last point of hull, and only
        // then can hooks take them.
        public const float BoardableHull = 1f;

        // A hook or harpoon can land only on a beaten ship, in range or already joined by a line.
        public bool CanBoard(ShipState ship, ShipState target)
        {
            if (Rules == null || target == null || !target.Alive || target.Team == ship.Team || target.BoardsOnContact) return false;
            if (!IsBoardable(target)) return false;
            var linked = target.TetheredTo == ship.Id || ship.TetheredTo == target.Id;
            return linked || InGrappleRange(ship, target);
        }

        public bool IsBoardable(ShipState target)
        {
            if (Rules == null || target.Boarding == BoardingRule.Never) return false;
            return target.Hull <= BoardableHull;
        }

        public bool InGrappleRange(ShipState ship, ShipState target)
        {
            return DistanceSquared(ship.X, ship.Z, target.X, target.Z) <= Rules.grappleRange * Rules.grappleRange
                && Math.Abs(ship.Y - target.Y) <= Rules.grappleAltitude;
        }

        public ShipState BoardingTarget(ShipState ship)
        {
            ShipState best = null;
            var bestDistance = float.MaxValue;
            foreach (var other in ships)
            {
                if (!CanBoard(ship, other)) continue;
                var distance = DistanceSquared(ship.X, ship.Z, other.X, other.Z);
                if (distance >= bestDistance) continue;
                best = other;
                bestDistance = distance;
            }
            return best;
        }

        void TryGrapple(ShipState ship)
        {
            if (ship.GrappleCooldown > 0f) return;
            var target = BoardingTarget(ship);
            if (target == null) return;
            ship.GrappleCooldown = Rules.grappleCooldown;
            target.Hooks++;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.HookLanded, ShipId = target.Id, X = target.X, Y = target.Y, Z = target.Z, Amount = target.Hooks });
            var tethered = ship.TetheredTo == target.Id || target.TetheredTo == ship.Id;
            if (!tethered && target.Hooks < target.HooksRequired) return;
            BoardedShipId = target.Id;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.Boarded, ShipId = target.Id, X = target.X, Y = target.Y, Z = target.Z });
        }

        // Hooks hold only while the ships stay close. Tethers reel ships together, and an anchor drags
        // both of them down until the lower one is crushed against the bottom of the sky.
        void StepTethers()
        {
            if (Rules == null) return;
            foreach (var ship in ships)
            {
                if (!ship.Alive) continue;
                if (ship.Hooks > 0)
                {
                    var limit = Rules.grappleRange * Rules.snapRangeScale;
                    var near = false;
                    foreach (var other in ships)
                    {
                        if (other.Team != ship.Team && other.Alive && DistanceSquared(ship.X, ship.Z, other.X, other.Z) <= limit * limit) near = true;
                    }
                    if (!near)
                    {
                        ship.Hooks = 0;
                        events.Add(new DogfightEvent { Kind = DogfightEventKind.HooksSnapped, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
                    }
                }

                if (ship.TetheredTo < 0) continue;
                var victim = ships[ship.TetheredTo];
                if (!ship.TetherDrags) ship.TetherTimer -= TickSeconds;
                if (!victim.Alive || (!ship.TetherDrags && ship.TetherTimer <= 0f))
                {
                    ship.TetheredTo = -1;
                    continue;
                }
                var dx = victim.X - ship.X;
                var dz = victim.Z - ship.Z;
                var distance = Math.Max(0.01f, (float)Math.Sqrt(dx * dx + dz * dz));
                var reach = (ship.Loadout.Radius + victim.Loadout.Radius) * 1.2f;
                if (distance > reach)
                {
                    victim.X -= dx / distance * Rules.reelSpeed * TickSeconds;
                    victim.Z -= dz / distance * Rules.reelSpeed * TickSeconds;
                }
                if (!ship.TetherDrags) continue;
                ship.Y = ClampAltitude(ship.Y - Rules.tetherSinkSpeed * TickSeconds);
                victim.Y = ClampAltitude(victim.Y - Rules.tetherSinkSpeed * TickSeconds);
                if (victim.Y > MinAltitude + 1f) continue;
                victim.TakeDamage(Rules.crushPerSecond * TickSeconds);
                if (Tick % TicksPerSecond == 0) events.Add(new DogfightEvent { Kind = DogfightEventKind.Crushing, ShipId = victim.Id, X = victim.X, Y = victim.Y, Z = victim.Z, Amount = Rules.crushPerSecond });
                if (!victim.Alive) events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = victim.Id, X = victim.X, Y = victim.Y, Z = victim.Z });
            }
        }

        void UseSignature(ShipState ship)
        {
            var signature = ship.Loadout.Signature;
            if (signature == null || ship.SignatureCooldown > 0f) return;
            switch (signature.kind)
            {
                case "harpoon":
                    var target = Opponent(ship);
                    if (target == null || DistanceSquared(ship.X, ship.Z, target.X, target.Z) > signature.range * signature.range) return;
                    ship.TetheredTo = target.Id;
                    ship.TetherTimer = signature.seconds;
                    events.Add(new DogfightEvent { Kind = DogfightEventKind.Tethered, ShipId = target.Id, X = target.X, Y = target.Y, Z = target.Z });
                    break;
                case "afterburner":
                    ship.BoostTimer = signature.seconds;
                    ship.BoostScale = signature.power;
                    break;
                case "ram":
                    ship.BoostTimer = signature.seconds;
                    ship.BoostScale = signature.power;
                    ship.Ramming = true;
                    break;
                case "thunder":
                    ship.BroadsideCooldown = 0f;
                    FireBroadside(ship, signature.power);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown signature kind '{signature.kind}'");
            }
            ship.SignatureCooldown = signature.cooldown;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.SignatureFired, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
        }

        void UseCrewAbilities(ShipState ship, ShipCommand command)
        {
            if (command.Patch && ship.PatchesLeft > 0 && ship.Hull < ship.Loadout.Hull)
            {
                ship.PatchesLeft--;
                var amount = ship.Loadout.Hull * Rules.patchShare;
                ship.Hull = Math.Min(ship.Loadout.Hull, ship.Hull + amount);
                events.Add(new DogfightEvent { Kind = DogfightEventKind.Patched, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z, Amount = amount });
            }
        }

        // Below a share of its hull, a ship pulls to one side at random moments. Returns how hard it's
        // pulling right now, from -1 to 1, swelling and easing off so it feels like a hurt ship.
        float UpdateDamageDrift(ShipState ship)
        {
            if (Rules == null || !ship.DriftsWhenHurt) return 0f;
            if (ship.Hull >= ship.Loadout.Hull * Rules.damagedDriftHullShare)
            {
                ship.DriftSide = 0f;
                ship.DriftTimer = 0f;
                return 0f;
            }

            ship.DriftTimer -= TickSeconds;
            if (ship.DriftTimer <= 0f)
            {
                if (ship.DriftSide != 0f)
                {
                    ship.DriftSide = 0f;
                    ship.DriftTimer = random.Range(Rules.damagedDriftGapMin, Rules.damagedDriftGapMax);
                }
                else
                {
                    ship.DriftSide = random.Value() < 0.5f ? -1f : 1f;
                    ship.DriftSeconds = random.Range(Rules.damagedDriftSecondsMin, Rules.damagedDriftSecondsMax);
                    ship.DriftTimer = ship.DriftSeconds;
                    events.Add(new DogfightEvent { Kind = DogfightEventKind.DamageDrift, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
                }
            }
            if (ship.DriftSide == 0f) return 0f;
            return ship.DriftSide * (float)Math.Sin(Math.PI * (1f - ship.DriftTimer / ship.DriftSeconds));
        }

        void UpdateEvade(ShipState ship, ShipCommand command)
        {
            ship.EvadeCooldown = Math.Max(0f, ship.EvadeCooldown - TickSeconds);
            ship.ImmuneTimer = Math.Max(0f, ship.ImmuneTimer - TickSeconds);
            if ((command.Evade != 0f || command.EvadeLift != 0f) && ship.EvadeCooldown <= 0f && ship.EvadeTimer <= 0f && !ship.Stunned)
            {
                // A ship that can't climb right now rolls sideways instead of doing nothing.
                var cantLift = ship.AltitudeLocked || (ship.EnvelopeOut && command.EvadeLift > 0f);
                ship.EvadeLift = cantLift ? 0f : Math.Sign(command.EvadeLift);
                ship.EvadeSide = ship.EvadeLift != 0f ? 0f : command.Evade != 0f ? Math.Sign(command.Evade) : 1f;
                ship.EvadeTimer = Rules.evadeSeconds;
                ship.EvadeCooldown = Rules.evadeCooldown;
                ship.ImmuneTimer = Rules.evadeImmunity;
                events.Add(new DogfightEvent { Kind = DogfightEventKind.Evaded, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
            }
            if (ship.EvadeTimer <= 0f) return;

            // A roll throws the ship off the line of whatever was aimed at it: sideways, or up or down.
            ship.EvadeTimer -= TickSeconds;
            ship.X += ship.ForwardZ * ship.EvadeSide * Rules.evadeSpeed * TickSeconds;
            ship.Z -= ship.ForwardX * ship.EvadeSide * Rules.evadeSpeed * TickSeconds;
            ship.Y = ClampAltitude(ship.Y + ship.EvadeLift * Rules.evadeSpeed * TickSeconds);
        }

        // Which way a roll goes for a stick position: whichever way the stick is pushed furthest.
        public static void RollDirection(float turn, float climb, out float side, out float lift)
        {
            lift = climb != 0f && Math.Abs(climb) >= Math.Abs(turn) ? Math.Sign(climb) : 0f;
            side = lift != 0f ? 0f : turn < -0.1f ? -1f : 1f;
        }

        // Announced first, so a player has time to roll out of the way.
        public void WarnVolley(ShipState ship)
        {
            announced.Add(new DogfightEvent { Kind = DogfightEventKind.VolleyWarning, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
        }

        // A homing missile. It steers for a while, then runs out of fuel.
        public void LaunchMissile(ShipState ship, ShipState target)
        {
            var angle = (float)Math.Atan2(target.X - ship.X, target.Z - ship.Z);
            var projectile = new ProjectileState
            {
                Id = nextProjectileId++,
                OwnerId = ship.Id,
                Team = ship.Team,
                X = ship.X,
                Y = ship.Y,
                Z = ship.Z,
                VelocityX = (float)Math.Sin(angle) * Rules.missileSpeed,
                VelocityZ = (float)Math.Cos(angle) * Rules.missileSpeed,
                Damage = Rules.missileDamage * ship.Loadout.BroadsideDamageScale,
                Remaining = Rules.missileSeconds,
                Heavy = true,
                AmmoId = "missile",
                HomingOn = target.Id,
                HomingSpeed = Rules.missileSpeed,
                HomingTurn = Rules.missileTurn,
            };
            projectiles.Add(projectile);
            announced.Add(new DogfightEvent { Kind = DogfightEventKind.MissileLaunched, ShipId = ship.Id, ProjectileId = projectile.Id, X = ship.X, Y = ship.Y, Z = ship.Z, Heavy = true, AmmoId = "missile" });
        }

        void SteerMissile(ProjectileState projectile)
        {
            var target = ships[projectile.HomingOn];
            if (!target.Alive) return;
            var dx = target.X - projectile.X;
            var dy = target.Y - projectile.Y;
            var dz = target.Z - projectile.Z;
            var length = Math.Max(0.01f, (float)Math.Sqrt(dx * dx + dy * dy + dz * dz));
            var blend = Math.Min(1f, projectile.HomingTurn * TickSeconds);
            var speed = projectile.HomingSpeed;
            projectile.VelocityX += (dx / length * speed - projectile.VelocityX) * blend;
            projectile.VelocityY += (dy / length * speed - projectile.VelocityY) * blend;
            projectile.VelocityZ += (dz / length * speed - projectile.VelocityZ) * blend;
        }

        float SpeedScale(ShipState ship)
        {
            if (Rules == null) return 1f;
            var scale = ship.EnginesOut ? Rules.disabledSpeedScale : 1f;
            if (ship.Hooks > 0) scale *= Rules.hookedSpeedScale;
            if (ship.BoostTimer > 0f) scale *= ship.BoostScale;
            if (Weather != null) scale *= Weather.WindScale(ship, Rules.windSpeedScale);
            return scale;
        }

        float ReloadScale(ShipState ship)
        {
            if (Rules == null) return 1f;
            var scale = ship.GunsOut ? Rules.disabledReloadScale : 1f;
            return scale;
        }

        // Launches an assault glider mid-fight. Every list indexed by ship id grows with it.
        public ShipState LaunchGlider(ShipState from, ShipLoadout loadout)
        {
            var glider = AddShip(from.Team, loadout, from.X, from.Z, from.Heading);
            glider.Y = from.Y;
            glider.BoardsOnContact = true;
            glider.HuntedByWhale = false;
            glider.AltitudeLocked = from.AltitudeLocked;
            announced.Add(new DogfightEvent { Kind = DogfightEventKind.GliderLaunched, ShipId = glider.Id, X = from.X, Y = from.Y, Z = from.Z });
            return glider;
        }

        public void AnnouncePickup(int id, float x, float y, float z)
        {
            events.Add(new DogfightEvent { Kind = DogfightEventKind.PickupTaken, HazardId = id, X = x, Y = y, Z = z });
        }

        public void AnnounceWave(float x, float y, float z)
        {
            events.Add(new DogfightEvent { Kind = DogfightEventKind.WaveArrived, X = x, Y = y, Z = z });
        }

        public void Depart(ShipState ship)
        {
            ship.Departed = true;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.Departed, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
        }

        void Land(ShipState glider)
        {
            BoardersLanded++;
            glider.Departed = true;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.BoarderLanded, ShipId = glider.Id, X = glider.X, Y = glider.Y, Z = glider.Z });
        }

        // Ships can't overlap, and a collision scrapes both hulls.
        void BumpShips()
        {
            for (var a = 0; a < ships.Count; a++)
            {
                for (var b = a + 1; b < ships.Count; b++)
                {
                    var first = ships[a];
                    var second = ships[b];
                    if (!first.Alive || !second.Alive) continue;
                    var reach = (first.Loadout.Radius + second.Loadout.Radius) * BumpDistanceShare * 0.5f;
                    var dx = second.X - first.X;
                    var dy = second.Y - first.Y;
                    var dz = second.Z - first.Z;
                    var distanceSquared = dx * dx + dy * dy + dz * dz;
                    if (distanceSquared >= reach * reach) continue;

                    if (first.BoardsOnContact || second.BoardsOnContact)
                    {
                        if (first.Team != second.Team) Land(first.BoardsOnContact ? first : second);
                        continue;
                    }

                    var distance = Math.Max(0.01f, (float)Math.Sqrt(distanceSquared));
                    var push = (reach - distance) * 0.5f;
                    first.X -= dx / distance * push;
                    first.Z -= dz / distance * push;
                    first.Y = ClampAltitude(first.Y - dy / distance * push);
                    second.X += dx / distance * push;
                    second.Z += dz / distance * push;
                    second.Y = ClampAltitude(second.Y + dy / distance * push);

                    if (first.CollisionCooldown > 0f || second.CollisionCooldown > 0f) continue;
                    first.CollisionCooldown = BumpCooldown;
                    second.CollisionCooldown = BumpCooldown;
                    first.TakeDamage(BumpDamageTo(first, second));
                    second.TakeDamage(BumpDamageTo(second, first));
                    events.Add(new DogfightEvent { Kind = DogfightEventKind.ShipBump, ShipId = first.Id, X = first.X + dx * 0.5f, Y = first.Y + dy * 0.5f, Z = first.Z + dz * 0.5f, Amount = BumpDamage });
                    foreach (var ship in new[] { first, second })
                    {
                        if (!ship.Alive) events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
                    }
                }
            }
        }

        // A ramming prow punishes what it hits and shrugs off most of the blow.
        static float BumpDamageTo(ShipState ship, ShipState other)
        {
            if (other.Ramming && other.Loadout.Signature != null) return BumpDamage * other.Loadout.Signature.range;
            return ship.Ramming ? BumpDamage * 0.2f : BumpDamage;
        }

        void StepShip(ShipState ship, ShipCommand command)
        {
            var loadout = ship.Loadout;

            // A shocked ship can't steer, climb or fly until the current lets go of it.
            var stunned = ship.Stunned;
            if (stunned) ship.StunTimer = Math.Max(0f, ship.StunTimer - TickSeconds);

            var turn = Clamp(command.Turn, -1f, 1f) * loadout.TurnDegrees * DegreesToRadians;

            // Past the arena edge the wind pushes every ship back toward the center.
            var distanceFromCenter = (float)Math.Sqrt(ship.X * ship.X + ship.Z * ship.Z);
            if (distanceFromCenter > ArenaRadius)
            {
                var toCenter = (float)Math.Atan2(-ship.X, -ship.Z);
                var over = Clamp((distanceFromCenter - ArenaRadius) / 40f, 0f, 1f);
                turn += WrapAngle(toCenter - ship.Heading) * BoundaryTurnStrength * over;
            }

            var drift = stunned ? 0f : UpdateDamageDrift(ship);
            if (drift != 0f) turn += drift * Rules.damagedDriftTurnShare * loadout.TurnDegrees * DegreesToRadians;
            if (stunned) turn = 0f;

            ship.TurnRate = turn;
            ship.Heading = WrapAngle(ship.Heading + turn * TickSeconds);
            if (drift != 0f)
            {
                ship.X += ship.ForwardZ * drift * Rules.damagedDriftSlip * TickSeconds;
                ship.Z -= ship.ForwardX * drift * Rules.damagedDriftSlip * TickSeconds;
            }

            var climb = stunned || ship.AltitudeLocked || ship.TornadoDrag != 0f || command.EnginesStopped ? 0f : Clamp(command.Climb, -1f, 1f);
            var lookGoal = command.EnginesStopped ? ship.LookPitch + Clamp(command.Climb, -1f, 1f) * LookRate * TickSeconds : 0f;
            ship.LookPitch = command.EnginesStopped ? Clamp(lookGoal, -MaxLookPitch, MaxLookPitch) : MoveTowards(ship.LookPitch, 0f, LookRate * 2f * TickSeconds);
            if (Rules != null && ship.EnvelopeOut) climb = -Rules.envelopeSinkSpeed / ClimbSpeed;
            ship.ClimbRate = stunned ? 0f : MoveTowards(ship.ClimbRate, climb * ClimbSpeed, ClimbAcceleration * TickSeconds);
            var altitude = ship.Y + ship.ClimbRate * TickSeconds;
            ship.Y = ClampAltitude(altitude);
            if (ship.Y != altitude) ship.ClimbRate = 0f;

            var throttle = Clamp(command.Throttle, MinimumThrottle, 1f);
            var climbEffect = 1f - ClimbSpeedEffect * ship.ClimbRate / ClimbSpeed;
            var speedAcceleration = ship.BoostTimer > 0f ? Acceleration * 3f : Acceleration;
            var targetSpeed = command.EnginesStopped ? 0f : loadout.Speed * throttle * climbEffect * SpeedScale(ship);
            ship.Speed = stunned ? 0f : MoveTowards(ship.Speed, targetSpeed, (command.EnginesStopped ? StopDeceleration : speedAcceleration) * TickSeconds);
            ship.X += ship.ForwardX * ship.Speed * TickSeconds;
            ship.Z += ship.ForwardZ * ship.Speed * TickSeconds;

            ship.TorpedoCooldown = Math.Max(0f, ship.TorpedoCooldown - TickSeconds);
            ship.ChaseCooldown = Math.Max(0f, ship.ChaseCooldown - TickSeconds);
            ship.BroadsideCooldown = Math.Max(0f, ship.BroadsideCooldown - TickSeconds);
            ship.CollisionCooldown = Math.Max(0f, ship.CollisionCooldown - TickSeconds);
            ship.GrappleCooldown = Math.Max(0f, ship.GrappleCooldown - TickSeconds);
            ship.SignatureCooldown = Math.Max(0f, ship.SignatureCooldown - TickSeconds);
            if (ship.BoostTimer > 0f)
            {
                ship.BoostTimer -= TickSeconds;
                if (ship.BoostTimer <= 0f) ship.Ramming = false;
            }
            if (ship.BurnTimer > 0f)
            {
                ship.BurnTimer -= TickSeconds;
                ship.TakeDamage(ship.BurnPerSecond * TickSeconds);
                if (!ship.Alive)
                {
                    events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
                    return;
                }
            }

            // The shock locks up the gun crews too: nothing fires until the ship can fly again.
            if (stunned)
            {
                command.FireTorpedo = false;
                command.FireChaseGuns = false;
                command.FireBroadside = false;
                command.Signature = false;
                command.Grapple = false;
            }

            if (command.FireTorpedo && ship.Loadout.Torpedo != null && ship.TorpedoCooldown <= 0f)
            {
                ship.TorpedoCooldown = ship.Loadout.Torpedo.reload * ReloadScale(ship);
                FireForward(ship, ship.Loadout.Torpedo, ship.Loadout.TorpedoDamageScale);
            }
            if (command.FireChaseGuns && ship.Loadout.ChaseGuns != null && ship.ChaseCooldown <= 0f)
            {
                ship.ChaseCooldown = ship.Loadout.ChaseGuns.reload * ReloadScale(ship);
                FireForward(ship, ship.Loadout.ChaseGuns, ship.Loadout.ChaseDamageScale);
            }
            if (command.FireBroadside && ship.BroadsideCooldown <= 0f) FireBroadside(ship);
            if (Rules == null) return;
            UpdateEvade(ship, command);
            if (command.Signature) UseSignature(ship);
            if (command.Grapple) TryGrapple(ship);
            UseCrewAbilities(ship, command);
        }

        void FireForward(ShipState ship, CannonDef cannon, float damageScale)
        {
            var aim = ship.Heading;
            var locked = -1;

            // Gentle aim assist so firing works with thumbs: shots curve onto a target near the nose.
            var target = Opponent(ship);
            if (target != null)
            {
                var toTarget = LeadAngle(ship, target, cannon.projectileSpeed);
                if (Math.Abs(WrapAngle(toTarget - ship.Heading)) <= cannon.aimAssistDegrees * DegreesToRadians)
                {
                    aim = toTarget;
                    locked = target.Id;
                }
            }
            // A stopped ship looking up or down fires where it's looking, unless it has a lock.
            var pitch = locked < 0 && ship.LookPitch != 0f ? ship.LookPitch : PitchToward(ship, target, cannon);
            FireVolley(ship, cannon, aim, pitch, damageScale, 0f, false, locked);
        }

        // Guns angle toward a target's altitude only within their elevation limit. Beyond it they fire
        // level, so getting far enough above or below a ship puts you out of its guns' reach.
        static float PitchToward(ShipState ship, ShipState target, CannonDef cannon, bool ignoresElevation = false)
        {
            if (target == null) return 0f;
            var distance = (float)Math.Sqrt(DistanceSquared(ship.X, ship.Z, target.X, target.Z));
            var travelTime = distance / Math.Max(1f, cannon.projectileSpeed);
            var pitch = (float)Math.Atan2(target.Y + target.ClimbRate * travelTime - ship.Y, Math.Max(1f, distance));
            return ignoresElevation || Math.Abs(pitch) <= cannon.elevationDegrees * DegreesToRadians ? pitch : 0f;
        }

        // Whether a gun can angle far enough to reach a target at its current altitude.
        public static bool CanElevate(ShipState ship, ShipState target, CannonDef cannon)
        {
            var distance = (float)Math.Sqrt(DistanceSquared(ship.X, ship.Z, target.X, target.Z));
            var pitch = (float)Math.Atan2(target.Y - ship.Y, Math.Max(1f, distance));
            return Math.Abs(pitch) <= cannon.elevationDegrees * DegreesToRadians;
        }

        void FireBroadside(ShipState ship, float extraDamage = 1f)
        {
            var cannon = ship.Loadout.Broadside;
            var ammo = ship.Ammo;
            ship.BroadsideCooldown = cannon.reload * ReloadScale(ship) * (ammo != null ? ammo.reloadScale : 1f);
            var target = Opponent(ship);

            // Fire from whichever side faces the target, and fan the volley toward it.
            var side = 1f;
            if (target != null)
            {
                var relative = WrapAngle((float)Math.Atan2(target.X - ship.X, target.Z - ship.Z) - ship.Heading);
                side = relative >= 0f ? 1f : -1f;
            }
            var aim = WrapAngle(ship.Heading + side * (float)Math.PI * 0.5f);
            if (target != null)
            {
                var toTarget = LeadAngle(ship, target, cannon.projectileSpeed);
                if (Math.Abs(WrapAngle(toTarget - aim)) <= cannon.aimAssistDegrees * DegreesToRadians)
                {
                    aim = toTarget;
                }
            }
            FireVolley(ship, cannon, aim, PitchToward(ship, target, cannon, ammo != null && ammo.ignoresElevation), ship.Loadout.BroadsideDamageScale * extraDamage, side, true);
        }

        void FireVolley(ShipState ship, CannonDef cannon, float aim, float pitch, float damageScale, float side, bool heavy, int locked = -1)
        {
            var count = Math.Max(1, cannon.shots);
            var torpedo = !string.IsNullOrEmpty(cannon.ammoId);
            for (var i = 0; i < count; i++)
            {
                var fraction = count == 1 ? 0f : i / (float)(count - 1) - 0.5f;
                var angle = aim + (fraction + random.Range(-0.12f, 0.12f)) * cannon.spreadDegrees * DegreesToRadians;
                var speed = torpedo ? Math.Max(cannon.projectileSpeed, ship.Speed * TorpedoLaunchMargin) : cannon.projectileSpeed;
                var flatSpeed = speed * (float)Math.Cos(pitch);

                // Broadside guns sit along the hull; chase guns sit at the front.
                var alongHull = heavy ? fraction * ship.Loadout.Radius * 1.4f : ship.Loadout.Radius;
                var acrossHull = heavy ? side * ship.Loadout.Radius * 0.5f : 0f;
                var startX = ship.X + ship.ForwardX * alongHull + ship.ForwardZ * acrossHull;
                var startZ = ship.Z + ship.ForwardZ * alongHull - ship.ForwardX * acrossHull;

                var projectile = new ProjectileState
                {
                    Id = nextProjectileId++,
                    OwnerId = ship.Id,
                    Team = ship.Team,
                    X = startX,
                    Y = ship.Y,
                    Z = startZ,
                    VelocityX = (float)Math.Sin(angle) * flatSpeed,
                    VelocityY = (float)Math.Sin(pitch) * speed,
                    VelocityZ = (float)Math.Cos(angle) * flatSpeed,
                    Damage = cannon.damage * damageScale,
                    Remaining = cannon.range / speed,
                    Heavy = heavy,
                    FindsWeakPoints = ship.WeakPoints,
                };
                ApplyShotType(ship, projectile, heavy);
                if (torpedo)
                {
                    projectile.Heavy = true;
                    projectile.AmmoId = cannon.ammoId;
                    projectile.HitRadiusBonus = cannon.hitRadiusBonus;
                    projectile.HomingOn = locked;
                    projectile.HomingSpeed = speed;
                    projectile.HomingTurn = cannon.homingTurn;
                }
                projectiles.Add(projectile);
                events.Add(new DogfightEvent
                {
                    Kind = DogfightEventKind.Fired,
                    ShipId = ship.Id,
                    ProjectileId = projectile.Id,
                    X = startX,
                    Y = ship.Y,
                    Z = startZ,
                    Heavy = projectile.Heavy,
                    AmmoId = projectile.AmmoId,
                });
            }
        }

        // Sets what a shot does to each part of its target, from the ship's aim and ammunition.
        void ApplyShotType(ShipState ship, ProjectileState projectile, bool heavy)
        {
            if (Rules == null) return;
            var damage = projectile.Damage;
            var ammo = heavy ? ship.Ammo : null;
            var hull = ammo != null ? ammo.hullScale : 1f;
            var engines = ammo != null ? ammo.engineScale : 0f;
            var guns = ammo != null ? ammo.gunScale : 0f;
            var envelope = ammo != null ? ammo.envelopeScale : 0f;
            projectile.AmmoId = ammo?.id;
            if (ship.Hidden && heavy) hull *= Rules.ambushScale;
            projectile.Damage = damage * hull;
            projectile.EngineDamage = damage * engines;
            projectile.GunDamage = damage * guns;
            projectile.EnvelopeDamage = damage * envelope;
            if (ammo == null) return;
            projectile.DeckDamage = damage * ammo.deckScale;
            projectile.BurnSeconds = ammo.burnSeconds;
            projectile.BurnPerSecond = ammo.burnPerSecond;
            projectile.HitRadiusBonus = ammo.hitRadiusBonus;
        }

        void StepProjectiles()
        {
            for (var i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                if (projectile.HomingOn >= 0) SteerMissile(projectile);
                projectile.X += projectile.VelocityX * TickSeconds;
                projectile.Y += projectile.VelocityY * TickSeconds;
                projectile.Z += projectile.VelocityZ * TickSeconds;
                projectile.Remaining -= TickSeconds;

                var hit = FindHit(projectile);
                if (hit != null)
                {
                    ApplyHit(projectile, hit);
                    projectiles.RemoveAt(i);
                }
                else if (Field != null && Field.BlocksShot(projectile, events))
                {
                    projectiles.RemoveAt(i);
                }
                else if (projectile.Remaining <= 0f)
                {
                    events.Add(new DogfightEvent
                    {
                        Kind = DogfightEventKind.Missed,
                        ProjectileId = projectile.Id,
                        X = projectile.X,
                        Y = projectile.Y,
                        Z = projectile.Z,
                        Heavy = projectile.Heavy,
                    });
                    projectiles.RemoveAt(i);
                }
            }
        }

        ShipState FindHit(ProjectileState projectile)
        {
            foreach (var ship in ships)
            {
                if (!ship.Alive || ship.Team == projectile.Team || ship.ImmuneTimer > 0f) continue;
                if (Math.Abs(projectile.Y - ship.Y) > HullHalfHeight + ProjectileRadius + projectile.HitRadiusBonus) continue;
                var radius = ship.Loadout.Radius + ProjectileRadius + projectile.HitRadiusBonus;
                if (DistanceSquared(projectile.X, projectile.Z, ship.X, ship.Z) <= radius * radius) return ship;
            }
            return null;
        }

        void ApplyHit(ProjectileState projectile, ShipState ship)
        {
            var raw = projectile.Damage;
            var stern = Rules != null && projectile.AmmoId != "missile" && IsStern(projectile, ship);
            if (projectile.FindsWeakPoints && stern) raw *= Rules.weakPointScale;
            if (stern && projectile.Heavy)
            {
                raw *= Rules.rakeScale;
                events.Add(new DogfightEvent { Kind = DogfightEventKind.Raked, ShipId = ship.Id, X = projectile.X, Y = projectile.Y, Z = projectile.Z });
            }
            var damage = DamageAfterArmor(raw, ship.Loadout.Armor);
            ship.TakeDamage(damage);
            DamagePart(ship, ShipPart.Engines, projectile.EngineDamage);
            DamagePart(ship, ShipPart.Guns, projectile.GunDamage);
            DamagePart(ship, ShipPart.Envelope, projectile.EnvelopeDamage);
            ship.DeckDamage += projectile.DeckDamage;
            if (projectile.BurnSeconds > 0f)
            {
                ship.BurnTimer = projectile.BurnSeconds;
                ship.BurnPerSecond = projectile.BurnPerSecond;
            }
            events.Add(new DogfightEvent
            {
                Kind = DogfightEventKind.Hit,
                ShipId = ship.Id,
                ProjectileId = projectile.Id,
                X = projectile.X,
                Y = projectile.Y,
                Z = projectile.Z,
                Amount = damage,
                Heavy = projectile.Heavy,
                AmmoId = projectile.AmmoId,
            });
            if (!ship.Alive)
            {
                events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
            }
        }

        // Whether a shot struck from behind the ship, where its weak points are.
        static bool IsStern(ProjectileState projectile, ShipState ship)
        {
            return (projectile.X - ship.X) * ship.ForwardX + (projectile.Z - ship.Z) * ship.ForwardZ < 0f;
        }

        void DamagePart(ShipState ship, ShipPart part, float amount)
        {
            if (amount <= 0f) return;
            var before = part == ShipPart.Engines ? ship.Engines : part == ShipPart.Guns ? ship.Guns : ship.Envelope;
            if (before <= 0f) return;
            var after = Math.Max(0f, before - amount);
            if (part == ShipPart.Engines) ship.Engines = after;
            else if (part == ShipPart.Guns) ship.Guns = after;
            else ship.Envelope = after;
            if (after <= 0f) events.Add(new DogfightEvent { Kind = DogfightEventKind.PartDisabled, ShipId = ship.Id, Part = part, X = ship.X, Y = ship.Y, Z = ship.Z });
        }

        void CheckFinished()
        {
            if (HoldOpen) return;
            var aliveTeam = -1;
            foreach (var ship in ships)
            {
                if (!ship.Alive) continue;
                if (aliveTeam >= 0 && aliveTeam != ship.Team) return;
                aliveTeam = ship.Team;
            }
            WinningTeam = aliveTeam >= 0 ? aliveTeam : NoWinner;
        }

        public static float DamageAfterArmor(float damage, float armor)
        {
            return Math.Max(damage * MinimumDamageShare, damage - armor);
        }

        // Angle to where the target will be when a shot of this speed reaches it.
        public static float LeadAngle(ShipState shooter, ShipState target, float projectileSpeed)
        {
            var distance = (float)Math.Sqrt(DistanceSquared(shooter.X, shooter.Z, target.X, target.Z));
            var travelTime = distance / Math.Max(1f, projectileSpeed);
            var futureX = target.X + target.ForwardX * target.Speed * travelTime;
            var futureZ = target.Z + target.ForwardZ * target.Speed * travelTime;
            return (float)Math.Atan2(futureX - shooter.X, futureZ - shooter.Z);
        }

        public static float WrapAngle(float radians)
        {
            const float tau = (float)(Math.PI * 2.0);
            var wrapped = radians % tau;
            if (wrapped > Math.PI) wrapped -= tau;
            else if (wrapped < -Math.PI) wrapped += tau;
            return wrapped;
        }

        public static float DistanceSquared(float ax, float az, float bx, float bz)
        {
            var dx = ax - bx;
            var dz = az - bz;
            return dx * dx + dz * dz;
        }

        const float DegreesToRadians = (float)(Math.PI / 180.0);
        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        static float MoveTowards(float value, float target, float maxDelta)
        {
            if (Math.Abs(target - value) <= maxDelta) return target;
            return value + Math.Sign(target - value) * maxDelta;
        }
    }

    // Flies a ship by producing the same commands a player would.
    public class DogfightAi
    {
        enum Phase { Attack, Broadside, BreakAway }

        const float DegreesToRadians = (float)(Math.PI / 180.0);
        const float DefaultBroadsideDistance = 62f;
        const float BreakAwayDistance = 120f;
        const float BroadsidePhaseSeconds = 4.5f;
        const float BreakAwayPhaseSeconds = 6f;
        const float AltitudeSwing = 30f;
        const float AttackAltitudeShare = 0.7f;
        const float TailedDistance = 90f;
        const float TailedAngle = 2.2f; // Radians from the nose, so the target is behind.
        const float WeaveSeconds = 1.4f;
        const float LookAheadSeconds = 2.4f;
        const float AvoidMargin = 14f;
        const float WhaleFleeRange = 230f;

        readonly GameRandom random;
        readonly float aimJitterDegrees;
        readonly float broadsideDistance;
        Phase phase;
        float phaseTime;
        float altitudeOffset;
        float jitter;
        float nextJitterAt;
        float time;

        public DogfightAi(int seed, float aimJitterDegrees, float broadsideDistance = 0f)
        {
            random = new GameRandom(seed);
            this.aimJitterDegrees = aimJitterDegrees;
            this.broadsideDistance = broadsideDistance > 0f ? broadsideDistance : DefaultBroadsideDistance;
        }

        public ShipCommand Think(DogfightSim sim, ShipState ship)
        {
            time += DogfightSim.TickSeconds;
            phaseTime += DogfightSim.TickSeconds;
            var target = sim.Opponent(ship);
            if (target == null) return new ShipCommand { Throttle = 0.6f };

            if (time >= nextJitterAt)
            {
                jitter = random.Range(-aimJitterDegrees, aimJitterDegrees) * DegreesToRadians;
                nextJitterAt = time + 1.2f;
            }

            var distance = (float)Math.Sqrt(DogfightSim.DistanceSquared(ship.X, ship.Z, target.X, target.Z));
            var toTarget = (float)Math.Atan2(target.X - ship.X, target.Z - ship.Z);
            var relative = DogfightSim.WrapAngle(toTarget - ship.Heading);
            var side = relative >= 0f ? 1f : -1f;
            UpdatePhase(ship, distance);

            // Attack runs instead of an endless orbit: close nose-on with the chase guns, turn to deliver
            // a broadside, then break away to a new altitude and come around again.
            float desired;
            float throttle;
            float altitudeGoal;
            switch (phase)
            {
                case Phase.Attack:
                    desired = DogfightSim.LeadAngle(ship, target, (ship.Loadout.ChaseGuns ?? ship.Loadout.Torpedo).projectileSpeed) + jitter;
                    throttle = 1f;
                    altitudeGoal = target.Y + altitudeOffset * AttackAltitudeShare;
                    break;
                case Phase.Broadside:
                    desired = toTarget - side * (float)Math.PI * 0.5f + jitter;
                    throttle = 0.75f;
                    altitudeGoal = target.Y;
                    break;
                default:
                    desired = toTarget + (float)Math.PI + side * 0.5f;
                    throttle = 1f;
                    altitudeGoal = target.Y + altitudeOffset;
                    break;
            }

            var guns = ship.Loadout.ChaseGuns;
            var torpedo = ship.Loadout.Torpedo;
            var broadside = ship.Loadout.Broadside;
            var beamError = Math.Abs(Math.Abs(relative) - (float)Math.PI * 0.5f);
            var command = new ShipCommand
            {
                Turn = DogfightSim.WrapAngle(desired - ship.Heading) * 2.2f,
                Throttle = throttle,
                Climb = Clamp1((altitudeGoal - ship.Y) / 10f),
                FireTorpedo = torpedo != null
                    && distance < torpedo.range * 0.95f
                    && Math.Abs(relative) <= torpedo.aimAssistDegrees * DegreesToRadians
                    && DogfightSim.CanElevate(ship, target, torpedo),
                FireChaseGuns = guns != null
                    && distance < guns.range * 0.95f
                    && Math.Abs(relative) <= guns.aimAssistDegrees * DegreesToRadians
                    && DogfightSim.CanElevate(ship, target, guns),
                FireBroadside = distance < broadside.range * 0.85f
                    && beamError <= 30f * DegreesToRadians
                    && DogfightSim.CanElevate(ship, target, broadside),
            };

            // A ship with an enemy on its tail weaves and changes altitude to throw off forward guns.
            if (distance < TailedDistance && Math.Abs(relative) > TailedAngle)
            {
                var weave = (int)(time / WeaveSeconds) % 2 == 0 ? 1f : -1f;
                command.Turn = weave;
                command.Climb = AwayFromAltitude(ship.Y - weave, ship.Y);
                command.Throttle = 1f;
            }

            if (sim.Field != null)
            {
                AvoidHazards(sim.Field, ship, ref command);
                FleeWhale(sim.Field, ship, ref command);
            }
            return command;
        }

        void UpdatePhase(ShipState ship, float distance)
        {
            switch (phase)
            {
                case Phase.Attack:
                    if (distance < broadsideDistance) SetPhase(Phase.Broadside);
                    break;
                case Phase.Broadside:
                    var fired = ship.BroadsideCooldown > ship.Loadout.Broadside.reload - 0.5f;
                    if (fired || phaseTime > BroadsidePhaseSeconds)
                    {
                        SetPhase(Phase.BreakAway);
                        altitudeOffset = (random.Value() < 0.5f ? -1f : 1f) * AltitudeSwing;
                    }
                    break;
                default:
                    if (distance > BreakAwayDistance || phaseTime > BreakAwayPhaseSeconds) SetPhase(Phase.Attack);
                    break;
            }
        }

        void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        // Steers around the nearest thing on the ship's path, in both heading and altitude.
        static void AvoidHazards(HazardField field, ShipState ship, ref ShipCommand command)
        {
            var speed = Math.Max(1f, ship.Speed);
            var forwardX = ship.ForwardX;
            var forwardZ = ship.ForwardZ;
            var forwardY = ship.ClimbRate / speed;
            var length = (float)Math.Sqrt(1f + forwardY * forwardY);
            forwardX /= length;
            forwardY /= length;
            forwardZ /= length;

            HazardState threat = null;
            var nearest = float.MaxValue;
            foreach (var hazard in field.Hazards)
            {
                if (!hazard.Alive) continue;
                var rx = hazard.X - ship.X;
                var ry = hazard.Y - ship.Y;
                var rz = hazard.Z - ship.Z;
                var along = rx * forwardX + ry * forwardY + rz * forwardZ;
                if (along < -hazard.Radius || along > speed * LookAheadSeconds + hazard.Radius || along >= nearest) continue;
                var px = rx - forwardX * along;
                var py = ry - forwardY * along;
                var pz = rz - forwardZ * along;
                var clearance = hazard.Radius + AvoidMargin;
                if (px * px + py * py + pz * pz >= clearance * clearance) continue;
                threat = hazard;
                nearest = along;
            }
            if (threat == null) return;

            var bearing = DogfightSim.WrapAngle((float)Math.Atan2(threat.X - ship.X, threat.Z - ship.Z) - ship.Heading);
            command.Turn = bearing >= 0f ? -1f : 1f;
            command.Climb = AwayFromAltitude(threat.Y, ship.Y);
        }

        public static void FleeWhale(HazardField field, ShipState ship, ref ShipCommand command)
        {
            WhaleState whale = null;
            foreach (var candidate in field.Whales)
            {
                if (candidate.MouthOpen && candidate.TargetId == ship.Id) whale = candidate;
            }
            if (whale == null) return;
            var dx = ship.X - whale.X;
            var dz = ship.Z - whale.Z;
            if (dx * dx + dz * dz > WhaleFleeRange * WhaleFleeRange) return;

            // Fly across the whale's line and change altitude, because it can't turn once it lunges.
            var across = (float)Math.Atan2(whale.DirX, whale.DirZ) + (float)Math.PI * 0.5f;
            if (Math.Abs(DogfightSim.WrapAngle(across - ship.Heading)) > (float)Math.PI * 0.5f) across += (float)Math.PI;
            command.Turn = DogfightSim.WrapAngle(across - ship.Heading) * 2.2f;
            command.Climb = AwayFromAltitude(whale.Y, ship.Y);
            command.Throttle = 1f;
        }

        static float AwayFromAltitude(float threatY, float shipY)
        {
            var direction = threatY >= shipY ? -1f : 1f;
            if (shipY <= DogfightSim.MinAltitude + 8f) return 1f;
            if (shipY >= DogfightSim.MaxAltitude - 8f) return -1f;
            return direction;
        }

        static float Clamp1(float value) => value < -1f ? -1f : value > 1f ? 1f : value;
    }

    // Small seeded generator, so battle and dogfight results replay the same on any machine.
    public class GameRandom
    {
        uint state;

        public GameRandom(int seed)
        {
            state = (uint)seed * 2654435761u + 1u;
            if (state == 0u) state = 1u;
        }

        public uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public float Value() => NextUInt() / (uint.MaxValue + 1f);
        public float Range(float min, float max) => min + (max - min) * Value();
        public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)(Value() * (maxExclusive - minInclusive));
    }
}
