using System.Collections.Generic;
using UnityEngine;

namespace Airships.Game
{
    public enum ShipHull { Sloop, Brigantine, Galleon }

    public class ShipModel
    {
        public GameObject Root;
        public Transform Body;
        public Transform[] Propellers;
        public Material Material;
    }

    public class CharacterModel
    {
        public GameObject Root;
        public KitCharacter Animation;
        public float HeadHeight;
        public List<Material> Materials = new List<Material>();

        public void SetEmission(Color color)
        {
            foreach (var material in Materials) material.SetColor("_Emission", color);
        }
    }

    public class DeckModel
    {
        public GameObject Root;
        public GameObject Kit;
        public float SurfaceHeight;

        // A named socket's position in the root's space, with the deck surface at height zero.
        public Vector3 Spot(string socket) => Root.transform.InverseTransformPoint(KitArt.Socket(Kit, socket).position);
    }

    // Builds game models from the Blender kit, plus a few code-built pieces for roles the kit lacks.
    public static class Models
    {
        // The kit's ships are real-world sizes, and these match them to the dogfight's distances.
        const float SloopScale = 2f;
        const float BrigantineScale = 1.4f;
        const float GalleonScale = 1.1f;

        // Lowers ships so shots, which fly at a fixed height, meet the hull rather than the keel.
        const float ShipDrop = 1.5f;

        // Fits the older accessories, drawn for a much larger head, onto kit characters.
        const float AccessoryScale = 0.42f;
        const float AccessoryBaseY = 1.9f;
        const float HatSink = 0.08f;

        static Shader litShader;

        public static Material NewLitMaterial(string name, float bandEdge = 0.1f, float bandSoftness = 0.08f)
        {
            if (litShader == null) litShader = Resources.Load<Shader>("Shaders/StylizedLit");
            var material = new Material(litShader) { name = name };
            material.SetFloat("_BandEdge", bandEdge);
            material.SetFloat("_BandSoftness", bandSoftness);
            return material;
        }

