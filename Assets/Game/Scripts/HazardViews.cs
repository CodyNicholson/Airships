using System.Collections.Generic;
using Airships.Core;
using UnityEngine;

namespace Airships.Game
{
    // Draws the dogfight's hazards from their simulated state. Rocks and sea life are built in code,
    // and debris uses the kit's crates, barrels and cannons.
    public class HazardViews
    {
        static readonly string[] DebrisProps = { "SM_Prop_Crate", "SM_Prop_Barrel", "SM_Prop_Cannon", "SM_Prop_WreckChunk", "SM_Prop_ScrapPile" };
        const int RockVariants = 3;
        const float JawOpenDegrees = 38f;

        // Meshes are shared across dogfights, because they never change after being built.
        static readonly Mesh[] RockMeshes = new Mesh[RockVariants];
        static Mesh jellyfishMesh;
        static Mesh fishSchoolMesh;
        static Mesh whaleBodyMesh;
        static Mesh whaleJawMesh;
        static Mesh whaleTailMesh;

        class View
        {
            public HazardState State;
            public Transform Transform;
            public Vector3 Previous;
            public Vector3 TumbleAxis;
            public float TumbleSpeed;
        }

        readonly List<View> views = new List<View>();
        readonly Dictionary<int, View> byId = new Dictionary<int, View>();
        readonly Transform root;
        readonly HazardField field;
        Transform whale;
        Transform whaleJaw;
        Transform whaleTail;
        Vector3 whalePrevious;
        float mouth;
        float time;

        public Transform WhaleTransform => whale;

        public HazardViews(HazardField field, Transform parent)
        {
            this.field = field;
            root = new GameObject("Hazards").transform;
            root.SetParent(parent, false);

            var seaMaterial = Models.NewLitMaterial("Sea Life", -0.2f, 0.3f);
            var rockMaterial = Models.NewLitMaterial("Rocks");
            var debrisMaterial = KitArt.NewMaterial("Debris", KitArt.Crimson);
            foreach (var hazard in field.Hazards)
            {
                var view = new View { State = hazard, Previous = Position(hazard) };
                view.Transform = Build(hazard, seaMaterial, rockMaterial, debrisMaterial);
                view.Transform.position = view.Previous;
                if (hazard.Kind == HazardKind.Debris || hazard.Kind == HazardKind.Rock)
                {
                    view.TumbleAxis = new Vector3(Mathf.Sin(hazard.Id * 12.3f), Mathf.Cos(hazard.Id * 7.1f), Mathf.Sin(hazard.Id * 3.7f)).normalized;
                    view.TumbleSpeed = hazard.Kind == HazardKind.Debris ? 24f : 2.5f;
                }
                views.Add(view);
                byId[hazard.Id] = view;
            }

            if (field.Whale != null)
            {
                BuildWhale(seaMaterial);
                whalePrevious = WhalePosition();
                whale.position = whalePrevious;
            }
        }

        static Vector3 Position(HazardState hazard) => new Vector3(hazard.X, hazard.Y, hazard.Z);
        Vector3 WhalePosition() => new Vector3(field.Whale.X, field.Whale.Y, field.Whale.Z);

        Transform Build(HazardState hazard, Material sea, Material rock, Material debris)
        {
            switch (hazard.Kind)
            {
                case HazardKind.Rock: return BuildRock(hazard, rock);
                case HazardKind.Debris: return BuildDebris(hazard, debris);
                case HazardKind.Mine: return BuildMine(hazard);
                case HazardKind.Jellyfish: return BuildShared("Jellyfish", ref jellyfishMesh, BuildJellyfishMesh, hazard.Radius, sea);
                default: return BuildShared("Fish School", ref fishSchoolMesh, BuildFishSchoolMesh, hazard.Radius, sea);
            }
        }

        Transform BuildShared(string name, ref Mesh mesh, System.Func<Mesh> build, float radius, Material material)
        {
            if (mesh == null) mesh = build();
            var go = Models.Attach(name, root, mesh, material, false);
            go.transform.localScale = Vector3.one * radius;
            return go.transform;
        }

        Transform BuildMine(HazardState hazard)
        {
            var holder = new GameObject("Mine").transform;
            holder.SetParent(root, false);
            var mine = KitArt.Spawn("SM_Env_LiftMine", holder);
            KitArt.Paint(mine, KitArt.NewMaterial("Mine", "Scrap"));
            var scale = hazard.Radius * 2f / 18f;
            mine.transform.localScale = Vector3.one * scale;
            mine.transform.localPosition = Vector3.down * (9.6f * scale);
            return holder;
        }

