using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public enum SkyZoneKind { Wind, Cloud, Updraft, Toxic }

    public class SkyZone
    {
        public SkyZoneKind Kind;
        public float X;
        public float Y;
        public float Z;
        public float Radius;
        public float Heading; // The way a wind current blows.
        public float Strength;
    }

    public class LightningStrike
    {
        public float X;
        public float Z;
        public float Timer;
    }

    // Wind currents, cloud banks, updrafts and lightning for one fight. Deterministic from its seed.
    public class SkyWeather
    {
        const float WindAngle = 1.05f; // Radians either side of the wind where a ship still rides it.
        const float WindRadius = 62f;
        const float CloudRadius = 58f;
        const float CloudHeight = 22f;
        const float UpdraftRadius = 44f;
        const float UpdraftStrength = 9f;
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

        public SkyWeather(WeatherDef def, SkyRulesDef rules, int seed)
        {
            this.rules = rules;
            random = new GameRandom(seed);
            lightningEvery = def.lightningEvery;
            nextStrike = lightningEvery;
            AddZones(SkyZoneKind.Wind, def.windCount, WindRadius);
            AddZones(SkyZoneKind.Cloud, def.cloudCount, CloudRadius);
            AddZones(SkyZoneKind.Updraft, def.updraftCount, UpdraftRadius);
            AddZones(SkyZoneKind.Toxic, def.toxicCount, ToxicRadius);
        }

        void AddZones(SkyZoneKind kind, int count, float radius)
        {
            for (var i = 0; i < count; i++)
            {
                // Evenly around the arena with some wobble, so a zone is always within reach.
                var angle = (i + random.Range(0f, 0.7f)) / Math.Max(1, count) * (float)Math.PI * 2f;
                var distance = random.Range(0.3f, 1f) * DogfightSim.ArenaRadius * ZoneSpread;
                zones.Add(new SkyZone
                {
                    Kind = kind,
                    X = (float)Math.Sin(angle) * distance,
                    Z = (float)Math.Cos(angle) * distance,
                    Y = kind == SkyZoneKind.Cloud || kind == SkyZoneKind.Toxic ? random.Range(-12f, 22f) : 0f,
                    Radius = radius,
                    Heading = random.Range(0f, (float)Math.PI * 2f),
                    Strength = kind == SkyZoneKind.Updraft ? UpdraftStrength : 1f,
                });
            }
        }

        static bool Inside(ShipState ship, SkyZone zone)
        {
            return DogfightSim.DistanceSquared(ship.X, ship.Z, zone.X, zone.Z) <= zone.Radius * zone.Radius;
        }

        // Flying with a wind current multiplies airspeed. Flying against one does nothing.
        public float WindScale(ShipState ship, float windSpeedScale)
        {
            foreach (var zone in zones)
            {
                if (zone.Kind != SkyZoneKind.Wind || !Inside(ship, zone)) continue;
                if (Math.Abs(DogfightSim.WrapAngle(zone.Heading - ship.Heading)) <= WindAngle) return windSpeedScale;
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
                    if (!Inside(ship, zone)) continue;
                    if (zone.Kind == SkyZoneKind.Cloud && Math.Abs(ship.Y - zone.Y) <= CloudHeight) ship.Hidden = true;
                    if (zone.Kind == SkyZoneKind.Updraft) ship.Y = DogfightSim.ClampAltitude(ship.Y + zone.Strength * DogfightSim.TickSeconds);
                    if (zone.Kind == SkyZoneKind.Toxic && Math.Abs(ship.Y - zone.Y) <= CloudHeight) ship.TakeDamage(rules.toxicPerSecond * DogfightSim.TickSeconds);
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
