using Airships.Core;
using UnityEngine;

namespace Airships.Game
{
    // Distant set pieces that sell where a fight is happening. They're scenery, so nothing here affects play.
    public static class SkyScenery
    {
        const float UndertowDepth = -190f;
        const float UndertowScale = 3f;
        const float StormWallScale = 2.8f;
        const int StormWallSegments = 12;
        const float SegmentDegrees = 30f;

        public static void Build(string weather, string backdrop, Transform parent)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);

            switch (weather)
            {
                case "heist":
                    Place("SM_Env_SkyVault", "Gilded", root, new Vector3(230f, -70f, 340f), 6f, 200f);
                    Undertow(root);
                    break;
                case "scrapwinds":
                    Place("SM_Env_ScrapIsle", "Scrap", root, new Vector3(-360f, -90f, 240f), 6f, 40f);
                    Place("SM_Env_ScrapIsle", "Scrap", root, new Vector3(390f, -110f, -130f), 7f, 160f);
                    Place("SM_Env_LiftMine", "Scrap", root, new Vector3(330f, -40f, 310f), 5f, 80f);
                    Place("SM_Env_DebrisField", "Scrap", root, new Vector3(-250f, -30f, -320f), 5f, 10f);
                    Undertow(root);
                    break;
                case "jetstream":
                    Place("SM_Env_SmugglersHaven", "Syndicate", root, new Vector3(-400f, -130f, 380f), 6f, 140f);
                    break;
                case "cyclone":
                case "storm":
                case "maelstrom":
                    StormWall(root);
                    Undertow(root);
                    break;
                case "citadel":
                    Undertow(root);
                    break;
            }

            if (!string.IsNullOrEmpty(backdrop)) Place(backdrop, null, root, new Vector3(-120f, -70f, 240f), 4f, 200f);
        }

        // Beside a ship in a story scene, near enough to read clearly.
        public static void BuildStoryBackdrop(string backdrop, Transform parent)
        {
            if (string.IsNullOrEmpty(backdrop)) return;
            Place(backdrop, null, parent, new Vector3(-90f, -55f, 160f), 3.2f, 210f);
        }

        static GameObject Place(string asset, string faction, Transform parent, Vector3 position, float scale, float yaw)
        {
            var kit = KitArt.Spawn(asset, parent);
            KitArt.Paint(kit, KitArt.NewMaterial(asset, faction ?? KitArt.Crimson));
            kit.transform.localPosition = position;
            kit.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            kit.transform.localScale = Vector3.one * scale;
            foreach (var renderer in kit.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return kit;
        }

        static void Undertow(Transform root)
        {
            Place("SM_Env_UndertowDisc", KitArt.Crimson, root, new Vector3(0f, UndertowDepth, 0f), UndertowScale, 0f);
        }

        // Twelve copies turned about their ring center form the Maelstrom's eye, around the whole arena.
        static void StormWall(Transform root)
        {
            for (var i = 0; i < StormWallSegments; i++)
            {
                var pivot = new GameObject("Storm Wall").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(0f, -60f, 0f);
                pivot.localRotation = Quaternion.Euler(0f, i * SegmentDegrees, 0f);
                var kit = Place("SM_Env_StormWall", KitArt.Crimson, pivot, Vector3.zero, StormWallScale, 0f);
                var center = KitArt.Socket(kit, "ring_centre");
                if (center != null) kit.transform.localPosition = -center.localPosition * StormWallScale;
            }
        }
    }
}