        Transform BuildRock(HazardState hazard, Material material)
        {
            var variant = hazard.Id % RockVariants;
            if (RockMeshes[variant] == null)
            {
                var kit = new MeshKit();
                kit.Blob(Vector3.zero, Vector3.one, Palette.IslandRock, 1, 0.2f, 100 + variant);
                kit.Blob(new Vector3(0.35f, 0.45f, 0.2f), Vector3.one * 0.55f, Color.Lerp(Palette.IslandRock, Palette.Ink, 0.25f), 0, 0.15f, 200 + variant);
                RockMeshes[variant] = kit.Build("Rock");
            }
            var go = Models.Attach("Rock", root, RockMeshes[variant], material, false);
            go.transform.localScale = Vector3.one * hazard.Radius;
            go.transform.rotation = Quaternion.Euler(hazard.Id * 37f, hazard.Id * 91f, 0f);
            return go.transform;
        }

        Transform BuildDebris(HazardState hazard, Material material)
        {
            var holder = new GameObject("Debris").transform;
            holder.SetParent(root, false);
            var prop = KitArt.Spawn(DebrisProps[hazard.Id % DebrisProps.Length], holder);
            KitArt.Paint(prop, material);
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Scales the prop so what the player sees matches its collision size.
            var bounds = prop.GetComponent<MeshFilter>().sharedMesh.bounds;
            var scale = hazard.Radius * 2f / Mathf.Max(0.01f, bounds.size.magnitude);
            prop.transform.localScale = Vector3.one * scale;
            prop.transform.localPosition = -bounds.center * scale;
            return holder;
        }

