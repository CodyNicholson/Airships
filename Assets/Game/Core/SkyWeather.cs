using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public enum SkyZoneKind { Wind, Cloud, TornadoUp, TornadoDown, Toxic }

    public class SkyZone
    {
        public SkyZoneKind Kind;
        public float X;
        public float Y;
        public float Z;
        public float Radius;
        public float Heading; // The way a wind tunnel blows.
        public float Length; // Wind tunnels only: Radius is the tube's, and X, Y and Z its middle.
        public float Strength;
    }

    public class LightningStrike
    {
        public float X;
        public float Z;
        public float Timer;
    }

    // Wind tunnels, cloud banks, tornadoes and lightning for one fight. Deterministic from its seed.
    public class SkyWeather
    {
        const float WindAngle = 1.05f; // Radians either side of the wind where a ship still rides it.
        const float WindRadius = 26f;
        const float TunnelLength = 240f;
        const float TunnelPush = 26f; // Meters per second along the tunnel, felt the moment a ship enters.
        const float TunnelAltitudeMin = -15f;
        const float TunnelAltitudeMax = 35f;
        const float CloudRadius = 58f;
        const float CloudHeight = 22f;
        const float TornadoRadius = 44f;
        const float SkyRingMin = 165f; // Wind tunnels and tornadoes sit around a ring, well clear of the middle.
        const float SkyRingMax = 186f;
        const float SlotWobble = 0.1f; // Radians, so a sky doesn't look machine-made.
        const float TunnelWobble = 0.15f;
        const float TornadoDragSpeed = 45f; // Top to bottom of the sky in about two and a half seconds.
        const float ToxicRadius = 52f;
        const float StrikeSpread = 34f;
        const float ZoneSpread = 0.85f;

        readonly List<SkyZone> zones = new List<SkyZone>();
        readonly List<LightningStrike> strikes = new List<LightningStrike>();
        readonly GameRandom random;
        readonly SkyRulesDef rules;
        readonly float lightningEvery;
        float nextStrike;

        public IReadOnlyList<SkyZone> Zones => zones;
        public IReadOnlyList<LightningStrike> Strikes => strikes;
        public int LightningTargetId { get; set; }

        readonly bool level;

        // Level puts every wind tunnel at the height of a fight where ships can't climb.
        public SkyWeather(WeatherDef def, SkyRulesDef rules, int seed, bool level = false)
        {
            this.rules = rules;
            this.level = level;
            random = new GameRandom(seed);
            lightningEvery = def.lightningEvery;
            nextStrike = lightningEvery;
            SpreadTunnelsAndTornadoes(def);
            AddZones(SkyZoneKind.Cloud, def.cloudCount, CloudRadius);
            AddZones(SkyZoneKind.Toxic, def.toxicCount, ToxicRadius);
        }

        // Evenly spaced slots around one ring, taking turns between tunnels and tornadoes so like doesn't
        // sit beside like. Each tunnel runs along the ring, so it never points at its neighbors.
        void SpreadTunnelsAndTornadoes(WeatherDef def)
        {
            var tunnels = def.windCount;
            var tornadoes = new List<SkyZoneKind>();
            for (var i = 0; i < def.tornadoUpCount; i++) tornadoes.Add(SkyZoneKind.TornadoUp);
            for (var i = 0; i < def.tornadoDownCount; i++) tornadoes.Add(SkyZoneKind.TornadoDown);
            var total = tunnels + tornadoes.Count;
            if (total == 0) return;

            var slot = (float)Math.PI * 2f / total;
            var start = random.Range(0f, (float)Math.PI * 2f);
            var nextIsTunnel = tunnels >= tornadoes.Count;
            for (var i = 0; i < total; i++)
            {
                var angle = start + i * slot + random.Range(-SlotWobble, SlotWobble);
                var distance = random.Range(SkyRingMin, SkyRingMax);
                var useTunnel = tunnels > 0 && (nextIsTunnel || tornadoes.Count == 0);
                if (useTunnel)
                {
                    tunnels--;
                    var along = angle + (random.Value() < 0.5f ? 0.5f : -0.5f) * (float)Math.PI + random.Range(-TunnelWobble, TunnelWobble);
                    AddZone(SkyZoneKind.Wind, WindRadius, angle, distance, along);
                }
                else
                {
                    var kind = tornadoes[tornadoes.Count - 1];
                    tornadoes.RemoveAt(tornadoes.Count - 1);
                    AddZone(kind, TornadoRadius, angle, distance, 0f);
                }
                nextIsTunnel = !useTunnel;
            }
        }

        void AddZones(SkyZoneKind kind, int count, float radius)
        {
            for (var i = 0; i < count; i++)
            {
                // Evenly around the arena with some wobble, so a zone is always within reach.
                var angle = (i + random.Range(0f, 0.7f)) / Math.Max(1, count) * (float)Math.PI * 2f;
                var distance = random.Range(0.3f, 1f) * DogfightSim.ArenaRadius * ZoneSpread;
                AddZone(kind, radius, angle, distance, random.Range(0f, (float)Math.PI * 2f));
            }
        }

        void AddZone(SkyZoneKind kind, float radius, float angle, float distance, float heading)
        {
            var isWind = kind == SkyZoneKind.Wind;
            zones.Add(new SkyZone
            {
                Kind = kind,
                Length = isWind ? TunnelLength : 0f,
                X = (float)Math.Sin(angle) * distance,
                Z = (float)Math.Cos(angle) * distance,
                Y = isWind ? (level ? 0f : random.Range(TunnelAltitudeMin, TunnelAltitudeMax)) : kind == SkyZoneKind.Cloud || kind == SkyZoneKind.Toxic ? random.Range(-12f, 22f) : 0f,
                Radius = radius,
                Heading = heading,
                Strength = 1f,
            });
        }

        static bool IsTornado(SkyZoneKind kind) => kind == SkyZoneKind.TornadoUp || kind == SkyZoneKind.TornadoDown;

        static bool Inside(ShipState ship, SkyZone zone)
        {
            return DogfightSim.DistanceSquared(ship.X, ship.Z, zone.X, zone.Z) <= zone.Radius * zone.Radius;
        }

        // Whether a ship is flying with a wind tunnel, inside its tube.
        static bool RidingTunnel(ShipState ship, SkyZone zone)
        {
            if (zone.Kind != SkyZoneKind.Wind) return false;
            var dirX = (float)Math.Sin(zone.Heading);
            var dirZ = (float)Math.Cos(zone.Heading);
            var dx = ship.X - zone.X;
            var dz = ship.Z - zone.Z;
            var along = dx * dirX + dz * dirZ;
            if (Math.Abs(along) > zone.Length * 0.5f) return false;
            var across = dx * dirZ - dz * dirX;
            var dy = ship.Y - zone.Y;
            if (across * across + dy * dy > zone.Radius * zone.Radius) return false;
            return Math.Abs(DogfightSim.WrapAngle(zone.Heading - ship.Heading)) <= WindAngle;
        }

        // Flying with a wind tunnel multiplies airspeed. Flying against one does nothing.
        public float WindScale(ShipState ship, float windSpeedScale)
        {
            foreach (var zone in zones)
            {
                if (RidingTunnel(ship, zone)) return windSpeedScale;
            }
            return 1f;
        }

        public void Step(DogfightSim sim, List<DogfightEvent> events)
        {
            foreach (var ship in sim.Ships)
            {
                if (!ship.Alive) continue;
                ship.Hidden = false;
                foreach (var zone in zones)
                {
                    if (RidingTunnel(ship, zone))
                    {
                        ship.X += (float)Math.Sin(zone.Heading) * TunnelPush * DogfightSim.TickSeconds;
                        ship.Z += (float)Math.Cos(zone.Heading) * TunnelPush * DogfightSim.TickSeconds;
                    }
                    if (!Inside(ship, zone)) continue;
                    if (zone.Kind == SkyZoneKind.Cloud && Math.Abs(ship.Y - zone.Y) <= CloudHeight) ship.Hidden = true;
                    if (IsTornado(zone.Kind) && !ship.AltitudeLocked && ship.TornadoDrag == 0f)
                    {
                        var drag = zone.Kind == SkyZoneKind.TornadoUp ? 1f : -1f;
                        var room = drag > 0f ? ship.Y < DogfightSim.MaxAltitude : ship.Y > DogfightSim.MinAltitude;
                        if (room)
                        {
                            ship.TornadoDrag = drag;
                            events.Add(new DogfightEvent { Kind = DogfightEventKind.TornadoCaught, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
                        }
                    }
                    if (zone.Kind == SkyZoneKind.Toxic && Math.Abs(ship.Y - zone.Y) <= CloudHeight) ship.TakeDamage(rules.toxicPerSecond * DogfightSim.TickSeconds);
                }

                // Once caught, a ship goes all the way, even after it's been flung clear of the funnel.
                if (ship.TornadoDrag != 0f)
                {
                    ship.Y = DogfightSim.ClampAltitude(ship.Y + ship.TornadoDrag * TornadoDragSpeed * DogfightSim.TickSeconds);
                    if (ship.Y >= DogfightSim.MaxAltitude || ship.Y <= DogfightSim.MinAltitude) ship.TornadoDrag = 0f;
                }
            }
            if (lightningEvery <= 0f) return;

            nextStrike -= DogfightSim.TickSeconds;
            if (nextStrike <= 0f)
            {
                nextStrike = lightningEvery * random.Range(0.8f, 1.2f);
                var target = sim.Ships[LightningTargetId];
                var strike = new LightningStrike
                {
                    X = target.X + target.ForwardX * target.Speed * rules.lightningWarning + random.Range(-StrikeSpread, StrikeSpread),
                    Z = target.Z + target.ForwardZ * target.Speed * rules.lightningWarning + random.Range(-StrikeSpread, StrikeSpread),
                    Timer = rules.lightningWarning,
                };
                strikes.Add(strike);
                events.Add(new DogfightEvent { Kind = DogfightEventKind.LightningWarning, X = strike.X, Y = target.Y, Z = strike.Z });
            }

            for (var i = strikes.Count - 1; i >= 0; i--)
            {
                var strike = strikes[i];
                strike.Timer -= DogfightSim.TickSeconds;
                if (strike.Timer > 0f) continue;
                strikes.RemoveAt(i);
                events.Add(new DogfightEvent { Kind = DogfightEventKind.LightningStrike, X = strike.X, Z = strike.Z });
                foreach (var ship in sim.Ships)
                {
                    if (!ship.Alive || DogfightSim.DistanceSquared(ship.X, ship.Z, strike.X, strike.Z) > rules.lightningRadius * rules.lightningRadius) continue;
                    ship.TakeDamage(rules.lightningDamage);
                    events.Add(new DogfightEvent { Kind = DogfightEventKind.HazardHit, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z, Amount = rules.lightningDamage, HazardId = -1 });
                    if (!ship.Alive) events.Add(new DogfightEvent { Kind = DogfightEventKind.Destroyed, ShipId = ship.Id, X = ship.X, Y = ship.Y, Z = ship.Z });
                }
            }
        }
    }
}
