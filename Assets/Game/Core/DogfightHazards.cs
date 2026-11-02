using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public enum HazardKind { Rock, Debris, Jellyfish, FishSchool, Mine }

    public class HazardState
    {
        public int Id;
        public HazardKind Kind;
        public float X;
        public float Y;
        public float Z;
        public float Radius;
        public float Damage;
        public float Toughness; // Damage from shots before debris breaks apart.
        public bool Alive = true;

        // Motion: a straight drift, a vertical bob, or a circular swim around the arena's center.
        public float DriftX;
        public float DriftZ;
        public float BaseY;
        public float BobPhase;
        public float OrbitRadius;
        public float OrbitAngle;
        public float OrbitRate;

        public float Heading; // Radians, for models that face the way they move.
        public float SwimSpeed; // Jellyfish only: meters per second at the average of its pulse.
    }

    public enum WhaleMode { Roaming, Stalking, Lunging, Resting }

    // The sky whale. X, Y and Z are its mouth, because that is the part that matters.
    public class WhaleState
    {
        public WhaleMode Mode;
        public float X;
        public float Y;
        public float Z;
        public float DirX;
        public float DirY;
        public float DirZ = 1f;
        public float Timer;
        public int TargetId = -1;
        public float WaypointX;
        public float WaypointY;
        public float WaypointZ;

        public bool MouthOpen => Mode == WhaleMode.Stalking || Mode == WhaleMode.Lunging;

        const float StalkingGape = 0.15f; // Parted while it hunts, as a warning.
        const float LungingGape = 0.45f;
        const float WideOpenDistance = 35f;
        const float StartsOpeningDistance = 150f;

        // How far its jaws are open, 0 to 1, for how far it is from its prey: wider the closer it gets.
        public float Gape(float distanceToPrey)
        {
            if (!MouthOpen) return 0f;
            var near = Math.Max(0f, Math.Min(1f, (StartsOpeningDistance - distanceToPrey) / (StartsOpeningDistance - WideOpenDistance)));
            var start = Mode == WhaleMode.Lunging ? LungingGape : StalkingGape;
            return start + (1f - start) * near;
        }
    }

    // Rocks, debris and flying sea life for one dogfight. Deterministic from its seed, like the rest of
    // the rules, so a server can run the same sky.
    public class HazardField
    {
        const float ShipCollisionShare = 0.75f;
        const float ClearingRadius = 55f;
        const float HazardSpread = 0.8f; // Keeps hazards in the part of the arena where fights happen.
        const float HitCooldown = 0.8f;
        const float BounceSlowdown = 0.45f;
        const float StalkTurnRate = 1.1f;
        const float RoamTurnRate = 0.5f;
        const float LeadSeconds = 0.35f;
        const float WhaleAvoidRange = 75f;
        const float WhaleAvoidTurnRate = 9f;
        const float PackFirstHunt = 8f; // A pack of whales starts early, one after another.
        const float PackStaggerSeconds = 8f;
        const float PackSpawnDistance = 0.9f; // Share of the arena's radius, and just inside it.
        const float JellySwimMin = 8f;
        const float JellySwimMax = 15f;
        const float JellyBob = 16f;
        const float JellyWander = 0.3f; // Radians per second at its widest curve.
        const float JellyTurnBack = 1.2f;
        const float PulseRate = 2.2f; // Matches the bell's pulse in the view, so they push as they squeeze.
        const float MineBob = 9f;

        readonly HazardDef def;
        readonly GameRandom random;
        readonly List<HazardState> hazards = new List<HazardState>();
        IReadOnlyList<ShipState> currentShips;
        float time;

        public IReadOnlyList<HazardState> Hazards => hazards;
        readonly List<WhaleState> whales = new List<WhaleState>();
        public IReadOnlyList<WhaleState> Whales => whales;
        public WhaleState Whale => whales.Count > 0 ? whales[0] : null;

        public HazardField(HazardDef def, int tier, int seed, IReadOnlyList<ShipState> ships)
        {
            this.def = def;
            random = new GameRandom(seed);
            AddMany(HazardKind.Rock, def.rockCount, def.rockMinRadius, def.rockMaxRadius, def.rockDamagePerMeter, ships);
            AddMany(HazardKind.Debris, def.debrisCount, def.debrisMinRadius, def.debrisMaxRadius, def.debrisDamagePerMeter, ships);
            AddMany(HazardKind.Jellyfish, def.jellyfishCount, def.jellyfishMinRadius, def.jellyfishMaxRadius, def.jellyfishDamagePerMeter, ships);
            AddMany(HazardKind.FishSchool, def.fishCount, def.fishRadius, def.fishRadius, def.fishDamagePerMeter, ships);
            if (tier >= def.whaleMinTier) AddWhales(1);
        }

        void AddMany(HazardKind kind, int count, float minRadius, float maxRadius, float damagePerMeter, IReadOnlyList<ShipState> ships)
        {
            for (var i = 0; i < count; i++)
            {
                var radius = random.Range(minRadius, maxRadius);
                var hazard = new HazardState
                {
                    Id = hazards.Count,
                    Kind = kind,
                    Radius = radius,
                    Damage = radius * damagePerMeter,
                    Toughness = radius * def.debrisToughnessPerMeter,
                };
                PlaceClearOfShips(hazard, ships);

                switch (kind)
                {
                    case HazardKind.Debris:
                        var angle = random.Range(0f, (float)Math.PI * 2f);
                        var drift = random.Range(1.5f, 5f);
                        hazard.DriftX = (float)Math.Sin(angle) * drift;
                        hazard.DriftZ = (float)Math.Cos(angle) * drift;
                        hazard.Heading = angle;
                        break;
                    case HazardKind.Jellyfish:
                        hazard.BaseY = Math.Max(DogfightSim.MinAltitude + JellyBob, Math.Min(DogfightSim.MaxAltitude - JellyBob, hazard.Y));
                        hazard.BobPhase = random.Range(0f, (float)Math.PI * 2f);
                        hazard.Heading = random.Range(0f, (float)Math.PI * 2f);
                        hazard.SwimSpeed = random.Range(JellySwimMin, JellySwimMax);
                        break;
                    case HazardKind.FishSchool:
                        hazard.OrbitRadius = random.Range(90f, DogfightSim.ArenaRadius - 30f);
                        hazard.OrbitAngle = random.Range(0f, (float)Math.PI * 2f);
                        hazard.OrbitRate = def.fishSpeed / hazard.OrbitRadius * (random.Value() < 0.5f ? -1f : 1f);
                        hazard.BaseY = hazard.Y;
                        hazard.X = (float)Math.Sin(hazard.OrbitAngle) * hazard.OrbitRadius;
                        hazard.Z = (float)Math.Cos(hazard.OrbitAngle) * hazard.OrbitRadius;
                        break;
                }
                hazards.Add(hazard);
            }
        }

        void PlaceClearOfShips(HazardState hazard, IReadOnlyList<ShipState> ships)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var angle = random.Range(0f, (float)Math.PI * 2f);
                var distance = (float)Math.Sqrt(random.Value()) * DogfightSim.ArenaRadius * HazardSpread;
                hazard.X = (float)Math.Sin(angle) * distance;
                hazard.Z = (float)Math.Cos(angle) * distance;
                hazard.Y = random.Range(DogfightSim.MinAltitude, DogfightSim.MaxAltitude);

                var clear = true;
                var keepOut = ClearingRadius + hazard.Radius;
                foreach (var ship in ships)
                {
                    if (DogfightSim.DistanceSquared(hazard.X, hazard.Z, ship.X, ship.Z) < keepOut * keepOut) clear = false;
                }
                if (clear) return;
            }
        }

        // Several whales come in from different sides, and take turns starting their first hunts.
        public void AddWhales(int count)
        {
            var start = random.Range(0f, (float)Math.PI * 2f);
            for (var i = 0; i < count; i++)
            {
                var angle = start + i * (float)Math.PI * 2f / count;
                var distance = DogfightSim.ArenaRadius * (count > 1 ? PackSpawnDistance : 1.2f);
                var whale = new WhaleState
                {
                    Mode = WhaleMode.Resting,
                    X = (float)Math.Sin(angle) * distance,
                    Z = (float)Math.Cos(angle) * distance,
                    Y = random.Range(DogfightSim.MinAltitude, DogfightSim.MaxAltitude),
                    DirX = -(float)Math.Sin(angle),
                    DirZ = -(float)Math.Cos(angle),
                    Timer = count > 1 ? PackFirstHunt + i * PackStaggerSeconds : random.Range(def.whaleFirstDelayMin, def.whaleFirstDelayMax),
                };
                PickWaypoint(whale);
                whales.Add(whale);
            }
        }

        // Drifting Lift-Stone mines. One touch or a few shots sets them off, and the blast hurts everyone near.
        public void AddMines(int count, float radius, float toughness, IReadOnlyList<ShipState> ships)
        {
            for (var i = 0; i < count; i++)
            {
                var hazard = new HazardState { Id = hazards.Count, Kind = HazardKind.Mine, Radius = radius, Damage = 0f, Toughness = toughness };
                PlaceClearOfShips(hazard, ships);
                hazard.Y = random.Range(-14f, 22f);
                hazard.BaseY = hazard.Y;
                hazard.BobPhase = random.Range(0f, (float)Math.PI * 2f);
                hazards.Add(hazard);
            }
        }

        // Set by the encounter, because a blast has to reach every ship.
        public float MineBlastRadius { get; set; }
        public float MineBlastDamage { get; set; }

        void Detonate(HazardState mine, IReadOnlyList<ShipState> ships, List<DogfightEvent> events)
        {
            mine.Alive = false;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.MineBlast, HazardId = mine.Id, HazardKind = HazardKind.Mine, X = mine.X, Y = mine.Y, Z = mine.Z });
            foreach (var ship in ships)
            {
                if (!ship.Alive || Distance(ship.X, ship.Y, ship.Z, mine.X, mine.Y, mine.Z) > MineBlastRadius) continue;
                ship.TakeDamage(MineBlastDamage);
                events.Add(new DogfightEvent { Kind = DogfightEventKind.HazardHit, ShipId = ship.Id, HazardId = mine.Id, HazardKind = HazardKind.Mine, X = ship.X, Y = ship.Y, Z = ship.Z, Amount = MineBlastDamage });
                if (!ship.Alive) events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
            }
        }

        public void Step(IReadOnlyList<ShipState> ships, List<DogfightEvent> events)
        {
            currentShips = ships;
            time += DogfightSim.TickSeconds;
            foreach (var hazard in hazards)
            {
                if (hazard.Alive) Move(hazard);
            }
            foreach (var whale in whales) StepWhale(whale, ships, events);
            foreach (var ship in ships)
            {
                if (ship.Alive) Collide(ship, events);
            }
        }

        void Move(HazardState hazard)
        {
            switch (hazard.Kind)
            {
                case HazardKind.Debris:
                    hazard.X += hazard.DriftX * DogfightSim.TickSeconds;
                    hazard.Z += hazard.DriftZ * DogfightSim.TickSeconds;
                    break;
                case HazardKind.Jellyfish:
                    Swim(hazard);
                    break;
                case HazardKind.Mine:
                    hazard.Y = hazard.BaseY + (float)Math.Sin(time * 0.6f + hazard.BobPhase) * MineBob;
                    break;
                case HazardKind.FishSchool:
                    hazard.OrbitAngle += hazard.OrbitRate * DogfightSim.TickSeconds;
                    hazard.X = (float)Math.Sin(hazard.OrbitAngle) * hazard.OrbitRadius;
                    hazard.Z = (float)Math.Cos(hazard.OrbitAngle) * hazard.OrbitRadius;
                    hazard.Y = hazard.BaseY + (float)Math.Sin(hazard.OrbitAngle * 3f) * 6f;
                    hazard.Heading = hazard.OrbitAngle + (float)Math.PI * 0.5f * Math.Sign(hazard.OrbitRate);
                    break;
            }
        }

        // Jellyfish wander around the sky in curves, pushing forward with each pulse of the bell.
        void Swim(HazardState jelly)
        {
            jelly.Heading += (float)Math.Sin(time * 0.4f + jelly.BobPhase) * JellyWander * DogfightSim.TickSeconds;
            if (jelly.X * jelly.X + jelly.Z * jelly.Z > DogfightSim.ArenaRadius * HazardSpread * DogfightSim.ArenaRadius * HazardSpread)
            {
                var home = (float)Math.Atan2(-jelly.X, -jelly.Z);
                jelly.Heading += DogfightSim.WrapAngle(home - jelly.Heading) * Math.Min(1f, JellyTurnBack * DogfightSim.TickSeconds);
            }
            var push = 0.4f + 1.2f * Math.Max(0f, (float)Math.Sin(time * PulseRate + jelly.BobPhase));
            jelly.X += (float)Math.Sin(jelly.Heading) * jelly.SwimSpeed * push * DogfightSim.TickSeconds;
            jelly.Z += (float)Math.Cos(jelly.Heading) * jelly.SwimSpeed * push * DogfightSim.TickSeconds;
            jelly.Y = DogfightSim.ClampAltitude(jelly.BaseY + (float)Math.Sin(time * 0.6f + jelly.BobPhase) * JellyBob);
        }

        void Collide(ShipState ship, List<DogfightEvent> events)
        {
            var shipRadius = ship.Loadout.Radius * ShipCollisionShare;
            foreach (var hazard in hazards)
            {
                if (!hazard.Alive) continue;
                var dx = ship.X - hazard.X;
                var dy = ship.Y - hazard.Y;
                var dz = ship.Z - hazard.Z;
                var reach = hazard.Radius + shipRadius;
                var distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared >= reach * reach) continue;

                if (hazard.Kind == HazardKind.Mine)
                {
                    Detonate(hazard, currentShips, events);
                    if (!ship.Alive) return;
                    continue;
                }

                // Solid things push the ship out even during the grace period after a hit.
                var distance = Math.Max(0.01f, (float)Math.Sqrt(distanceSquared));
                if (hazard.Kind == HazardKind.Rock)
                {
                    var push = reach - distance;
                    ship.X += dx / distance * push;
                    ship.Y = DogfightSim.ClampAltitude(ship.Y + dy / distance * push);
                    ship.Z += dz / distance * push;
                }

                if (ship.CollisionCooldown > 0f) continue;
                ship.CollisionCooldown = HitCooldown;
                ship.Speed *= BounceSlowdown;
                ship.TakeDamage(hazard.Damage);
                events.Add(new DogfightEvent
                {
                    Kind = DogfightEventKind.HazardHit,
                    ShipId = ship.Id,
                    HazardId = hazard.Id,
                    HazardKind = hazard.Kind,
                    X = hazard.X + dx / distance * hazard.Radius,
                    Y = hazard.Y + dy / distance * hazard.Radius,
                    Z = hazard.Z + dz / distance * hazard.Radius,
                    Amount = hazard.Damage,
                });
                if (hazard.Kind == HazardKind.Debris || hazard.Kind == HazardKind.Jellyfish) Break(hazard, events);
                if (!ship.Alive)
                {
                    events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
                    return;
                }
            }
        }

        void Break(HazardState hazard, List<DogfightEvent> events)
        {
            hazard.Alive = false;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.HazardBroken, HazardId = hazard.Id, HazardKind = hazard.Kind, X = hazard.X, Y = hazard.Y, Z = hazard.Z, Amount = hazard.Radius });
        }

        // Rocks stop cannon fire, and enough shots break debris apart. Sea life lets shots pass.
        public bool BlocksShot(ProjectileState projectile, List<DogfightEvent> events)
        {
            foreach (var hazard in hazards)
            {
                if (!hazard.Alive || (hazard.Kind != HazardKind.Rock && hazard.Kind != HazardKind.Debris && hazard.Kind != HazardKind.Mine)) continue;
                var dx = projectile.X - hazard.X;
                var dy = projectile.Y - hazard.Y;
                var dz = projectile.Z - hazard.Z;
                if (dx * dx + dy * dy + dz * dz > hazard.Radius * hazard.Radius) continue;

                events.Add(new DogfightEvent { Kind = DogfightEventKind.ShotBlocked, ProjectileId = projectile.Id, HazardId = hazard.Id, HazardKind = hazard.Kind, X = projectile.X, Y = projectile.Y, Z = projectile.Z, Heavy = projectile.Heavy });
                if (hazard.Kind == HazardKind.Debris)
                {
                    hazard.Toughness -= projectile.Damage;
                    if (hazard.Toughness <= 0f) Break(hazard, events);
                }
                else if (hazard.Kind == HazardKind.Mine)
                {
                    hazard.Toughness -= projectile.Damage;
                    if (hazard.Toughness <= 0f) Detonate(hazard, currentShips, events);
                }
                return true;
            }
            return false;
        }

        void StepWhale(WhaleState whale, IReadOnlyList<ShipState> ships, List<DogfightEvent> events)
        {
            whale.Timer -= DogfightSim.TickSeconds;
            float speed;
            switch (whale.Mode)
            {
                case WhaleMode.Stalking:
                    speed = def.whaleCruiseSpeed * 0.5f;
                    var target = ships[whale.TargetId];
                    if (target.Alive) SteerWhale(whale, target.X, target.Y, target.Z, StalkTurnRate);
                    if (whale.Timer <= 0f) BeginLunge(whale, target, events);
                    break;
                case WhaleMode.Lunging:
                    speed = def.whaleLungeSpeed;
                    if (whale.Timer <= 0f) Rest(whale);
                    break;
                case WhaleMode.Roaming:
                    speed = def.whaleCruiseSpeed;
                    SteerWhale(whale, whale.WaypointX, whale.WaypointY, whale.WaypointZ, RoamTurnRate);
                    if (Distance(whale.X, whale.Y, whale.Z, whale.WaypointX, whale.WaypointY, whale.WaypointZ) < 40f) PickWaypoint(whale);
                    if (whale.Timer <= 0f && !AnyWhaleHunting) TryStalk(whale, ships, events);
                    break;
                default:
                    speed = def.whaleCruiseSpeed;
                    SteerWhale(whale, whale.WaypointX, whale.WaypointY, whale.WaypointZ, RoamTurnRate);
                    if (Distance(whale.X, whale.Y, whale.Z, whale.WaypointX, whale.WaypointY, whale.WaypointZ) < 40f) PickWaypoint(whale);
                    if (whale.Timer <= 0f) whale.Mode = WhaleMode.Roaming;
                    break;
            }

            SteerAroundUnhuntedShips(whale, ships);
            whale.X += whale.DirX * speed * DogfightSim.TickSeconds;
            whale.Y += whale.DirY * speed * DogfightSim.TickSeconds;
            whale.Z += whale.DirZ * speed * DogfightSim.TickSeconds;

            if (!whale.MouthOpen) return;
            foreach (var ship in ships)
            {
                if (!ship.Alive || !ship.HuntedByWhale || ship.ImmuneTimer > 0f) continue;
                var reach = def.whaleMouthRadius + ship.Loadout.Radius * ShipCollisionShare;
                if (Distance(whale.X, whale.Y, whale.Z, ship.X, ship.Y, ship.Z) > reach) continue;
                ship.Hull = 0f;
                ship.Eaten = true;
                events.Add(new DogfightEvent { Kind = DogfightEventKind.Eaten, ShipId = ship.Id, X = whale.X, Y = whale.Y, Z = whale.Z });
                Rest(whale);
                return;
            }
        }

        // Only one whale hunts at a time, so a player can always read which one to dodge.
        bool AnyWhaleHunting
        {
            get
            {
                foreach (var whale in whales)
                {
                    if (whale.MouthOpen) return true;
                }
                return false;
            }
        }

        void TryStalk(WhaleState whale, IReadOnlyList<ShipState> ships, List<DogfightEvent> events)
        {
            ShipState nearest = null;
            var nearestDistance = def.whaleSenseRange;
            foreach (var ship in ships)
            {
                if (!ship.Alive || !ship.HuntedByWhale) continue;
                var distance = Distance(whale.X, whale.Y, whale.Z, ship.X, ship.Y, ship.Z);
                if (distance >= nearestDistance) continue;
                nearest = ship;
                nearestDistance = distance;
            }
            if (nearest == null) return;
            whale.Mode = WhaleMode.Stalking;
            whale.TargetId = nearest.Id;
            whale.Timer = def.whaleAlertSeconds;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.WhaleAlert, ShipId = nearest.Id, X = whale.X, Y = whale.Y, Z = whale.Z });
        }

        // The whale commits to a line here, so turning or changing altitude escapes it.
        void BeginLunge(WhaleState whale, ShipState target, List<DogfightEvent> events)
        {
            whale.Mode = WhaleMode.Lunging;
            whale.Timer = def.whaleLungeSeconds;
            var aimX = target.X + target.ForwardX * target.Speed * LeadSeconds - whale.X;
            var aimY = target.Y + target.ClimbRate * LeadSeconds - whale.Y;
            var aimZ = target.Z + target.ForwardZ * target.Speed * LeadSeconds - whale.Z;
            var length = Math.Max(0.01f, (float)Math.Sqrt(aimX * aimX + aimY * aimY + aimZ * aimZ));
            whale.DirX = aimX / length;
            whale.DirY = aimY / length;
            whale.DirZ = aimZ / length;
            events.Add(new DogfightEvent { Kind = DogfightEventKind.WhaleLunge, ShipId = target.Id, X = whale.X, Y = whale.Y, Z = whale.Z });
        }

        // Turns the whale away from ships it doesn't hunt, more sharply the closer they are.
        void SteerAroundUnhuntedShips(WhaleState whale, IReadOnlyList<ShipState> ships)
        {
            foreach (var ship in ships)
            {
                if (!ship.Alive || ship.HuntedByWhale) continue;
                var awayX = whale.X - ship.X;
                var awayY = whale.Y - ship.Y;
                var awayZ = whale.Z - ship.Z;
                var distance = Math.Max(0.01f, (float)Math.Sqrt(awayX * awayX + awayY * awayY + awayZ * awayZ));
                if (distance >= WhaleAvoidRange) continue;

                // Steering straight away does nothing on a head-on course, so it also veers to one side.
                var sideX = whale.DirZ;
                var sideZ = -whale.DirX;
                var side = awayX * sideX + awayZ * sideZ >= 0f ? 1f : -1f;
                var goalX = awayX / distance + sideX * side;
                var goalY = awayY / distance;
                var goalZ = awayZ / distance + sideZ * side;

                var blend = Math.Min(1f, WhaleAvoidTurnRate * (1f - distance / WhaleAvoidRange) * DogfightSim.TickSeconds);
                var dirX = whale.DirX + (goalX - whale.DirX) * blend;
                var dirY = whale.DirY + (goalY - whale.DirY) * blend;
                var dirZ = whale.DirZ + (goalZ - whale.DirZ) * blend;
                var length = Math.Max(0.01f, (float)Math.Sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ));
                whale.DirX = dirX / length;
                whale.DirY = dirY / length;
                whale.DirZ = dirZ / length;
            }
        }

        void Rest(WhaleState whale)
        {
            whale.Mode = WhaleMode.Resting;
            whale.TargetId = -1;
            whale.Timer = random.Range(def.whaleRestMin, def.whaleRestMax);
            PickWaypoint(whale);
        }

        void PickWaypoint(WhaleState whale)
        {
            var angle = random.Range(0f, (float)Math.PI * 2f);
            var distance = random.Range(40f, DogfightSim.ArenaRadius);
            whale.WaypointX = (float)Math.Sin(angle) * distance;
            whale.WaypointZ = (float)Math.Cos(angle) * distance;
            whale.WaypointY = random.Range(DogfightSim.MinAltitude, DogfightSim.MaxAltitude);
        }

        void SteerWhale(WhaleState whale, float x, float y, float z, float rate)
        {
            var toX = x - whale.X;
            var toY = y - whale.Y;
            var toZ = z - whale.Z;
            var length = Math.Max(0.01f, (float)Math.Sqrt(toX * toX + toY * toY + toZ * toZ));
            var blend = Math.Min(1f, rate * DogfightSim.TickSeconds);
            var dirX = whale.DirX + (toX / length - whale.DirX) * blend;
            var dirY = whale.DirY + (toY / length - whale.DirY) * blend;
            var dirZ = whale.DirZ + (toZ / length - whale.DirZ) * blend;
            var dirLength = Math.Max(0.01f, (float)Math.Sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ));
            whale.DirX = dirX / dirLength;
            whale.DirY = dirY / dirLength;
            whale.DirZ = dirZ / dirLength;
        }

        static float Distance(float ax, float ay, float az, float bx, float by, float bz)
        {
            var dx = ax - bx;
            var dy = ay - by;
            var dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