        static Mesh BuildJellyfishMesh()
        {
            var kit = new MeshKit();
            var bell = Color.Lerp(Palette.Storm, Palette.Medic, 0.35f);
            kit.Blob(new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.7f, 1f), bell, 1, 0.04f, 7);
            kit.Blob(new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.4f, 0.6f), Palette.Mana, 1);
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI / 3f;
                var top = new Vector3(Mathf.Cos(angle) * 0.6f, -0.2f, Mathf.Sin(angle) * 0.6f);
                kit.Cylinder(top, top + new Vector3(Mathf.Cos(angle) * 0.15f, -1.1f, Mathf.Sin(angle) * 0.15f), 0.09f, 0.015f, 4, bell);
            }
            return kit.Build("Jellyfish");
        }

        static Mesh BuildFishSchoolMesh()
        {
            var kit = new MeshKit();
            var random = new System.Random(55);
            for (var i = 0; i < 9; i++)
            {
                var at = new Vector3((float)random.NextDouble() * 1.6f - 0.8f, (float)random.NextDouble() * 1.2f - 0.6f, (float)random.NextDouble() * 1.6f - 0.8f);
                var color = i % 3 == 0 ? Palette.Accent : Color.Lerp(Palette.Storm, Palette.Coat, 0.3f);
                kit.Blob(at, new Vector3(0.09f, 0.14f, 0.34f), color, 0);
                kit.Cylinder(at + Vector3.back * 0.28f, at + Vector3.back * 0.5f, 0f, 0.13f, 3, color);
            }
            return kit.Build("Fish School");
        }

        void BuildWhale(Material material)
        {
            var slate = Color.Lerp(Palette.Coat, Palette.DarkSmoke, 0.5f);
            var belly = Palette.Medic;
            if (whaleBodyMesh == null)
            {
                var kit = new MeshKit();
                kit.Blob(new Vector3(0f, 2f, -30f), new Vector3(15f, 14f, 34f), slate, 2, 0.02f, 11);
                kit.Blob(new Vector3(0f, -6f, -24f), new Vector3(12f, 8f, 26f), belly, 1);

                // The upper jaw, with teeth along the opening.
                kit.Blob(new Vector3(0f, 3f, -5f), new Vector3(13f, 7f, 12f), slate, 1);
                for (var i = -3; i <= 3; i++)
                {
                    var at = new Vector3(i * 3.2f, -1.5f, 3f - Mathf.Abs(i) * 1.5f);
                    kit.Cylinder(at + Vector3.up * 1.2f, at + Vector3.down * 2.2f, 0.8f, 0f, 4, Palette.Canvas);
                }
                for (var side = -1; side <= 1; side += 2)
                {
                    kit.Blob(new Vector3(side * 12.5f, 5f, -9f), new Vector3(1.8f, 2.2f, 2.2f), Palette.Canvas, 0);
                    kit.Blob(new Vector3(side * 13.2f, 5f, -8f), Vector3.one * 1.1f, Palette.Ink, 0);

                    // Pectoral fin, like a wing.
                    kit.Quad(new Vector3(side * 13f, -3f, -22f), new Vector3(side * 13f, -3f, -34f), new Vector3(side * 36f, -9f, -40f), new Vector3(side * 36f, -9f, -32f), slate, Vector3.up);
                    kit.Quad(new Vector3(side * 13f, -3f, -22f), new Vector3(side * 13f, -3f, -34f), new Vector3(side * 36f, -9f, -40f), new Vector3(side * 36f, -9f, -32f), slate, Vector3.down);
                }
                whaleBodyMesh = kit.Build("Whale Body");

                var jaw = new MeshKit();
                jaw.Blob(new Vector3(0f, -3f, -7f), new Vector3(12f, 3.5f, 14f), belly, 1);
                jaw.Blob(new Vector3(0f, -1.2f, -7f), new Vector3(9.5f, 1.4f, 11f), Palette.Danger, 1);
                for (var i = -3; i <= 3; i++)
                {
                    var at = new Vector3(i * 2.8f, -1f, 3f - Mathf.Abs(i) * 1.5f);
                    jaw.Cylinder(at + Vector3.down * 1f, at + Vector3.up * 1.8f, 0.7f, 0f, 4, Palette.Canvas);
                }
                whaleJawMesh = jaw.Build("Whale Jaw");

                var tail = new MeshKit();
                tail.Cylinder(Vector3.zero, new Vector3(0f, 0f, -16f), 8f, 3f, 8, slate);
                tail.Quad(new Vector3(-2f, 0f, -14f), new Vector3(2f, 0f, -14f), new Vector3(20f, 1f, -28f), new Vector3(-20f, 1f, -28f), slate, Vector3.up);
                tail.Quad(new Vector3(-2f, 0f, -14f), new Vector3(2f, 0f, -14f), new Vector3(20f, 1f, -28f), new Vector3(-20f, 1f, -28f), slate, Vector3.down);
                whaleTailMesh = tail.Build("Whale Tail");
            }

            whale = Models.Attach("Sky Whale", root, whaleBodyMesh, material, false).transform;
            whaleJaw = Models.Attach("Jaw", whale, whaleJawMesh, material, false).transform;
            whaleJaw.localPosition = new Vector3(0f, -1f, -10f);
            whaleTail = Models.Attach("Tail", whale, whaleTailMesh, material, false).transform;
            whaleTail.localPosition = new Vector3(0f, 2f, -60f);
        }

        // Call after each simulation step, so drawing can blend between steps.
        public void CapturePrevious()
        {
            foreach (var view in views) view.Previous = Position(view.State);
            if (field.Whale != null) whalePrevious = WhalePosition();
        }

        public void Hide(int hazardId)
        {
            if (byId.TryGetValue(hazardId, out var view)) view.Transform.gameObject.SetActive(false);
        }

        public void Update(float alpha)
        {
            time += Time.deltaTime;
            foreach (var view in views)
            {
                var hazard = view.State;
                if (!hazard.Alive) continue;
                view.Transform.position = Vector3.Lerp(view.Previous, Position(hazard), alpha);
                switch (hazard.Kind)
                {
                    case HazardKind.FishSchool:
                        view.Transform.rotation = Quaternion.Euler(0f, hazard.Heading * Mathf.Rad2Deg, 0f);
                        break;
                    case HazardKind.Mine:
                        view.Transform.Rotate(0f, 14f * Time.deltaTime, 0f, Space.World);
                        break;
                    case HazardKind.Jellyfish:
                        var pulse = 1f + Mathf.Sin(time * 2.2f + hazard.BobPhase) * 0.07f;
                        view.Transform.localScale = new Vector3(pulse, 2f - pulse, pulse) * hazard.Radius;
                        break;
                    default:
                        view.Transform.Rotate(view.TumbleAxis, view.TumbleSpeed * Time.deltaTime, Space.World);
                        break;
                }
            }

            if (whale == null) return;
            var state = field.Whale;
            whale.position = Vector3.Lerp(whalePrevious, WhalePosition(), alpha);
            var direction = new Vector3(state.DirX, state.DirY, state.DirZ);
            if (direction.sqrMagnitude > 0.001f) whale.rotation = Quaternion.Slerp(whale.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-6f * Time.deltaTime));
            mouth = Mathf.MoveTowards(mouth, state.MouthOpen ? 1f : 0f, Time.deltaTime * 2.2f);
            whaleJaw.localRotation = Quaternion.Euler(mouth * JawOpenDegrees, 0f, 0f);
            whaleTail.localRotation = Quaternion.Euler(Mathf.Sin(time * (state.Mode == WhaleMode.Lunging ? 7f : 1.6f)) * 16f, 0f, 0f);
        }
    }

    // Draws cloud banks, wind currents, updrafts, toxic smog and lightning with the kit's hazard models.
    public class SkyZoneViews
    {
        const float BoltSeconds = 0.22f;

        readonly SkyWeather weather;
        readonly Transform root;
        readonly List<Transform> updrafts = new List<Transform>();
        readonly Material zoneMaterial;
        GameObject bolt;
        float boltTimer;

        public SkyZoneViews(SkyWeather weather, Transform parent, bool showWind, bool stormy)
        {
            this.weather = weather;
            root = new GameObject("Sky Zones").transform;
            root.SetParent(parent, false);
            zoneMaterial = KitArt.NewMaterial("Zones", KitArt.Crimson, -0.25f, 0.45f);

            foreach (var zone in weather.Zones)
            {
                switch (zone.Kind)
                {
                    case SkyZoneKind.Cloud:
                        Place(stormy ? "SM_Env_StormCloud" : "SM_Env_Cloud", zone, zone.Radius / 6.3f, zone.Y - 14f);
                        break;
                    case SkyZoneKind.Toxic:
                        var cloud = Place("SM_Env_ToxicCloud", zone, zone.Radius / 6.2f, zone.Y - 8f);
                        var smog = KitArt.Socket(cloud, "centre");
                        if (smog != null) Effects.Attach("SmogVent", smog, 2.4f).Active = true;
                        break;
                    case SkyZoneKind.Wind:
                        if (showWind) PlaceWind(zone);
                        break;
                    case SkyZoneKind.Updraft:
                        var column = Place("SM_Env_UpdraftColumn", zone, zone.Radius / 9.5f, -50f);
                        updrafts.Add(column.transform);
                        break;
                }
            }
        }

        GameObject Place(string asset, SkyZone zone, float scale, float y)
        {
            var kit = KitArt.Spawn(asset, root);
            KitArt.Paint(kit, zoneMaterial);
            kit.transform.position = new Vector3(zone.X, y, zone.Z);
            kit.transform.localScale = Vector3.one * scale;
            foreach (var renderer in kit.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return kit;
        }

        // A ribbon along the current, once Lyra has shown the player where they run.
        void PlaceWind(SkyZone zone)
        {
            var kit = Place("SM_Env_WindCurrent", zone, 1f, 0f);
            kit.transform.localScale = new Vector3(zone.Radius / 8.5f, 1.6f, zone.Radius * 2f / 60f);
            kit.transform.rotation = Quaternion.Euler(0f, zone.Heading * Mathf.Rad2Deg, 0f);
            kit.transform.position = new Vector3(zone.X, 2f, zone.Z);
        }

        public void ShowWarning(Vector3 position, float radius)
        {
            for (var i = 0; i < 16; i++)
            {
                var angle = i / 16f * Mathf.PI * 2f;
                Fx.Burst(position + new Vector3(Mathf.Cos(angle) * radius, 3f, Mathf.Sin(angle) * radius), Palette.Cloud, 1, 0.5f, 2.2f, 1.5f);
            }
        }

        // The bolt model shows for a few frames, with the flash from the flipbook.
        public void Strike(Vector3 position)
        {
            if (bolt == null)
            {
                bolt = KitArt.Spawn("SM_Env_LightningBolt", root);
                KitArt.Paint(bolt, zoneMaterial);
            }
            bolt.SetActive(true);
            bolt.transform.position = position + Vector3.down * 6f;
            bolt.transform.localScale = Vector3.one * 0.9f;
            bolt.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            boltTimer = BoltSeconds;
            Effects.Play("LightningStrike", position + Vector3.up * 30f, 1.2f);
            Fx.Burst(position, Palette.Storm, 18, 18f, 2.4f, 0.5f);
        }

        public void Update(ShipState player)
        {
            foreach (var column in updrafts) column.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            if (bolt == null || boltTimer <= 0f) return;
            boltTimer -= Time.deltaTime;
            if (boltTimer <= 0f) bolt.SetActive(false);
        }
    }
}
