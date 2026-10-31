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
        public CannonDef Bow;
        public CannonDef Broadside;
        public float BowDamageScale = 1f;
        public float BroadsideDamageScale = 1f;
    }

    // One tick of input for one ship. Both the player's touch controls, the AI, and later a network
    // client produce this same structure, so the simulation never knows who is flying.
    public struct ShipCommand
    {
        public float Turn;     // -1 left .. 1 right
        public float Throttle; // 0 .. 1
        public bool FireBow;
        public bool FireBroadside;
    }

    public class ShipState
    {
        public int Id;
        public int Team;
        public ShipLoadout Loadout;
        public float X;
        public float Z;
        public float Heading; // Radians, 0 faces +Z, positive turns toward +X.
        public float Speed;
        public float TurnRate; // Radians per second this tick, used for visual banking.
        public float Hull;
        public float BowCooldown;
        public float BroadsideCooldown;

        public bool Alive => Hull > 0f;
        public float ForwardX => (float)Math.Sin(Heading);
        public float ForwardZ => (float)Math.Cos(Heading);
    }

    public class ProjectileState
    {
        public int Id;
        public int OwnerId;
        public int Team;
        public float X;
        public float Z;
        public float VelocityX;
        public float VelocityZ;
        public float Damage;
        public float Remaining;
        public bool Heavy;
    }

    public enum DogfightEventKind { Fired, Hit, Missed, Destroyed }

    public struct DogfightEvent
    {
        public DogfightEventKind Kind;
        public int ShipId;
        public int ProjectileId;
        public float X;
        public float Z;
        public float Amount;
        public bool Heavy;
    }

    // Dogfight rules with no Unity dependency. Fixed-step and driven only by commands and a seed, so
    // a server can run the same rules and decide the outcome for online matches.
    public class DogfightSim
    {
        public const int TicksPerSecond = 30;
        public const float TickSeconds = 1f / TicksPerSecond;
        public const float MinimumDamageShare = 0.2f;
        public const float ArenaRadius = 300f;
        const float ProjectileRadius = 1f;
        const float MinimumThrottle = 0.35f;
        const float Acceleration = 16f;
        const float BoundaryTurnStrength = 2.2f;

        readonly List<ShipState> ships = new List<ShipState>();
        readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        readonly List<DogfightEvent> events = new List<DogfightEvent>();
        readonly GameRandom random;
        int nextProjectileId = 1;

        public IReadOnlyList<ShipState> Ships => ships;
        public IReadOnlyList<ProjectileState> Projectiles => projectiles;
        public IReadOnlyList<DogfightEvent> Events => events;
        public int Tick { get; private set; }
        public int WinningTeam { get; private set; } = -1;
        public bool Finished => WinningTeam >= 0;

        public DogfightSim(int seed)
        {
            random = new GameRandom(seed);
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
            ships.Add(ship);
            return ship;
        }

        public ShipState Opponent(ShipState ship)
        {
            ShipState best = null;
            var bestDistance = float.MaxValue;
            foreach (var other in ships)
            {
                if (other.Team == ship.Team || !other.Alive) continue;
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
            if (Finished) return;
            Tick++;
            for (var i = 0; i < ships.Count; i++)
            {
                if (ships[i].Alive) StepShip(ships[i], commands[i]);
            }
            StepProjectiles();
            CheckFinished();
        }

        void StepShip(ShipState ship, ShipCommand command)
        {
            var loadout = ship.Loadout;
            var turn = Clamp(command.Turn, -1f, 1f) * loadout.TurnDegrees * DegreesToRadians;

            // Past the arena edge the wind pushes every ship back toward the center.
            var distanceFromCenter = (float)Math.Sqrt(ship.X * ship.X + ship.Z * ship.Z);
            if (distanceFromCenter > ArenaRadius)
            {
                var toCenter = (float)Math.Atan2(-ship.X, -ship.Z);
                var over = Clamp((distanceFromCenter - ArenaRadius) / 40f, 0f, 1f);
                turn += WrapAngle(toCenter - ship.Heading) * BoundaryTurnStrength * over;
            }

            ship.TurnRate = turn;
            ship.Heading = WrapAngle(ship.Heading + turn * TickSeconds);

            var throttle = Clamp(command.Throttle, MinimumThrottle, 1f);
            ship.Speed = MoveTowards(ship.Speed, loadout.Speed * throttle, Acceleration * TickSeconds);
            ship.X += ship.ForwardX * ship.Speed * TickSeconds;
            ship.Z += ship.ForwardZ * ship.Speed * TickSeconds;

            ship.BowCooldown = Math.Max(0f, ship.BowCooldown - TickSeconds);
            ship.BroadsideCooldown = Math.Max(0f, ship.BroadsideCooldown - TickSeconds);

            if (command.FireBow && ship.BowCooldown <= 0f) FireBow(ship);
            if (command.FireBroadside && ship.BroadsideCooldown <= 0f) FireBroadside(ship);
        }

        void FireBow(ShipState ship)
        {
            var cannon = ship.Loadout.Bow;
            ship.BowCooldown = cannon.reload;
            var aim = ship.Heading;

            // Gentle aim assist so firing works with thumbs: shots curve onto a target near the bow.
            var target = Opponent(ship);
            if (target != null)
            {
                var toTarget = LeadAngle(ship, target, cannon.projectileSpeed);
                if (Math.Abs(WrapAngle(toTarget - ship.Heading)) <= cannon.aimAssistDegrees * DegreesToRadians)
                {
                    aim = toTarget;
                }
            }
            FireVolley(ship, cannon, aim, ship.Loadout.BowDamageScale, 0f, false);
        }

        void FireBroadside(ShipState ship)
        {
            var cannon = ship.Loadout.Broadside;
            ship.BroadsideCooldown = cannon.reload;
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
            FireVolley(ship, cannon, aim, ship.Loadout.BroadsideDamageScale, side, true);
        }

        void FireVolley(ShipState ship, CannonDef cannon, float aim, float damageScale, float side, bool heavy)
        {
            var count = Math.Max(1, cannon.shots);
            for (var i = 0; i < count; i++)
            {
                var fraction = count == 1 ? 0f : i / (float)(count - 1) - 0.5f;
                var angle = aim + (fraction + random.Range(-0.12f, 0.12f)) * cannon.spreadDegrees * DegreesToRadians;
                var speed = cannon.projectileSpeed;

                // Broadside guns sit along the hull; bow guns sit at the front.
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
                    Z = startZ,
                    VelocityX = (float)Math.Sin(angle) * speed,
                    VelocityZ = (float)Math.Cos(angle) * speed,
                    Damage = cannon.damage * damageScale,
                    Remaining = cannon.range / speed,
                    Heavy = heavy,
                };
                projectiles.Add(projectile);
                events.Add(new DogfightEvent
                {
                    Kind = DogfightEventKind.Fired,
                    ShipId = ship.Id,
                    ProjectileId = projectile.Id,
                    X = startX,
                    Z = startZ,
                    Heavy = heavy,
                });
            }
        }

        void StepProjectiles()
        {
            for (var i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                projectile.X += projectile.VelocityX * TickSeconds;
                projectile.Z += projectile.VelocityZ * TickSeconds;
                projectile.Remaining -= TickSeconds;

                var hit = FindHit(projectile);
                if (hit != null)
                {
                    ApplyHit(projectile, hit);
                    projectiles.RemoveAt(i);
                }
                else if (projectile.Remaining <= 0f)
                {
                    events.Add(new DogfightEvent
                    {
                        Kind = DogfightEventKind.Missed,
                        ProjectileId = projectile.Id,
                        X = projectile.X,
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
                if (!ship.Alive || ship.Team == projectile.Team) continue;
                var radius = ship.Loadout.Radius + ProjectileRadius;
                if (DistanceSquared(projectile.X, projectile.Z, ship.X, ship.Z) <= radius * radius) return ship;
            }
            return null;
        }

        void ApplyHit(ProjectileState projectile, ShipState ship)
        {
            var damage = DamageAfterArmor(projectile.Damage, ship.Loadout.Armor);
            ship.Hull = Math.Max(0f, ship.Hull - damage);
            events.Add(new DogfightEvent
            {
                Kind = DogfightEventKind.Hit,
                ShipId = ship.Id,
                ProjectileId = projectile.Id,
                X = projectile.X,
                Z = projectile.Z,
                Amount = damage,
                Heavy = projectile.Heavy,
            });
            if (!ship.Alive)
            {
                events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = ship.Id, X = ship.X, Z = ship.Z });
            }
        }

        void CheckFinished()
        {
            var aliveTeam = -1;
            foreach (var ship in ships)
            {
                if (!ship.Alive) continue;
                if (aliveTeam >= 0 && aliveTeam != ship.Team) return;
                aliveTeam = ship.Team;
            }
            if (aliveTeam >= 0) WinningTeam = aliveTeam;
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
        const float DegreesToRadians = (float)(Math.PI / 180.0);
        readonly GameRandom random;
        readonly float aimJitterDegrees;
        float jitter;
        float nextJitterAt;
        float time;

        public DogfightAi(int seed, float aimJitterDegrees)
        {
            random = new GameRandom(seed);
            this.aimJitterDegrees = aimJitterDegrees;
        }

        public ShipCommand Think(DogfightSim sim, ShipState ship)
        {
            time += DogfightSim.TickSeconds;
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
            var broadsideRange = ship.Loadout.Broadside.range * 0.8f;

            // Chase from afar. Once in range, turn side-on so the broadside bears on the target.
            float desired;
            if (distance > broadsideRange)
            {
                desired = toTarget + jitter;
            }
            else
            {
                var side = relative >= 0f ? 1f : -1f;
                desired = toTarget - side * (float)Math.PI * 0.42f + jitter;
            }

            var error = DogfightSim.WrapAngle(desired - ship.Heading);
            var beamError = Math.Abs(Math.Abs(relative) - (float)Math.PI * 0.5f);
            return new ShipCommand
            {
                Turn = error * 2.2f,
                Throttle = distance > broadsideRange ? 1f : 0.7f,
                FireBow = distance < ship.Loadout.Bow.range * 0.95f
                    && Math.Abs(relative) <= ship.Loadout.Bow.aimAssistDegrees * DegreesToRadians,
                FireBroadside = distance < broadsideRange && beamError <= 35f * DegreesToRadians,
            };
        }
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