        public static GameObject Attach(string name, Transform parent, Mesh mesh, Material material, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        public static ShipModel CreateShip(string name, ShipHull hull, string faction, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            var model = KitArt.Spawn("SM_Ship_" + hull, body);
            model.transform.localPosition = Vector3.down * ShipDrop;
            model.transform.localScale = Vector3.one * (hull == ShipHull.Sloop ? SloopScale : hull == ShipHull.Brigantine ? BrigantineScale : GalleonScale);

            // The propeller's pivot is at its base, so each spins under a parent placed at its hub.
            var hubs = new List<Transform>();
            foreach (Transform socket in model.transform)
            {
                if (!socket.name.StartsWith(KitArt.SocketPrefix + "propeller_")) continue;
                var hub = new GameObject("Propeller Hub").transform;
                hub.SetParent(socket, false);
                var propeller = KitArt.Spawn("SM_Ship_Propeller", hub);
                propeller.transform.localPosition = -KitArt.Socket(propeller, "hub").localPosition;
                hubs.Add(hub);
            }

            // A low band edge keeps most of the ship in the sun, so sails stay bright when back-lit.
            var material = KitArt.NewMaterial(name + " Paint", faction, -0.45f, 0.2f);
            KitArt.Paint(model, material);
            return new ShipModel { Root = root, Body = body, Propellers = hubs.ToArray(), Material = material };
        }

        public static CharacterModel CreateCharacter(string name, string classId, Transform parent)
        {
            switch (classId)
            {
                case "buccaneer": return Character(name, "SK_PirateCaptain", KitArt.Crimson, parent, "SM_Prop_Cutlass", "weapon.R");
                case "gunner": return Character(name, "SK_PirateGunner", KitArt.Crimson, parent, "SM_Prop_Flintlock", "gun.R");
                case "stormcaller":
                    var caller = Character(name, "SK_PirateDeckhand", KitArt.Azure, parent);
                    AddHat(caller, kit => AddWizardHat(kit, Palette.Coat, Palette.Storm));
                    AddHeld(caller, "weapon.R", BuildStaff(), new Vector3(0f, -0.3f, 0f));
                    return caller;
                case "surgeon":
                    var surgeon = Character(name, "SK_PirateDeckhand", KitArt.Emerald, parent, "SM_Prop_RumBottle", "weapon.R");
                    AddHat(surgeon, kit => AddCap(kit, Palette.Herb));
                    return surgeon;
                case "pirate_captain":
                    var captain = Character(name, "SK_PirateBrute", KitArt.Obsidian, parent, "SM_Prop_BoardingAxe", "weapon.R");
                    AddHat(captain, kit => AddTricorn(kit, Palette.Ink, Palette.Danger));
                    return captain;
                case "bombardier":
                    var bombardier = Character(name, "SK_PirateGunner", KitArt.Obsidian, parent);
                    AddHeld(bombardier, "gun.R", BuildBomb(), Vector3.zero);
                    return bombardier;
                default: return Character(name, "SK_PirateDeckhand", KitArt.Obsidian, parent, "SM_Prop_Cutlass", "weapon.R");
            }
        }

        static CharacterModel Character(string name, string asset, string faction, Transform parent, string heldProp = null, string bone = null)
        {
            var model = KitArt.Spawn(asset, parent);
            model.name = name;
            var material = KitArt.NewMaterial(name + " Paint", faction);
            var character = new CharacterModel
            {
                Root = model,
                Animation = model.GetComponent<KitCharacter>(),
                HeadHeight = KitArt.Socket(model, "head_top").localPosition.y,
            };
            character.Materials.Add(material);

            if (heldProp != null)
            {
                // The kit's rule: no rotation offset, and a local position of the negative grip.
                var prop = KitArt.Spawn(heldProp, KitArt.FindDeep(model.transform, bone));
                var grip = KitArt.Socket(prop, "grip");
                if (grip != null) prop.transform.localPosition = -grip.localPosition;
            }
            KitArt.Paint(model, material);
            return character;
        }

        static void AddHat(CharacterModel character, System.Action<MeshKit> build)
        {
            var kit = new MeshKit { Transform = Matrix4x4.Scale(Vector3.one * AccessoryScale) * Matrix4x4.Translate(Vector3.down * AccessoryBaseY) };
            build(kit);
            var root = character.Root.transform;
            var hat = Attach("Hat", root, kit.Build("Hat"), AccessoryMaterial(character));
            var top = KitArt.Socket(character.Root, "head_top").localPosition;
            hat.transform.localPosition = top + Vector3.down * HatSink;

            // Placed in the bind pose first, then carried by the head bone.
            hat.transform.SetParent(KitArt.FindDeep(root, "head"), true);
        }

        static void AddHeld(CharacterModel character, string bone, Mesh mesh, Vector3 localPosition)
        {
            var held = Attach("Held", KitArt.FindDeep(character.Root.transform, bone), mesh, AccessoryMaterial(character));
            held.transform.localPosition = localPosition;
        }

        static Material AccessoryMaterial(CharacterModel character)
        {
            if (character.Materials.Count > 1) return character.Materials[1];

            // Code-built meshes carry vertex colors and no palette, so they use the white texture.
            var material = NewLitMaterial(character.Root.name + " Accessories");
            character.Materials.Add(material);
            return material;
        }

        static void AddTricorn(MeshKit kit, Color hat, Color band)
        {
            kit.Box(new Vector3(0f, 1.9f, 0f), new Vector3(1.28f, 0.1f, 0.95f), hat);
            kit.Box(new Vector3(0f, 2.1f, -0.02f), new Vector3(0.68f, 0.34f, 0.56f), hat, 0.82f);
            kit.Box(new Vector3(0f, 1.98f, 0.28f), new Vector3(0.72f, 0.1f, 0.04f), band);
            for (var side = -1; side <= 1; side += 2)
            {
                kit.Box(new Vector3(side * 0.62f, 2.02f, 0f), new Vector3(0.18f, 0.26f, 0.9f), hat, 0.7f);
            }
        }

        static void AddWizardHat(MeshKit kit, Color hat, Color trim)
        {
            kit.Cylinder(new Vector3(0f, 1.9f, 0f), new Vector3(0f, 1.98f, 0f), 0.68f, 0.64f, 8, trim);
            kit.Cylinder(new Vector3(0f, 1.98f, 0f), new Vector3(0.08f, 2.95f, -0.16f), 0.42f, 0.02f, 8, hat);
        }

        static void AddCap(MeshKit kit, Color cap)
        {
            kit.Cylinder(new Vector3(0f, 1.9f, 0f), new Vector3(0f, 2.16f, 0f), 0.44f, 0.5f, 8, cap);
            kit.Box(new Vector3(0f, 2.05f, 0.47f), new Vector3(0.09f, 0.26f, 0.03f), Palette.Medic);
            kit.Box(new Vector3(0f, 2.05f, 0.47f), new Vector3(0.26f, 0.09f, 0.03f), Palette.Medic);
        }

        static Mesh BuildStaff()
        {
            var kit = new MeshKit();
            kit.Cylinder(Vector3.zero, new Vector3(0f, 1.25f, 0f), 0.025f, 0.03f, 6, Palette.DarkWood);
            kit.Blob(new Vector3(0f, 1.34f, 0f), Vector3.one * 0.11f, Palette.Storm, 0);
            return kit.Build("Staff");
        }

        static Mesh BuildBomb()
        {
            var kit = new MeshKit();
            kit.Blob(Vector3.zero, Vector3.one * 0.14f, Palette.Ink, 1);
            kit.Cylinder(new Vector3(0f, 0.12f, 0f), new Vector3(0.04f, 0.22f, 0f), 0.015f, 0.015f, 4, Palette.Spark);
            return kit.Build("Bomb");
        }

        // The boarding battle stage. The kit's deck faces its intended camera, so it turns to face ours.
        public static DeckModel CreateDeck(Transform parent, string faction)
        {
            var root = new GameObject("Boarding Deck");
            root.transform.SetParent(parent, false);
            var model = KitArt.Spawn("SM_Env_BoardingDeck", root.transform);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var surface = KitArt.Socket(model, "centre_stage").localPosition.y;
            model.transform.localPosition = Vector3.down * surface;
            KitArt.Paint(model, KitArt.NewMaterial("Deck Paint", faction));
            return new DeckModel { Root = root, Kit = model, SurfaceHeight = surface };
        }

        // A kit prop with its own material instance, for rewards and set dressing.
        public static GameObject CreateProp(string asset, string faction, Transform parent)
        {
            var prop = KitArt.Spawn(asset, parent);
            KitArt.Paint(prop, KitArt.NewMaterial(asset + " Paint", faction));
            return prop;
        }
    }
}
