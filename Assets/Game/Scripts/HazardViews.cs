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
        const float JawOpenDegrees = 55f;
        const float JawOpenRate = 3.5f;
        const float JawCloseRate = 2.2f;
        const float NotHeld = -1f;
        const float JawLength = 17f; // From the hinge to the front teeth.
        const float GapeLift = 14f; // The head tips back as the jaw drops.
        const int JellyBolts = 5;

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
            public SparkCluster Sparks; // Jellyfish only: the current crackling over the bell.
        }

        readonly List<View> views = new List<View>();
        readonly Dictionary<int, View> byId = new Dictionary<int, View>();
        readonly Transform root;
        readonly HazardField field;
        class WhaleView
        {
            public WhaleState State;
            public Transform Body;
            public Transform Jaw;
            public Transform Tail;
            public Vector3 Previous;
            public float Mouth;
            public float HeldMouth = NotHeld; // A swallow takes over the jaw from the hunt.
            public float HeldRate;
            public float Bulge;
            public Vector3 Glide;
        }

        readonly List<WhaleView> whales = new List<WhaleView>();
        float time;

        // The whale nearest a point, such as the one that just swallowed a ship.
        WhaleView ViewOf(Transform body) => whales.Find(view => view.Body == body);

        // Sets the jaw directly: 0 shut, 1 wide open.
        public void HoldMouth(Transform body, float open, float rate)
        {
            var view = ViewOf(body);
            view.HeldMouth = open;
            view.HeldRate = rate;
        }

        // A swallow pushing through the body, and the whale swimming off afterward.
        public void Gulp(Transform body, float bulge, Vector3 glide)
        {
            var view = ViewOf(body);
            view.Bulge = bulge;
            view.Glide = glide;
        }

        public Transform WhaleNearest(Vector3 point)
        {
            Transform nearest = null;
            var best = float.MaxValue;
            foreach (var view in whales)
            {
                var distance = (view.Body.position - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = view.Body;
            }
            return nearest;
        }

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
                if (hazard.Kind == HazardKind.Jellyfish) view.Sparks = new SparkCluster(view.Transform, JellyBolts);
                views.Add(view);
                byId[hazard.Id] = view;
            }

            foreach (var state in field.Whales) BuildWhale(state, seaMaterial);
        }

        static Vector3 Position(HazardState hazard) => new Vector3(hazard.X, hazard.Y, hazard.Z);
        static Vector3 Position(WhaleState whale) => new Vector3(whale.X, whale.Y, whale.Z);

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

        void BuildWhale(WhaleState state, Material material)
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
                kit.Blob(new Vector3(0f, -1f, -12f), new Vector3(11f, 5f, 12f), Palette.Ink, 1); // The throat, seen when it gapes.
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
                jaw.Blob(new Vector3(0f, -3f, 7f), new Vector3(12f, 3.5f, 14f), belly, 1);
                jaw.Blob(new Vector3(0f, -1.2f, 7f), new Vector3(9.5f, 1.4f, 11f), Palette.Danger, 1);
                for (var i = -3; i <= 3; i++)
                {
                    var at = new Vector3(i * 2.8f, -1f, JawLength - Mathf.Abs(i) * 1.5f);
                    jaw.Cylinder(at + Vector3.down * 1f, at + Vector3.up * 1.8f, 0.7f, 0f, 4, Palette.Canvas);
                }
                whaleJawMesh = jaw.Build("Whale Jaw");

                var tail = new MeshKit();
                tail.Cylinder(Vector3.zero, new Vector3(0f, 0f, -16f), 8f, 3f, 8, slate);
                tail.Quad(new Vector3(-2f, 0f, -14f), new Vector3(2f, 0f, -14f), new Vector3(20f, 1f, -28f), new Vector3(-20f, 1f, -28f), slate, Vector3.up);
                tail.Quad(new Vector3(-2f, 0f, -14f), new Vector3(2f, 0f, -14f), new Vector3(20f, 1f, -28f), new Vector3(-20f, 1f, -28f), slate, Vector3.down);
                whaleTailMesh = tail.Build("Whale Tail");
            }

            var body = Models.Attach("Sky Whale", root, whaleBodyMesh, material, false).transform;
            var mouthJaw = Models.Attach("Jaw", body, whaleJawMesh, material, false).transform;
            mouthJaw.localPosition = new Vector3(0f, -1f, -24f);
            var tailFin = Models.Attach("Tail", body, whaleTailMesh, material, false).transform;
            tailFin.localPosition = new Vector3(0f, 2f, -60f);
            var previous = Position(state);
            body.position = previous;
            whales.Add(new WhaleView { State = state, Body = body, Jaw = mouthJaw, Tail = tailFin, Previous = previous });
        }

        // Call after each simulation step, so drawing can blend between steps.
        public void CapturePrevious()
        {
            foreach (var view in views) view.Previous = Position(view.State);
            foreach (var whale in whales) whale.Previous = Position(whale.State);
        }

        public void Hide(int hazardId)
        {
            if (byId.TryGetValue(hazardId, out var view)) view.Transform.gameObject.SetActive(false);
        }

        public void Update(float alpha, Vector3 prey)
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
                        var pulse = 1f + Mathf.Sin(time * 2.2f + hazard.BobPhase) * 0.14f;
                        view.Transform.localScale = new Vector3(pulse, 2f - pulse, pulse) * hazard.Radius;

                        // In the jellyfish's own space, so the arcs size and squeeze with the bell.
                        view.Sparks.Update(new Vector3(0f, 0.1f, 0f), new Vector3(0.85f, 0.6f, 0.85f), 0.45f, 0.95f, 0.6f);
                        break;
                    default:
                        view.Transform.Rotate(view.TumbleAxis, view.TumbleSpeed * Time.deltaTime, Space.World);
                        break;
                }
            }

            foreach (var whale in whales)
            {
                var state = whale.State;
                var at = Vector3.Lerp(whale.Previous, Position(state), alpha);
                whale.Body.position = at + whale.Glide;
                whale.Body.localScale = new Vector3(1f + whale.Bulge, 1f + whale.Bulge, 1f - whale.Bulge * 0.4f);
                var direction = new Vector3(state.DirX, state.DirY, state.DirZ);
                if (direction.sqrMagnitude > 0.001f) whale.Body.rotation = Quaternion.Slerp(whale.Body.rotation, Quaternion.LookRotation(direction) * Quaternion.Euler(-whale.Mouth * GapeLift, 0f, 0f), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
                if (whale.HeldMouth != NotHeld)
                {
                    whale.Mouth = Mathf.MoveTowards(whale.Mouth, whale.HeldMouth, Time.unscaledDeltaTime * whale.HeldRate);
                }
                else
                {
                    var gape = state.Gape(Vector3.Distance(at, prey));
                    whale.Mouth = Mathf.MoveTowards(whale.Mouth, gape, Time.deltaTime * (gape > whale.Mouth ? JawOpenRate : JawCloseRate));
                }
                whale.Jaw.localRotation = Quaternion.Euler(whale.Mouth * JawOpenDegrees, 0f, 0f);
                whale.Tail.localRotation = Quaternion.Euler(Mathf.Sin(time * (state.Mode == WhaleMode.Lunging ? 7f : 1.6f)) * 16f, 0f, 0f);
            }
        }
    }

    // Draws cloud banks, wind tunnels, tornadoes, toxic smog and lightning with the kit's hazard models.
    public class SkyZoneViews
    {
        const float BoltSeconds = 0.22f;
        const int TunnelHoops = 4;
        const float HoopFlowSpeed = 45f;
        const float StrandSpinDegrees = 50f;
        const float MouthFlare = 1.12f;

        static Mesh hoopMesh;
        static Mesh mouthMesh;
        static Mesh strandsMesh;

        class Tunnel
        {
            public SkyZone Zone;
            public Transform Strands;
            public Transform[] Hoops;
            public float Flow;
        }

        readonly List<Tunnel> tunnels = new List<Tunnel>();
        Material tunnelMaterial;
        float tornadoFlow;

        readonly SkyWeather weather;
        readonly Transform root;
        const float TornadoBase = -50f;
        const int TornadoHoops = 4;
        const float TornadoHoopSpeed = 38f;
        const float TornadoHoopHigh = 75f;
        const float TornadoHoopLow = -55f;
        static readonly Color DownTornadoGrey = new Color(0.38f, 0.38f, 0.41f);
        static Mesh downHoopMesh;
        Material downTornadoMaterial;
        readonly List<(Transform hoop, SkyZone zone, float direction, float offset)> tornadoHoops = new List<(Transform, SkyZone, float, float)>();
        const float TornadoSpinDegrees = 40f;
        readonly List<(Transform funnel, float spin)> tornadoes = new List<(Transform, float)>();
        readonly Material zoneMaterial;
        GameObject bolt;
        float boltTimer;

        public SkyZoneViews(SkyWeather weather, Transform parent, bool stormy)
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
                        PlaceTunnel(zone);
                        break;
                    case SkyZoneKind.TornadoUp:
                        tornadoes.Add((Place("SM_Env_UpdraftColumn", zone, zone.Radius / 9.5f, TornadoBase).transform, 1f));
                        AddTornadoHoops(zone, 1f);
                        break;
                    case SkyZoneKind.TornadoDown:
                        tornadoes.Add((PlaceUpsideDownTornado(zone), -1f));
                        AddTornadoHoops(zone, -1f);
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

        // The same funnel flipped over, filling the same stretch of sky, with its wide end at the bottom.
        Transform PlaceUpsideDownTornado(SkyZone zone)
        {
            var kit = Place("SM_Env_UpdraftColumn", zone, zone.Radius / 9.5f, TornadoBase);
            if (downTornadoMaterial == null)
            {
                downTornadoMaterial = new Material(zoneMaterial) { name = "Upside-Down Tornado" };
                downTornadoMaterial.SetColor("_BaseColor", DownTornadoGrey);
                downTornadoMaterial.SetTexture("_GlowMap", Texture2D.blackTexture); // Glow would wash the grey out.
            }
            KitArt.Paint(kit, downTornadoMaterial);
            var before = MiddleHeight(kit);
            kit.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            kit.transform.position += Vector3.up * (before - MiddleHeight(kit));
            return kit.transform;
        }

        // Rings rising through a tornado, or sinking through an upside-down one, show which way it carries you.
        void AddTornadoHoops(SkyZone zone, float direction)
        {
            BuildHoopMeshes();
            if (tunnelMaterial == null) tunnelMaterial = Models.NewLitMaterial("Wind Tunnel", -0.6f, 0.3f);
            for (var i = 0; i < TornadoHoops; i++)
            {
                var hoop = Models.Attach("Tornado Hoop", root, direction > 0f ? hoopMesh : downHoopMesh, tunnelMaterial, false).transform;
                hoop.rotation = Quaternion.Euler(90f, 0f, 0f);
                hoop.localScale = Vector3.one * zone.Radius;
                tornadoHoops.Add((hoop, zone, direction, i / (float)TornadoHoops));
            }
        }

        static void BuildHoopMeshes()
        {
            if (hoopMesh != null) return;
            var hoop = new MeshKit();
            AddHoop(hoop, 0f, 0.022f, Palette.Storm);
            hoopMesh = hoop.Build("Wind Tunnel Hoop");
            var down = new MeshKit();
            AddHoop(down, 0f, 0.022f, DownTornadoGrey);
            downHoopMesh = down.Build("Down Tornado Hoop");
            var mouth = new MeshKit();
            AddHoop(mouth, 0f, 0.05f, Palette.Cloud);
            mouthMesh = mouth.Build("Wind Tunnel Mouth");
            strandsMesh = BuildStrands();
        }

        static float MiddleHeight(GameObject kit)
        {
            var renderers = kit.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds.center.y;
        }

        // A ring of the given thickness, radius 1, facing along Z.
        static void AddHoop(MeshKit kit, float z, float thickness, Color color)
        {
            const int Segments = 24;
            for (var i = 0; i < Segments; i++)
            {
                var from = i / (float)Segments * Mathf.PI * 2f;
                var to = (i + 1) / (float)Segments * Mathf.PI * 2f;
                kit.Cylinder(new Vector3(Mathf.Cos(from), Mathf.Sin(from), z), new Vector3(Mathf.Cos(to), Mathf.Sin(to), z), thickness, thickness, 5, color);
            }
        }

        // Spiral strands running the tube's length, radius 1 and length 1 centered on the origin.
        static Mesh BuildStrands()
        {
            const int Strands = 6;
            const int Steps = 28;
            var kit = new MeshKit();
            for (var strand = 0; strand < Strands; strand++)
            {
                var start = strand / (float)Strands * Mathf.PI * 2f;
                for (var step = 0; step < Steps; step++)
                {
                    var a = step / (float)Steps;
                    var b = (step + 1) / (float)Steps;
                    var from = new Vector3(Mathf.Cos(start + a * Mathf.PI), Mathf.Sin(start + a * Mathf.PI), a - 0.5f);
                    var to = new Vector3(Mathf.Cos(start + b * Mathf.PI), Mathf.Sin(start + b * Mathf.PI), b - 0.5f);
                    kit.Cylinder(from, to, 0.009f, 0.009f, 4, Color.Lerp(Palette.Storm, Palette.Cloud, 0.6f));
                }
            }
            return kit.Build("Wind Tunnel Strands");
        }

        // A round tube of hoops and spiral strands at the tunnel's height, flowing the way it blows.
        void PlaceTunnel(SkyZone zone)
        {
            BuildHoopMeshes();
            if (tunnelMaterial == null) tunnelMaterial = Models.NewLitMaterial("Wind Tunnel", -0.6f, 0.3f);

            var body = new GameObject("Wind Tunnel").transform;
            body.SetParent(root, false);
            body.SetPositionAndRotation(new Vector3(zone.X, zone.Y, zone.Z), Quaternion.Euler(0f, zone.Heading * Mathf.Rad2Deg, 0f));

            Transform Part(string name, Mesh mesh, float z, Vector3 scale)
            {
                var part = Models.Attach(name, body, mesh, tunnelMaterial, false).transform;
                part.localPosition = new Vector3(0f, 0f, z);
                part.localScale = scale;
                foreach (var renderer in part.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return part;
            }

            var ring = Vector3.one * zone.Radius;
            Part("Entry", mouthMesh, -zone.Length * 0.5f, ring * MouthFlare);
            Part("Exit", mouthMesh, zone.Length * 0.5f, ring * MouthFlare);
            var tunnel = new Tunnel
            {
                Zone = zone,
                Strands = Part("Strands", strandsMesh, 0f, new Vector3(zone.Radius, zone.Radius, zone.Length)),
                Hoops = new Transform[TunnelHoops],
            };
            for (var i = 0; i < TunnelHoops; i++) tunnel.Hoops[i] = Part("Hoop", hoopMesh, 0f, ring);
            tunnels.Add(tunnel);
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
            foreach (var (funnel, spin) in tornadoes) funnel.Rotate(0f, spin * TornadoSpinDegrees * Time.deltaTime, 0f, Space.World);
            tornadoFlow += TornadoHoopSpeed * Time.deltaTime;
            foreach (var (hoop, zone, direction, offset) in tornadoHoops)
            {
                var span = TornadoHoopHigh - TornadoHoopLow;
                var travelled = Mathf.Repeat(tornadoFlow + offset * span, span);
                var height = direction > 0f ? TornadoHoopLow + travelled : TornadoHoopHigh - travelled;
                hoop.position = new Vector3(zone.X, height, zone.Z);
            }
            foreach (var tunnel in tunnels)
            {
                tunnel.Strands.Rotate(0f, 0f, StrandSpinDegrees * Time.deltaTime, Space.Self);
                tunnel.Flow = Mathf.Repeat(tunnel.Flow + HoopFlowSpeed * Time.deltaTime, tunnel.Zone.Length);
                for (var i = 0; i < tunnel.Hoops.Length; i++)
                {
                    var along = Mathf.Repeat(tunnel.Flow + i * tunnel.Zone.Length / tunnel.Hoops.Length, tunnel.Zone.Length);
                    tunnel.Hoops[i].localPosition = new Vector3(0f, 0f, along - tunnel.Zone.Length * 0.5f);
                }
            }
            if (bolt == null || boltTimer <= 0f) return;
            boltTimer -= Time.deltaTime;
            if (boltTimer <= 0f) bolt.SetActive(false);
        }
    }
}
