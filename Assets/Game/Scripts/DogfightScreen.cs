using System.Collections;
using System.Collections.Generic;
using Airships.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Airships.Game
{
    // Real-time airship combat. All rules live in DogfightSim; this class supplies input and draws it.
    public class DogfightScreen : MonoBehaviour
    {
        const float ProjectileHeight = 3f;
        const float DefaultThrottle = 0.72f;
        const int PlayerShipIndex = 0;
        const int EnemyShipIndex = 1;

        public DogfightSim Sim { get; private set; }

        GameDirector director;
        ShipState player;
        ShipState enemy;
        DogfightAi enemyAi;
        DogfightAi autopilot;
        readonly List<ShipCommand> commands = new List<ShipCommand> { default, default };
        float accumulator;
        bool ended;

        ShipModel playerModel;
        ShipModel enemyModel;
        Vector2 playerPrevious;
        Vector2 enemyPrevious;
        float playerPreviousHeading;
        float enemyPreviousHeading;
        float playerFlash;
        float enemyFlash;
        float smokeTimer;
        float bobTime;

        Transform cameraProxy;
        Transform cameraLook;
        float cameraYaw;

        readonly Dictionary<int, GameObject> shots = new Dictionary<int, GameObject>();
        readonly Stack<GameObject> shotPool = new Stack<GameObject>();
        Transform shotRoot;
        Material shotMaterial;
        Mesh lightShotMesh;
        Mesh heavyShotMesh;

        VirtualStick stick;
        HoldButton bowButton;
        HoldButton broadsideButton;
        Image bowCooldown;
        Image broadsideCooldown;
        Bar playerHull;
        Bar enemyHull;
        TextMeshProUGUI playerHullText;
        RectTransform marker;
        TextMeshProUGUI markerDistance;
        Canvas canvas;
        GameObject hud;
        GameObject pauseOverlay;
        GameObject settingsPanel;
        float lastPlayerHull;
        float lastEnemyHull;

        void Start()
        {
            director = GameDirector.Instance;
            var content = director.Content;
            var tier = director.EncounterTier;

            Sim = new DogfightSim(director.Seed);
            player = Sim.AddShip(0, Encounter.PlayerLoadout(content, director.Save, GameDirector.PlayerShipId), 0f, -110f, 0f);
            enemy = Sim.AddShip(1, Encounter.EnemyLoadout(content, GameDirector.EnemyShipId, tier), 40f, 110f, Mathf.PI);
            enemyAi = new DogfightAi(director.Seed + 1, Mathf.Max(4f, 14f - tier * 2f));
            autopilot = new DogfightAi(director.Seed + 2, 4f);
            lastPlayerHull = player.Hull;
            lastEnemyHull = enemy.Hull;

            playerModel = Models.CreateShip("Player Ship", ShipHull.Sloop, KitArt.Crimson, transform);
            enemyModel = Models.CreateShip("Enemy Ship", EnemyHull(tier), KitArt.Obsidian, transform);
            playerPrevious = new Vector2(player.X, player.Z);
            enemyPrevious = new Vector2(enemy.X, enemy.Z);
            playerPreviousHeading = player.Heading;
            enemyPreviousHeading = enemy.Heading;
            PlaceShip(playerModel, player, playerPrevious, playerPreviousHeading, 0f);
            PlaceShip(enemyModel, enemy, enemyPrevious, enemyPreviousHeading, 0f);

            shotRoot = new GameObject("Shots").transform;
            shotRoot.SetParent(transform, false);
            shotMaterial = Models.NewLitMaterial("Shot");
            shotMaterial.SetColor("_Emission", Palette.Spark * 1.7f);
            lightShotMesh = BuildShotMesh(0.55f);
            heavyShotMesh = BuildShotMesh(0.95f);

            cameraProxy = new GameObject("Camera Proxy").transform;
            cameraProxy.SetParent(transform, false);
            cameraLook = new GameObject("Camera Look").transform;
            cameraLook.SetParent(cameraProxy, false);
            cameraLook.localPosition = new Vector3(0f, 3f, 24f);
            cameraYaw = player.Heading * Mathf.Rad2Deg;
            cameraProxy.SetPositionAndRotation(playerModel.Root.transform.position, Quaternion.Euler(0f, cameraYaw, 0f));
            director.Stage.FrameTarget(cameraProxy, cameraLook, new Vector3(0f, 19f, -54f), 52f, 0.35f);

            BuildHud();
            director.ScreenFx.Milestone("ENEMY SIGHTED!", Palette.Danger);
        }

        // Enemies never share the player's silhouette, and the largest hull signals the toughest
        // pirates. This changes only the look, not the ship's stats.
        static ShipHull EnemyHull(int tier) => tier >= 6 ? ShipHull.Galleon : ShipHull.Brigantine;

        static Mesh BuildShotMesh(float radius)
        {
            var kit = new MeshKit();
            kit.Blob(Vector3.zero, Vector3.one * radius, Color.white, 1);
            return kit.Build("Shot");
        }

        void Update()
        {
            if (!ended)
            {
                accumulator += Time.deltaTime;
                while (accumulator >= DogfightSim.TickSeconds && !ended)
                {
                    accumulator -= DogfightSim.TickSeconds;
                    StepSimulation();
                }
            }

            UpdateVisuals();
            if (!ended) UpdateHud();
        }

        void StepSimulation()
        {
            playerPrevious = new Vector2(player.X, player.Z);
            enemyPrevious = new Vector2(enemy.X, enemy.Z);
            playerPreviousHeading = player.Heading;
            enemyPreviousHeading = enemy.Heading;

            commands[PlayerShipIndex] = GameDirector.Autoplay ? autopilot.Think(Sim, player) : ReadPlayerCommand();
            commands[EnemyShipIndex] = enemyAi.Think(Sim, enemy);
            Sim.Step(commands);
            foreach (var simEvent in Sim.Events) Present(simEvent);

            if (Sim.Finished)
            {
                ended = true;
                StartCoroutine(Finish(Sim.WinningTeam == player.Team));
            }
        }

        ShipCommand ReadPlayerCommand()
        {
            var keyboard = Keyboard.current;
            var turn = stick.Value.x;
            var throttle = DefaultThrottle + Mathf.Max(stick.Value.y, -1f) * (1f - DefaultThrottle);
            if (stick.Value.y < 0f) throttle = Mathf.Lerp(DefaultThrottle, 0.35f, -stick.Value.y);
            var fireBow = bowButton.Held;
            var fireBroadside = broadsideButton.ConsumeTap() || broadsideButton.Held;

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turn = -1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turn = 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttle = 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) throttle = 0.35f;
                fireBow |= keyboard.spaceKey.isPressed;
                fireBroadside |= keyboard.eKey.isPressed;
            }

            return new ShipCommand { Turn = turn, Throttle = throttle, FireBow = fireBow, FireBroadside = fireBroadside };
        }

        void Present(DogfightEvent e)
        {
            switch (e.Kind)
            {
                case DogfightEventKind.Fired:
                    SpawnShot(e);
                    Fx.MuzzleFlash(new Vector3(e.X, ProjectileHeight, e.Z));
                    if (e.ShipId == player.Id)
                    {
                        AudioBank.Play(e.Heavy ? Sfx.HeavyCannon : Sfx.Cannon, e.Heavy ? 0.55f : 0.5f);
                        if (e.Heavy) director.Stage.Shake(0.5f);
                    }
                    else
                    {
                        AudioBank.Play(e.Heavy ? Sfx.HeavyCannon : Sfx.Cannon, 0.22f);
                    }
                    break;
                case DogfightEventKind.Hit:
                    ReleaseShot(e.ProjectileId);
                    PresentHit(e);
                    break;
                case DogfightEventKind.Missed:
                    ReleaseShot(e.ProjectileId);
                    Fx.Puff(new Vector3(e.X, ProjectileHeight, e.Z), Palette.Cloud);
                    break;
                case DogfightEventKind.Destroyed:
                    var position = new Vector3(e.X, 3f, e.Z);
                    Fx.Explosion(position);
                    AudioBank.Play(Sfx.Explosion, 1f, 0f);
                    director.Stage.Shake(2.2f);
                    GameTime.HitStop(0.09f);
                    break;
            }
        }

        void PresentHit(DogfightEvent e)
        {
            var position = new Vector3(e.X, ProjectileHeight + 1f, e.Z);
            Fx.Hit(position, e.Heavy);
            var hitPlayer = e.ShipId == player.Id;
            AudioBank.Play(e.Heavy ? Sfx.HeavyHit : Sfx.Hit, hitPlayer ? 0.85f : 0.6f);
            director.ScreenFx.FloatingNumber(position, director.Stage.MainCamera, Mathf.RoundToInt(e.Amount).ToString(), hitPlayer ? Palette.Danger : Palette.Accent, e.Heavy ? 52f : 40f);

            if (hitPlayer)
            {
                playerFlash = 1f;
                director.Stage.Shake(e.Heavy ? 1f : 0.4f);
                if (e.Heavy) director.ScreenFx.Flash(Palette.Danger, 0.2f);
            }
            else
            {
                enemyFlash = 1f;
                if (e.Heavy)
                {
                    director.Stage.Shake(0.45f);
                    GameTime.HitStop(0.04f);
                }
            }
        }

        void SpawnShot(DogfightEvent e)
        {
            var shot = shotPool.Count > 0 ? shotPool.Pop() : NewShot();
            shot.GetComponent<MeshFilter>().sharedMesh = e.Heavy ? heavyShotMesh : lightShotMesh;
            shot.transform.position = new Vector3(e.X, ProjectileHeight, e.Z);
            var trail = shot.GetComponent<TrailRenderer>();
            trail.enabled = director.Stage.HighDefinition;
            trail.widthMultiplier = e.Heavy ? 0.7f : 0.4f;
            trail.Clear();
            shot.SetActive(true);
            shots[e.ProjectileId] = shot;
        }

        GameObject NewShot()
        {
            var shot = Models.Attach("Shot", shotRoot, lightShotMesh, shotMaterial, false);
            var trail = shot.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Fx.ParticleMaterial;
            trail.time = 0.22f;
            trail.minVertexDistance = 1.5f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Palette.Spark, 0f), new GradientColorKey(Palette.Cloud, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return shot;
        }

        void ReleaseShot(int id)
        {
            if (!shots.TryGetValue(id, out var shot)) return;
            shots.Remove(id);
            shot.SetActive(false);
            shotPool.Push(shot);
        }

        IEnumerator Finish(bool won)
        {
            hud.SetActive(false);
            CloseOverlays();
            if (won)
            {
                director.OnDogfightWon();
                director.ScreenFx.Milestone("ENEMY CRIPPLED!", Palette.Accent);
                director.Stage.FrameTarget(enemyModel.Root.transform, enemyModel.Body, new Vector3(30f, 16f, -34f), 42f, 1f);
                yield return new WaitForSecondsRealtime(2f);
                BuildBoardPrompt();
            }
            else
            {
                director.Stage.FrameTarget(playerModel.Root.transform, playerModel.Body, new Vector3(-30f, 16f, -34f), 42f, 1f);
                yield return new WaitForSecondsRealtime(2.2f);
                director.OnDogfightLost();
            }
        }

        void BuildBoardPrompt()
        {
            var economy = director.Content.economy;
            var boarding = Encounter.BoardingWin(economy, director.EncounterTier);

            var panel = UIKit.Panel("Board Prompt", canvas.transform, Palette.WithAlpha(Palette.Panel, 0.95f));
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.32f), Vector2.one * 0.5f, Vector2.zero, new Vector2(1160f, 430f));
            var heading = UIKit.Text("Heading", panel.transform, "BOARD THEIR SHIP?", 66f, Palette.Accent);
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(1080f, 84f));
            var detail = UIKit.Text("Detail", panel.transform, $"Defeat their crew for another {boarding.Gold} gold and {boarding.Xp} XP.", 36f, Palette.TextLight, TextAlignmentOptions.Center, false);
            UIKit.Place(detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(1080f, 60f));

            var board = UIKit.Button("Board", panel.transform, "BOARD", 50f, Palette.PlayerPaint, director.Board);
            board.ClickSound = Sfx.Confirm;
            UIKit.Place((RectTransform)board.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 36f), new Vector2(480f, 150f));
            var leave = UIKit.Button("Leave", panel.transform, "LEAVE THEM", 44f, Palette.PanelLight, director.DeclineBoarding);
            UIKit.Place((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 36f), new Vector2(480f, 150f));

            if (GameDirector.Autoplay) StartCoroutine(AutoBoard(board));
        }

        static IEnumerator AutoBoard(GameButton board)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            board.Press();
        }

        void UpdateVisuals()
        {
            var alpha = Mathf.Clamp01(accumulator / DogfightSim.TickSeconds);
            bobTime += Time.deltaTime;
            PlaceShip(playerModel, player, playerPrevious, playerPreviousHeading, alpha);
            PlaceShip(enemyModel, enemy, enemyPrevious, enemyPreviousHeading, alpha);

            if (!ended)
            {
                // The proxy swings behind the ship more slowly than it turns, which reveals the turn.
                cameraYaw = Mathf.LerpAngle(cameraYaw, playerModel.Root.transform.eulerAngles.y, 1f - Mathf.Exp(-3f * Time.deltaTime));
                cameraProxy.SetPositionAndRotation(playerModel.Root.transform.position, Quaternion.Euler(0f, cameraYaw, 0f));
            }

            foreach (var projectile in Sim.Projectiles)
            {
                if (!shots.TryGetValue(projectile.Id, out var shot)) continue;
                shot.transform.position = new Vector3(
                    projectile.X + projectile.VelocityX * DogfightSim.TickSeconds * alpha,
                    ProjectileHeight,
                    projectile.Z + projectile.VelocityZ * DogfightSim.TickSeconds * alpha);
            }

            playerFlash = Mathf.MoveTowards(playerFlash, 0f, Time.deltaTime * 6f);
            enemyFlash = Mathf.MoveTowards(enemyFlash, 0f, Time.deltaTime * 6f);
            playerModel.Material.SetColor("_Emission", Palette.Danger * playerFlash * 0.7f);
            enemyModel.Material.SetColor("_Emission", Palette.Spark * enemyFlash * 0.6f);

            smokeTimer -= Time.deltaTime;
            if (smokeTimer <= 0f)
            {
                smokeTimer = 0.14f;
                EmitDamageSmoke(player, playerModel);
                EmitDamageSmoke(enemy, enemyModel);
            }
        }

        static void EmitDamageSmoke(ShipState ship, ShipModel model)
        {
            var health = ship.Hull / ship.Loadout.Hull;
            if (health >= 0.5f) return;
            var position = model.Body.position + model.Body.TransformVector(new Vector3(Random.Range(-2f, 2f), 4f, Random.Range(-5f, 5f)));
            Fx.Burst(position, health <= 0f ? Palette.Ink : Palette.DarkSmoke, 1, 1.5f, health <= 0f ? 6f : 3.4f, 1.8f, 7f);
            if (health <= 0f) Fx.Burst(position, Palette.Danger, 1, 2f, 2.4f, 0.6f, 5f);
        }

        void PlaceShip(ShipModel model, ShipState ship, Vector2 previous, float previousHeading, float alpha)
        {
            var x = Mathf.Lerp(previous.x, ship.X, alpha);
            var z = Mathf.Lerp(previous.y, ship.Z, alpha);
            var heading = Mathf.LerpAngle(previousHeading * Mathf.Rad2Deg, ship.Heading * Mathf.Rad2Deg, alpha);
            model.Root.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, heading, 0f));

            var alive = ship.Alive;
            var bank = alive ? Mathf.Clamp(-ship.TurnRate * Mathf.Rad2Deg * 0.3f, -22f, 22f) : 14f;
            var current = model.Body.localEulerAngles.z;
            var roll = Mathf.LerpAngle(current, bank, 1f - Mathf.Exp(-4f * Time.deltaTime));
            var pitch = alive ? Mathf.Sin(bobTime * 1.3f + ship.Id) * 1.2f : 6f;
            model.Body.localRotation = Quaternion.Euler(pitch, 0f, roll);

            var sink = alive ? 0f : -2.4f;
            var height = Mathf.Sin(bobTime * 1.1f + ship.Id) * 0.5f + sink;
            model.Body.localPosition = Vector3.Lerp(model.Body.localPosition, Vector3.up * height, 1f - Mathf.Exp(-3f * Time.deltaTime));

            var spin = alive ? 900f * (ship.Speed / ship.Loadout.Speed) : 60f;
            foreach (var propeller in model.Propellers) propeller.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
        }

        void BuildHud()
        {
            canvas = UIKit.CreateCanvas("Dogfight Hud", transform, 10);
            var safe = UIKit.SafeArea(canvas.transform);
            hud = safe.gameObject;

            var stickZone = UIKit.Node("Stick Zone", safe);
            stickZone.anchorMin = Vector2.zero;
            stickZone.anchorMax = new Vector2(0.48f, 0.82f);
            stickZone.offsetMin = Vector2.zero;
            stickZone.offsetMax = Vector2.zero;
            stick = VirtualStick.Create(stickZone);

            bowButton = BuildFireButton(safe, "BOW", new Vector2(-30f, 250f), 200f, Palette.PanelLight, out bowCooldown);
            broadsideButton = BuildFireButton(safe, "BROAD\nSIDE", new Vector2(-250f, 40f), 250f, Palette.PlayerPaint, out broadsideCooldown);

            playerHull = BuildHullBar(safe, director.Content.Ship(GameDirector.PlayerShipId).name, new Vector2(0f, 1f), new Vector2(28f, -20f), Palette.Heal, out playerHullText);
            enemyHull = BuildHullBar(safe, $"Enemy Raider  (Rank {director.EncounterTier + 1})", new Vector2(1f, 1f), new Vector2(-28f, -20f), Palette.Danger, out _);

            var pause = UIKit.Button("Pause", safe, "II", 44f, Palette.WithAlpha(Palette.Panel, 0.9f), Pause);
            UIKit.Place((RectTransform)pause.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(112f, 96f));

            var markerImage = UIKit.Solid("Enemy Marker", safe, Palette.Danger);
            marker = markerImage.rectTransform;
            marker.sizeDelta = new Vector2(30f, 30f);
            marker.anchorMin = Vector2.zero;
            marker.anchorMax = Vector2.zero;
            marker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            markerDistance = UIKit.Text("Distance", marker.parent, "", 26f, Palette.TextLight);
            markerDistance.outlineWidth = 0.2f;
            markerDistance.outlineColor = Palette.Ink;
            markerDistance.rectTransform.sizeDelta = new Vector2(200f, 40f);
            markerDistance.rectTransform.anchorMin = Vector2.zero;
            markerDistance.rectTransform.anchorMax = Vector2.zero;
        }

        static HoldButton BuildFireButton(RectTransform safe, string label, Vector2 position, float size, Color color, out Image cooldown)
        {
            var body = new GameObject(label.Replace("\n", " ") + " Button", typeof(RectTransform)).AddComponent<Image>();
            body.transform.SetParent(safe, false);
            body.sprite = UIKit.Circle;
            body.color = Palette.WithAlpha(color, 0.92f);
            UIKit.Place(body.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), position, Vector2.one * size);

            cooldown = new GameObject("Reload", typeof(RectTransform)).AddComponent<Image>();
            cooldown.transform.SetParent(body.transform, false);
            cooldown.sprite = UIKit.Circle;
            cooldown.color = Palette.WithAlpha(Palette.Ink, 0.7f);
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = 2;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0f;
            cooldown.raycastTarget = false;
            UIKit.Fill(cooldown.rectTransform);

            var text = UIKit.Text("Label", body.transform, label, size * 0.17f, Palette.TextLight);
            text.outlineWidth = 0.2f;
            text.outlineColor = Palette.Ink;
            UIKit.Fill(text.rectTransform);
            return body.gameObject.AddComponent<HoldButton>();
        }

        static Bar BuildHullBar(RectTransform safe, string label, Vector2 anchor, Vector2 position, Color color, out TextMeshProUGUI value)
        {
            var panel = UIKit.Panel(label, safe, Palette.WithAlpha(Palette.Panel, 0.88f));
            UIKit.Place(panel.rectTransform, anchor, anchor, position, new Vector2(620f, 98f));
            var text = UIKit.Text("Name", panel.transform, label, 28f, Palette.TextLight, TextAlignmentOptions.Left);
            UIKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(480f, 38f));
            value = UIKit.Text("Value", panel.transform, "", 28f, Palette.Accent, TextAlignmentOptions.Right);
            UIKit.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -8f), new Vector2(160f, 38f));
            var bar = UIKit.Bar("Hull", panel.transform, color);
            UIKit.Place((RectTransform)bar.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(584f, 38f));
            return bar;
        }

        void UpdateHud()
        {
            if (!Mathf.Approximately(player.Hull, lastPlayerHull))
            {
                lastPlayerHull = player.Hull;
                playerHull.Set(player.Hull / player.Loadout.Hull);
            }
            if (!Mathf.Approximately(enemy.Hull, lastEnemyHull))
            {
                lastEnemyHull = enemy.Hull;
                enemyHull.Set(enemy.Hull / enemy.Loadout.Hull);
            }
            playerHullText.text = Mathf.CeilToInt(player.Hull).ToString();
            bowCooldown.fillAmount = player.BowCooldown / player.Loadout.Bow.reload;
            broadsideCooldown.fillAmount = player.BroadsideCooldown / player.Loadout.Broadside.reload;
            UpdateMarker();
        }

        // Marks the enemy on screen, or pins to the screen edge pointing toward it when out of view.
        void UpdateMarker()
        {
            var parent = (RectTransform)marker.parent;
            var camera = director.Stage.MainCamera;
            var target = enemyModel.Body.position + Vector3.up * 13f;
            var screen = camera.WorldToScreenPoint(target);
            var behind = screen.z < 0f;
            if (behind) screen = -screen;

            var width = parent.rect.width;
            var height = parent.rect.height;
            var scale = height / Mathf.Max(1f, Screen.safeArea.height);
            var local = new Vector2((screen.x - Screen.safeArea.xMin) * scale, (screen.y - Screen.safeArea.yMin) * scale);

            const float margin = 90f;
            var onScreen = !behind && local.x > margin && local.x < width - margin && local.y > margin && local.y < height - 170f;
            if (!onScreen)
            {
                var center = new Vector2(width * 0.5f, height * 0.5f);
                var direction = (local - center).normalized;
                if (direction == Vector2.zero) direction = Vector2.up;
                var extents = new Vector2(width * 0.5f - margin, height * 0.5f - 170f);
                var fit = Mathf.Min(extents.x / Mathf.Max(0.001f, Mathf.Abs(direction.x)), extents.y / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
                local = center + direction * fit;
            }
            marker.anchoredPosition = local;
            markerDistance.rectTransform.anchoredPosition = local + Vector2.up * 44f;
            var distance = Vector2.Distance(new Vector2(player.X, player.Z), new Vector2(enemy.X, enemy.Z));
            markerDistance.text = $"{Mathf.RoundToInt(distance)} m";
        }

        void Pause()
        {
            if (pauseOverlay != null || ended) return;
            GameTime.Paused = true;

            var scrim = UIKit.Solid("Pause", canvas.transform, Palette.WithAlpha(Palette.Ink, 0.72f), true);
            UIKit.Fill(scrim.rectTransform);
            pauseOverlay = scrim.gameObject;
            var panel = UIKit.Panel("Panel", scrim.transform, Palette.Panel);
            UIKit.Place(panel.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(680f, 620f));
            var title = UIKit.Text("Title", panel.transform, "PAUSED", 60f, Palette.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(600f, 80f));

            AddPauseButton(panel.transform, "RESUME", -130f, Palette.PlayerPaint, Resume);
            AddPauseButton(panel.transform, "SETTINGS", -280f, Palette.PanelLight, () =>
            {
                if (settingsPanel == null) settingsPanel = SettingsPanel.Build(canvas.transform, director, () => settingsPanel = null);
            });
            AddPauseButton(panel.transform, "RETREAT", -430f, Palette.PanelLight, () =>
            {
                ended = true;
                Resume();
                director.RetreatToPort();
            });
        }

        static void AddPauseButton(Transform parent, string label, float y, Color color, System.Action action)
        {
            var button = UIKit.Button(label, parent, label, 42f, color, action);
            UIKit.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(520f, 130f));
        }

        void Resume()
        {
            GameTime.Paused = false;
            CloseOverlays();
        }

        void CloseOverlays()
        {
            if (settingsPanel != null) Destroy(settingsPanel);
            if (pauseOverlay != null) Destroy(pauseOverlay);
            GameTime.Paused = false;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && !ended) Pause();
        }
    }
}
