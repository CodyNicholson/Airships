using System.Collections;
using System.Collections.Generic;
using Airships.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Airships.Game
{
    // Real-time airship combat. All rules live in SkyEncounter and DogfightSim; this class supplies input and draws it.
    public class DogfightScreen : MonoBehaviour
    {
        const float ProjectileHeight = 3f;
        const float DefaultThrottle = 0.72f;
        const float ClimbDeadZone = 0.18f;
        const float GaugeHeight = 360f;
        const float WhaleMarkerRange = 230f;
        const float RollTapSeconds = 0.22f;
        const float RadarScale = 0.25f;
        const float RadarRim = 88f;
        const float SwallowSeconds = 0.7f;
        const float GulpSeconds = 2.4f;
        const float GulpGlideSpeed = 9f;
        const float SwallowGlideSpeed = 22f;
        static readonly Vector3 SwallowPoint = new Vector3(0f, -3f, -9f); // Inside the mouth.
        static readonly Vector3 SwallowCameraOffset = new Vector3(36f, 4f, 62f); // In front of the jaws, off to one side, looking in.
        const int MaxEnemyRows = 4;
        const float EnemyRowHeight = 46f;
        const float RattleSeconds = 0.9f;
        const string Torpedo = "torpedo";

        public DogfightSim Sim { get; private set; }
        public SkyEncounter Encounter { get; private set; }

        class ShipView
        {
            public ShipState State;
            public ShipModel Model;
            public Vector3 Previous;
            public float PreviousHeading;
            public float Flash;
            public RectTransform Marker;
            public TextMeshProUGUI Label;
            public Bar MarkerHull;
            public string Name;
            public float Rattle;
            public List<EffectEmitter> Fires = new List<EffectEmitter>();
            public List<EffectEmitter> Embers = new List<EffectEmitter>();
            public List<EffectEmitter> Smoke = new List<EffectEmitter>();
            public List<EffectEmitter> Exhaust = new List<EffectEmitter>();
            public List<EffectEmitter> Burners = new List<EffectEmitter>();
            public List<EffectEmitter> Leaks = new List<EffectEmitter>();
        }

        class EnemyRow
        {
            public ShipState Ship;
            public GameObject Root;
            public Bar Hull;
            public TextMeshProUGUI Name;
        }

        class PickupView
        {
            public SkyPickup Pickup;
            public Transform Model;
            public RectTransform Marker;
            public float Base;
        }

        GameDirector director;
        ShipState player;
        ShipView playerView;
        AutoCaptain autopilot;
        float accumulator;
        bool ended;
        bool betrayalShown;

        readonly List<ShipView> views = new List<ShipView>();
        readonly Dictionary<int, ShipView> viewById = new Dictionary<int, ShipView>();
        HazardViews hazardViews;
        SkyZoneViews zoneViews;
        LineRenderer hookLine;
        float smokeTimer;
        float bobTime;

        bool swallowing;
        bool wasRiding;
        Transform cameraProxy;
        Transform cameraLook;
        float cameraYaw;

        readonly Dictionary<int, GameObject> shots = new Dictionary<int, GameObject>();
        readonly Stack<GameObject> shotPool = new Stack<GameObject>();
        Transform shotRoot;
        Material shotMaterial;
        Mesh lightShotMesh;
        Mesh heavyShotMesh;
        const float DangerHullShare = 0.4f; // Below this, the music's danger layer comes in.
        const float EngineCruiseLevel = 0.55f;
        const float HookFlightSeconds = 0.22f;
        const float CloseSoundMeters = 40f;
        const float FarSoundMeters = 220f;
        const float FarSoundLevel = 0.25f;
        const float ExhaustScale = 0.55f; // At the kit's size, propeller smoke hides the ship's stern.
        Material shotKitMaterial;
        Material fittingMaterial;
        GameObject hookProp;
        GameObject harpoonProp;
        float winchTimer;
        Image statusIcon;
        readonly List<Image> abilityIcons = new List<Image>();
        readonly Dictionary<string, Mesh> ammoMeshes = new Dictionary<string, Mesh>();
        readonly HashSet<int> missiles = new HashSet<int>();
        readonly HashSet<int> torpedoes = new HashSet<int>();
        readonly List<EnemyRow> enemyRows = new List<EnemyRow>();
        Image boardLine;
        TextMeshProUGUI enemyHullText;
        readonly List<PickupView> pickupViews = new List<PickupView>();
        RawImage rain;
        float boostHeld;
        Image boostCooldown;
        Image aimIcon;
        Image ammoIcon;
        Bar enemyEnvelope;

        VirtualStick stick;
        RectTransform stickZone;
        RectTransform safeArea;
        HoldButton bowButton;
        HoldButton broadsideButton;
        HoldButton boostButton;
        HoldButton grappleButton;
        HoldButton signatureButton;
        Image bowCooldown;
        Image broadsideCooldown;
        Image signatureCooldown;
        Image grappleCooldown;
        TextMeshProUGUI grappleLabel;
        GameButton aimButton;
        GameButton ammoButton;
        GameButton patchButton;
        GameButton overdriveButton;
        Bar playerHull;
        Bar enemyHull;
        Bar enemyEngines;
        Bar enemyGuns;
        GameObject enemyParts;
        TextMeshProUGUI playerHullText;
        TextMeshProUGUI enemyName;
        TextMeshProUGUI objectiveText;
        TextMeshProUGUI statusText;
        RectTransform whaleMarker;
        TextMeshProUGUI whaleMarkerLabel;
        TextMeshProUGUI whaleWarning;
        RectTransform playerAltitudeTick;
        RectTransform enemyAltitudeTick;
        GameObject altitudeGauge;
        RectTransform radar;
        readonly List<RectTransform> radarDots = new List<RectTransform>();
        readonly List<RectTransform> whaleDots = new List<RectTransform>();
        Canvas canvas;
        GameObject hud;
        GameObject pauseOverlay;
        GameObject settingsPanel;
        CommsBar comms;
        ShipState focus;
        float lastPlayerHull;
        bool wasBoosting;
        bool hullAlarmRang;
        readonly List<(string control, Transform transform)> teachable = new List<(string, Transform)>();
        List<AmmoDef> ammoChoices;
        int ammoIndex;
        bool patchPressed;
        bool overdrivePressed;

        void Start()
        {
            director = GameDirector.Instance;
            var config = director.Sky;
            var content = director.Content;
            var weather = content.Weather(config.Weather);
            director.Stage.ApplyWeather(weather);
            SkyScenery.Build(config.Weather, config.Phase?.backdrop, transform);

            Encounter = new SkyEncounter(config);
            Sim = Encounter.Sim;
            player = Encounter.Player;
            autopilot = new AutoCaptain(director.Seed + 2, 4f);
            hazardViews = new HazardViews(Sim.Field, transform);
            zoneViews = new SkyZoneViews(Sim.Weather, transform, weather.lightningEvery > 0f || config.Weather == "cyclone");
            lastPlayerHull = player.Hull;

            shotRoot = new GameObject("Shots").transform;
            shotRoot.SetParent(transform, false);
            shotMaterial = Models.NewLitMaterial("Shot");
            shotMaterial.SetColor("_Emission", Palette.Spark * 1.7f);
            lightShotMesh = BuildShotMesh(0.55f);
            heavyShotMesh = BuildShotMesh(0.95f);
            shotKitMaterial = KitArt.NewMaterial("Kit Shots", KitArt.Crimson);
            shotKitMaterial.SetColor("_Emission", Palette.Spark * 0.5f);
            fittingMaterial = KitArt.NewMaterial("Fittings", KitArt.Obsidian);

            hookLine = new GameObject("Hook Line").AddComponent<LineRenderer>();
            hookLine.transform.SetParent(transform, false);
            hookLine.sharedMaterial = Fx.ParticleMaterial;
            hookLine.widthMultiplier = 0.7f;
            hookLine.positionCount = 2;
            hookLine.startColor = Palette.Brass;
            hookLine.endColor = Palette.Brass;
            hookLine.enabled = false;

            BuildHud();
            playerView = AddView(player, config.PlayerShipName);
            foreach (var enemy in Encounter.Enemies) AddView(enemy.Ship, enemy.Spawn.name);
            BuildPickups(config);
            if (weather.rain) BuildRain();

            cameraProxy = new GameObject("Camera Proxy").transform;
            cameraProxy.SetParent(transform, false);
            cameraLook = new GameObject("Camera Look").transform;
            cameraLook.SetParent(cameraProxy, false);
            cameraLook.localPosition = new Vector3(0f, 3f, 24f);
            cameraYaw = player.Heading * Mathf.Rad2Deg;
            cameraProxy.SetPositionAndRotation(playerView.Model.Root.transform.position, Quaternion.Euler(0f, cameraYaw, 0f));
            var cameraScale = CameraScaleFor(playerView.Model);
            cameraLook.localPosition = new Vector3(0f, 3f * cameraScale, 24f);
            director.Stage.FrameTarget(cameraProxy, cameraLook, new Vector3(0f, 19f * cameraScale, -54f * cameraScale), 52f, 0.35f);

            var phase = config.Phase;
            director.ScreenFx.Milestone(phase == null ? "ENEMY SIGHTED!" : director.Run.PhaseIndex == 0 ? director.Run.Mission.title.ToUpperInvariant() : "BATTLE STATIONS!", Palette.Danger, Sfx.WarHorn);
        }

        // Bigger ships need the chase camera further back, or they fill the screen.
        static float CameraScaleFor(ShipModel model)
        {
            var bounds = new Bounds(model.Root.transform.position, Vector3.zero);
            foreach (var renderer in model.Kit.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            return Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) / 28f, 1f, 1.7f);
        }

        void AttachEffects(ShipView view)
        {
            var kit = view.Model.Kit;
            var scale = view.Model.Scale;
            if (view.State.BoardsOnContact)
            {
                foreach (var thruster in Effects.AttachAll("ThrusterTrail", kit, scale)) thruster.Active = true;
                return;
            }
            view.Fires = Effects.AttachAll("HullFire", kit, scale);
            view.Embers = Effects.AttachAll("EmberDrift", kit, scale);
            view.Smoke = Effects.AttachAll("DamageSmoke", kit, scale);
            view.Exhaust = Effects.AttachAll("EngineExhaust", kit, scale * ExhaustScale);
            view.Burners = Effects.AttachAll("AfterburnerBoost", kit, scale);
            view.Leaks = Effects.AttachAll("EnvelopeHit", kit, scale);
        }

        // Bolts on what a ship is fighting with, so its loadout shows from the chase camera.
        void FitWeapons(ShipView view)
        {
            var kit = view.Model.Kit;
            var deck = KitArt.Socket(kit, "deck_centre");
            if (deck == null) return;
            if (view.State == player)
            {
                var signature = player.Loadout.Signature;
                if (signature != null && Encounter.HasControl("signature"))
                {
                    if (signature.kind == "harpoon" && KitArt.Socket(kit, "harpoon_muzzle") == null) Fit("SM_Prop_HarpoonWinch", deck, new Vector3(0f, 0.4f, 2.4f), view);
                    if (signature.kind == "afterburner" && KitArt.Socket(kit, "afterburner_L") == null) Fit("SM_Prop_AfterburnerTurbine", deck, new Vector3(0f, 0.4f, -2.4f), view);
                }
                if (Encounter.HasControl("flak")) Fit("SM_Prop_FlakGun", deck, new Vector3(1.4f, 0.2f, 0f), view);
                if (Encounter.HasControl("mortar")) Fit("SM_Prop_DeckMortar", deck, new Vector3(-1.4f, 0.2f, 0f), view);
                return;
            }
            var enemy = FindEnemy(view.State);
            if (enemy == null) return;
            if (enemy.Spawn.shipId == "siege_frigate") Fit("SM_Prop_SiegeCannon", deck, new Vector3(0f, 0.2f, 0f), view);
            if (enemy.Spawn.shipId == "iron_maw") Fit("SM_Prop_KragAnchor", deck, new Vector3(0f, -5f, 3f), view);
            if (enemy.Spawn.missileEvery > 0f) Fit("SM_Prop_Missile", deck, new Vector3(1.4f, 0.2f, 0f), view);
        }

        void Fit(string asset, Transform socket, Vector3 offset, ShipView view)
        {
            var prop = KitArt.Spawn(asset, socket);
            KitArt.Paint(prop, fittingMaterial);
            prop.transform.localPosition = offset;
        }

        static void SetActive(List<EffectEmitter> emitters, bool active)
        {
            foreach (var emitter in emitters) emitter.Active = active;
        }

        void BuildPickups(SkyConfig config)
        {
            if (Encounter.Pickups.Count == 0) return;
            var asset = string.IsNullOrEmpty(config.Phase?.pickupModel) ? "SM_Prop_LiftStone" : config.Phase.pickupModel;
            var material = KitArt.NewMaterial("Pickups", "Scrap");
            material.SetColor("_Emission", Palette.Spark * 0.35f);
            foreach (var pickup in Encounter.Pickups)
            {
                var kit = KitArt.Spawn(asset, transform);
                KitArt.Paint(kit, material);
                kit.transform.position = new Vector3(pickup.X, pickup.Y, pickup.Z);
                kit.transform.localScale = Vector3.one * 7f;
                var image = new GameObject("Pickup Marker", typeof(RectTransform)).AddComponent<Image>();
                image.transform.SetParent(safeArea, false);
                image.sprite = KitArt.Icon(asset == "SM_Prop_LiftStone" ? "Item_LiftStone" : asset == "SM_Prop_ScrapPile" ? "Item_Scrap" : asset == "SM_Prop_CipherKey" ? "Item_CipherKey" : "Marker_Objective");
                image.raycastTarget = false;
                var marker = image.rectTransform;
                marker.anchorMin = Vector2.zero;
                marker.anchorMax = Vector2.zero;
                marker.sizeDelta = Vector2.one * 48f;
                pickupViews.Add(new PickupView { Pickup = pickup, Model = kit.transform, Marker = marker, Base = pickup.Y });
            }
        }

        void BuildRain()
        {
            rain = new GameObject("Rain", typeof(RectTransform)).AddComponent<RawImage>();
            rain.transform.SetParent(canvas.transform, false);
            rain.transform.SetAsFirstSibling();
            rain.raycastTarget = false;
            rain.texture = Resources.Load<Texture2D>($"Art/Vfx/T_VFX_Rain_{KitArt.Quality}");
            rain.color = new Color(1f, 1f, 1f, 0.25f);
            UIKit.Fill(rain.rectTransform);
        }

        // The prologue's turning point: the same ship, now a burning wreck.
        void SwapToWreck()
        {
            var old = playerView.Model;
            var fresh = Models.CreateShip("Player Ship", director.Content.Ship("gale_wreck"), transform);
            fresh.Root.transform.SetPositionAndRotation(old.Root.transform.position, old.Root.transform.rotation);
            fresh.Body.localPosition = old.Body.localPosition;
            fresh.Body.localRotation = old.Body.localRotation;
            old.Root.SetActive(false);
            Destroy(old.Root);
            playerView.Model = fresh;
            AttachEffects(playerView);
            SetActive(playerView.Fires, true);
            SetActive(playerView.Embers, true);
            SetActive(playerView.Smoke, true);
            Effects.PlayOn("LiftCoreSabotage", fresh.Kit, fresh.Scale);
        }

        // Debris tumbling away from a ship that's gone.
        void ThrowWreckage(Vector3 at, float scale)
        {
            var count = director.Stage.HighDefinition ? 5 : 2;
            var material = KitArt.NewMaterial("Wreckage", KitArt.Crimson);
            for (var i = 0; i < count; i++)
            {
                var chunk = KitArt.Spawn("SM_Prop_WreckChunk", transform);
                KitArt.Paint(chunk, material);
                chunk.transform.position = at + Random.insideUnitSphere * 4f;
                chunk.transform.localScale = Vector3.one * scale * Random.Range(0.5f, 1.1f);
                chunk.AddComponent<FallingChunk>().Velocity = Random.onUnitSphere * 12f + Vector3.up * 5f;
            }
        }

        ShipView AddView(ShipState state, string name)
        {
            var content = director.Content;
            ShipModel model;
            if (state == player)
            {
                var id = director.Run != null ? director.Run.LastShipFlown : director.Save.activeShip;
                model = Models.CreateShip("Player Ship", content.Ship(id), transform);
            }
            else
            {
                var enemy = FindEnemy(state);
                var def = content.Ship(enemy.Spawn.shipId);
                model = director.Run == null
                    ? Models.CreateShip("Enemy Ship", FreeSkiesHull(director.Sky.Tier), KitArt.Obsidian, transform)
                    : Models.CreateShip(name, def, transform);
            }
            var view = new ShipView { State = state, Model = model, Name = name, Previous = new Vector3(state.X, state.Y, state.Z), PreviousHeading = state.Heading };
            PlaceShip(model, state, view.Previous, view.PreviousHeading, 0f);
            AttachEffects(view);
            FitWeapons(view);
            if (state != player)
            {
                var friendly = state.Team == player.Team;
                var main = state.Boarding == BoardingRule.Main;
                var image = new GameObject("Ship Marker", typeof(RectTransform)).AddComponent<Image>();
                image.transform.SetParent(safeArea, false);
                image.raycastTarget = false;
                image.sprite = KitArt.Icon(friendly ? "Marker_Waypoint" : main ? "Marker_Objective" : "Marker_EnemyFleet");
                image.color = friendly ? Palette.Heal : state.BoardsOnContact || main ? Palette.Accent : Color.white;
                view.Marker = image.rectTransform;
                view.Marker.sizeDelta = state.BoardsOnContact ? new Vector2(30f, 30f) : main ? new Vector2(66f, 66f) : new Vector2(50f, 50f);
                view.Marker.anchorMin = Vector2.zero;
                view.Marker.anchorMax = Vector2.zero;
                view.Label = UIKit.Text("Distance", safeArea, "", 26f, friendly ? Palette.Heal : Palette.TextLight);
                view.Label.rectTransform.sizeDelta = new Vector2(320f, 40f);
                view.Label.rectTransform.anchorMin = Vector2.zero;
                view.Label.rectTransform.anchorMax = Vector2.zero;
                if (!state.BoardsOnContact)
                {
                    view.MarkerHull = UIKit.Bar("Marker Hull", safeArea, friendly ? Palette.Heal : Palette.Danger);
                    UIKit.Place((RectTransform)view.MarkerHull.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120f, 16f));
                    view.MarkerHull.Set(state.Hull / state.Loadout.Hull, true);
                }
            }
            views.Add(view);
            viewById[state.Id] = view;
            return view;
        }

        SkyEnemy FindEnemy(ShipState state)
        {
            foreach (var enemy in Encounter.Enemies)
            {
                if (enemy.Ship == state) return enemy;
            }
            return null;
        }

        // Enemies never share the player's silhouette, and the largest hull signals the toughest
        // pirates. This changes only the look, not the ship's stats.
        static ShipHull FreeSkiesHull(int tier) => tier >= 6 ? ShipHull.Galleon : ShipHull.Brigantine;

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
            foreach (var view in views)
            {
                view.Previous = new Vector3(view.State.X, view.State.Y, view.State.Z);
                view.PreviousHeading = view.State.Heading;
            }
            hazardViews.CapturePrevious();

            ShipCommand command;
            if (GameDirector.Autoplay)
            {
                autopilot.ChooseAmmo(Encounter, ammoChoices);
                command = autopilot.Think(Encounter);
            }
            else
            {
                command = ReadPlayerCommand();
            }
            AudioBank.SetEngine(command.Throttle > DefaultThrottle ? 1f : EngineCruiseLevel);
            Encounter.Step(command);

            foreach (var simEvent in Sim.Events) Present(simEvent);

            // Gliders launch mid-fight, so a new ship can need a model.
            foreach (var enemy in Encounter.Enemies)
            {
                if (!viewById.ContainsKey(enemy.Ship.Id)) AddView(enemy.Ship, enemy.Spawn.name);
            }
            CommsLine line;
            while ((line = Encounter.NextComms()) != null) comms.Say(line);
            if (Encounter.BetrayalStarted && !betrayalShown)
            {
                betrayalShown = true;
                SwapToWreck();
                director.ScreenFx.Milestone("MUTINY!", Palette.Danger);
                director.Stage.Shake(3f);
                AudioBank.Play(Sfx.Explosion, 1f, 0f);
            }

            if (Encounter.Outcome != SkyOutcome.Ongoing)
            {
                ended = true;
                StartCoroutine(Finish());
            }
        }

        ShipCommand ReadPlayerCommand()
        {
            var keyboard = Keyboard.current;
            var turn = stick.Value.x;

            // A small dead zone keeps steering from nudging the altitude.
            var climb = Mathf.Abs(stick.Value.y) < ClimbDeadZone ? 0f : stick.Value.y;
            var boosting = boostButton.Held;
            var fireBow = bowButton.Held;
            var fireBroadside = broadsideButton.ConsumeTap() || broadsideButton.Held;
            var grapple = grappleButton.ConsumeTap() || grappleButton.Held;
            var signature = signatureButton.ConsumeTap();

            // A quick tap of BOOST rolls toward the stick, and holding it opens the throttle.
            var roll = false;
            if (boostButton.Held)
            {
                boostHeld += DogfightSim.TickSeconds;
            }
            else
            {
                if (boostHeld > 0f && boostHeld < RollTapSeconds) roll = true;
                boostHeld = 0f;
            }
            boosting = boosting && boostHeld >= RollTapSeconds;

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turn = -1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turn = 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) climb = 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) climb = -1f;
                boosting |= keyboard.leftShiftKey.isPressed;
                fireBow |= keyboard.spaceKey.isPressed;
                fireBroadside |= keyboard.eKey.isPressed;
                grapple |= keyboard.gKey.isPressed;
                roll |= keyboard.qKey.wasPressedThisFrame;
                signature |= keyboard.rKey.wasPressedThisFrame;
            }

            var evade = 0f;
            var evadeLift = 0f;
            if (roll) DogfightSim.RollDirection(turn, Encounter.CanClimb ? climb : 0f, out evade, out evadeLift);

            if (boosting && !wasBoosting) AudioBank.Play(Sfx.BoostIgnite, 0.7f);
            wasBoosting = boosting;

            if (Mathf.Abs(turn) > 0.1f) Encounter.MarkUsed("stick");
            if (Mathf.Abs(climb) > 0.1f) Encounter.MarkUsed("altitude");
            if (boosting) Encounter.MarkUsed("boost");
            if (fireBow) Encounter.MarkUsed("bow");
            if (fireBroadside) Encounter.MarkUsed("broadside");
            if (grapple) Encounter.MarkUsed("grapple");
            if (signature) Encounter.MarkUsed("signature");

            var command = new ShipCommand
            {
                Turn = turn,
                Throttle = boosting ? 1f : DefaultThrottle,
                Climb = climb,
                FireBow = fireBow && Encounter.HasControl("bow"),
                FireBroadside = fireBroadside && Encounter.HasControl("broadside"),
                Grapple = grapple && Encounter.HasControl("grapple"),
                Signature = signature && Encounter.HasControl("signature"),
                Patch = patchPressed,
                Overdrive = overdrivePressed,
                Evade = evade,
                EvadeLift = evadeLift,
            };
            patchPressed = false;
            overdrivePressed = false;
            return command;
        }

        void Present(DogfightEvent e)
        {
            switch (e.Kind)
            {
                case DogfightEventKind.Fired when e.AmmoId == Torpedo:
                    SpawnShot(e);
                    missiles.Add(e.ProjectileId);
                    torpedoes.Add(e.ProjectileId);
                    Effects.Play("CannonFire", new Vector3(e.X, e.Y + ProjectileHeight, e.Z), 2.4f);
                    AudioBank.Play(Sfx.MissileLaunch, 0.9f);
                    if (e.ShipId == player.Id) director.Stage.Shake(0.6f);
                    break;
                case DogfightEventKind.Fired:
                    SpawnShot(e);
                    Effects.Play("CannonFire", new Vector3(e.X, e.Y + ProjectileHeight, e.Z), e.Heavy ? 2.2f : 1.3f);
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
                    if (torpedoes.Contains(e.ProjectileId))
                    {
                        // Out of fuel, a torpedo goes off in the air rather than vanishing.
                        BurstTorpedo(e, 0.7f * NearnessToPlayer(e));
                        break;
                    }
                    ReleaseShot(e.ProjectileId);
                    PresentMissSound(e);
                    if (e.AmmoId == "flak") Effects.Play("FlakBurst", new Vector3(e.X, e.Y + ProjectileHeight, e.Z), 1.5f);
                    else if (e.AmmoId == "mortar") Effects.Play("MortarImpact", new Vector3(e.X, e.Y + ProjectileHeight, e.Z));
                    else Effects.Play("CannonSmoke", new Vector3(e.X, e.Y + ProjectileHeight, e.Z), 0.8f);
                    break;
                case DogfightEventKind.Destroyed:
                    var position = new Vector3(e.X, e.Y + ProjectileHeight, e.Z);
                    if (viewById.TryGetValue(e.ShipId, out var doomed))
                    {
                        var blast = doomed.Model.Scale * Mathf.Clamp(doomed.State.Loadout.Radius / 6f, 0.6f, 1.6f);
                        Effects.PlayOn("ShipExplosion", doomed.Model.Kit, blast);
                        Effects.PlayOn("ShipShockwave", doomed.Model.Kit, blast);
                        ThrowWreckage(doomed.Model.Body.position, doomed.Model.Scale * 0.6f);
                    }
                    else
                    {
                        Effects.Play("ShipExplosion", position, 1.5f);
                    }
                    AudioBank.Play(Sfx.Explosion, 1f, 0f);
                    director.Stage.Shake(2.2f);
                    GameTime.HitStop(0.09f);
                    if (e.ShipId != player.Id) HideShipSoon(e.ShipId);
                    break;
                case DogfightEventKind.ShotBlocked:
                    if (torpedoes.Contains(e.ProjectileId)) BurstTorpedo(e, NearnessToPlayer(e));
                    ReleaseShot(e.ProjectileId);
                    Fx.Puff(new Vector3(e.X, e.Y + ProjectileHeight, e.Z), e.HazardKind == HazardKind.Rock ? Palette.Smoke : Palette.DeckWood);
                    AudioBank.Play(Sfx.Hit, 0.25f);
                    break;
                case DogfightEventKind.HazardHit:
                    PresentCrash(e);
                    break;
                case DogfightEventKind.HazardBroken:
                    hazardViews.Hide(e.HazardId);
                    AudioBank.Play(e.HazardKind == HazardKind.Jellyfish ? Sfx.JellyPop : Sfx.TimberBreak, 0.8f * NearnessToPlayer(e));
                    Fx.Burst(new Vector3(e.X, e.Y, e.Z), e.HazardKind == HazardKind.Jellyfish ? Palette.Storm : Palette.DeckWood, 16, 12f, 1.4f, 0.8f, 1f);
                    break;
                case DogfightEventKind.ShipBump:
                    Fx.Hit(new Vector3(e.X, e.Y + ProjectileHeight, e.Z), false);
                    AudioBank.Play(Sfx.HeavyHit, 0.7f);
                    director.Stage.Shake(0.8f);
                    break;
                case DogfightEventKind.WhaleAlert:
                    AudioBank.Play(Sfx.WhaleCall, 1f, 0f);
                    AudioBank.PlayMusic(GameMusic.Boss);
                    break;
                case DogfightEventKind.WhaleLunge:
                    AudioBank.Play(Sfx.WhaleCall, 0.7f, 0.2f);
                    director.Stage.Shake(0.6f);
                    break;
                case DogfightEventKind.Eaten:
                    PresentEaten(e);
                    break;
                case DogfightEventKind.Raked:
                    director.ScreenFx.FloatingNumber(new Vector3(e.X, e.Y + ProjectileHeight + 3f, e.Z), director.Stage.MainCamera, "RAKED!", Palette.Accent, 56f);
                    AudioBank.Play(Sfx.Raked, 0.85f);
                    break;
                case DogfightEventKind.Evaded:
                    Fx.Burst(new Vector3(e.X, e.Y + ProjectileHeight, e.Z), Palette.Cloud, 16, 16f, 1.4f, 0.4f);
                    AudioBank.Play(Sfx.ShipRoll, e.ShipId == player.Id ? 0.9f : 0.3f);
                    Encounter.MarkUsed("evade");
                    break;
                case DogfightEventKind.DamageDrift:
                    AudioBank.Play(Sfx.HullCreak, 0.6f);
                    director.Stage.Shake(0.3f);
                    break;
                case DogfightEventKind.VolleyWarning:
                    AudioBank.Play(Sfx.VolleyWarning, 0.3f, 0.1f);
                    if (viewById.TryGetValue(e.ShipId, out var aiming))
                    {
                        foreach (var muzzle in KitArt.MatchSockets(aiming.Model.Kit, new[] { "muzzle*" })) Fx.Burst(muzzle.position, Palette.Danger, 2, 1f, 1.8f, 0.7f);
                    }
                    break;
                case DogfightEventKind.MissileLaunched:
                    SpawnShot(e);
                    missiles.Add(e.ProjectileId);
                    Effects.Play("CannonFire", new Vector3(e.X, e.Y + ProjectileHeight, e.Z), 2f);
                    AudioBank.Play(Sfx.MissileLaunch, 0.75f);
                    break;
                case DogfightEventKind.MineBlast:
                    hazardViews.Hide(e.HazardId);
                    Effects.Play("MortarImpact", new Vector3(e.X, e.Y, e.Z), 3.5f);
                    AudioBank.Play(Sfx.Explosion, 0.9f);
                    director.Stage.Shake(1.2f);
                    break;
                case DogfightEventKind.PickupTaken:
                    foreach (var view in pickupViews)
                    {
                        if (view.Pickup.Id != e.HazardId) continue;
                        view.Model.gameObject.SetActive(false);
                        view.Marker.gameObject.SetActive(false);
                    }
                    Effects.Play("LiftCoreSabotage", new Vector3(e.X, e.Y, e.Z));
                    AudioBank.Play(Sfx.Pickup, 0.8f);
                    break;
                case DogfightEventKind.WaveArrived:
                    director.ScreenFx.Milestone("REINFORCEMENTS!", Palette.Danger, Sfx.WarHorn);
                    break;
                case DogfightEventKind.PartDisabled:
                    var at = new Vector3(e.X, e.Y + ProjectileHeight, e.Z);
                    Fx.Hit(at, true);
                    director.ScreenFx.FloatingNumber(at, director.Stage.MainCamera, e.Part == ShipPart.Engines ? "ENGINES OUT!" : "GUNS OUT!", Palette.Accent, 60f);
                    AudioBank.Play(Sfx.HeavyHit, 1f, 0f);
                    break;
                case DogfightEventKind.HookLanded:
                    var hooked = new Vector3(e.X, e.Y + ProjectileHeight, e.Z);
                    Fx.Hit(hooked, false);
                    AudioBank.Play(Sfx.HookThrow, 0.7f);
                    StartCoroutine(PlayAfter(Sfx.HookLand, HookFlightSeconds, 0.9f));
                    director.ScreenFx.FloatingNumber(hooked, director.Stage.MainCamera, "HOOK!", Palette.Brass, 54f);
                    break;
                case DogfightEventKind.HooksSnapped:
                    AudioBank.Play(Sfx.RopeSnap, 0.9f);
                    director.ScreenFx.FloatingNumber(new Vector3(e.X, e.Y + ProjectileHeight, e.Z), director.Stage.MainCamera, "LINES SNAPPED", Palette.Danger, 50f);
                    break;
                case DogfightEventKind.Boarded:
                    AudioBank.Play(Sfx.ChainTaut, 0.7f);
                    break;
                case DogfightEventKind.GliderLaunched:
                    AudioBank.Play(Sfx.GliderLaunch, 0.7f * NearnessToPlayer(e));
                    break;
                case DogfightEventKind.Tethered:
                    AudioBank.Play(Sfx.ChainTaut, 1f, 0f);
                    director.Stage.Shake(1.4f);
                    break;
                case DogfightEventKind.SignatureFired:
                    Fx.Burst(new Vector3(e.X, e.Y + ProjectileHeight, e.Z), Palette.Accent, 18, 14f, 1.4f, 0.5f);
                    AudioBank.Play(Sfx.SignatureFire, 0.75f);
                    break;
                case DogfightEventKind.BoarderLanded:
                    director.ScreenFx.Flash(Palette.Danger, 0.2f);
                    AudioBank.Play(Sfx.Slash, 1f, 0f);
                    director.ScreenFx.FloatingNumber(new Vector3(e.X, e.Y + ProjectileHeight, e.Z), director.Stage.MainCamera, "BOARDERS!", Palette.Danger, 60f);
                    HideShipSoon(e.ShipId);
                    break;
                case DogfightEventKind.Departed:
                    AudioBank.Play(Sfx.ShipFlyby, 0.7f * NearnessToPlayer(e));
                    HideShipSoon(e.ShipId);
                    break;
                case DogfightEventKind.Patched:
                    Fx.Burst(new Vector3(e.X, e.Y + ProjectileHeight, e.Z), Palette.Heal, 22, 8f, 1.2f, 0.8f, 3f);
                    AudioBank.Play(Sfx.Heal);
                    break;
                case DogfightEventKind.Overdrive:
                    director.ScreenFx.Flash(Palette.Storm, 0.2f);
                    AudioBank.Play(Sfx.Overdrive, 0.75f);
                    break;
                case DogfightEventKind.Crushing:
                    AudioBank.Play(Sfx.HullCreak, 0.9f);
                    director.Stage.Shake(0.5f);
                    director.ScreenFx.Flash(Palette.Danger, 0.15f);
                    break;
                case DogfightEventKind.LightningWarning:
                    zoneViews.ShowWarning(new Vector3(e.X, e.Y, e.Z), director.Content.skyRules.lightningRadius);
                    AudioBank.Play(Sfx.ThunderRumble, 0.6f);
                    break;
                case DogfightEventKind.LightningStrike:
                    zoneViews.Strike(new Vector3(e.X, player.Y, e.Z));
                    director.ScreenFx.Flash(Palette.Cloud, 0.35f, 0.18f);
                    AudioBank.Play(Sfx.LightningCrack, 0.9f);
                    director.Stage.Shake(1f);
                    break;
            }
        }

        // Flak and shells burst loudest near the player. A miss only whizzes if it goes close by,
        // and it's a near miss if the player rolled out of the way of it.
        void PresentMissSound(DogfightEvent e)
        {
            if (e.AmmoId == "flak") AudioBank.Play(Sfx.FlakBurst, 0.7f * NearnessToPlayer(e));
            else if (e.AmmoId == "mortar") AudioBank.Play(Sfx.MortarBurst, 0.7f * NearnessToPlayer(e));
            else if (NearnessToPlayer(e) >= 1f) AudioBank.Play(player.ImmuneTimer > 0f ? Sfx.NearMiss : Sfx.ShotWhiz, player.ImmuneTimer > 0f ? 0.8f : 0.35f);
        }

        float NearnessToPlayer(DogfightEvent e)
        {
            var distance = Mathf.Sqrt((e.X - player.X) * (e.X - player.X) + (e.Z - player.Z) * (e.Z - player.Z));
            return Mathf.Lerp(1f, FarSoundLevel, Mathf.InverseLerp(CloseSoundMeters, FarSoundMeters, distance));
        }

        static IEnumerator PlayAfter(Sfx sound, float seconds, float volume)
        {
            yield return new WaitForSecondsRealtime(seconds);
            AudioBank.Play(sound, volume);
        }

        void OnDestroy() => AudioBank.SetEngine(0f);

        void HideShipSoon(int shipId)
        {
            if (viewById.TryGetValue(shipId, out var view)) StartCoroutine(HideAfter(view));
        }

        IEnumerator HideAfter(ShipView view)
        {
            view.Marker?.gameObject.SetActive(false);
            view.Label?.gameObject.SetActive(false);
            if (view.MarkerHull != null) view.MarkerHull.gameObject.SetActive(false);
            yield return new WaitForSeconds(view.State.Departed ? 0f : 1.6f);
            view.Model.Root.SetActive(false);
        }

        // Crash feedback scales with the damage, which scales with the size of what was hit.
        void PresentCrash(DogfightEvent e)
        {
            var position = new Vector3(e.X, e.Y, e.Z);
            var hitPlayer = e.ShipId == player.Id;
            var severity = Mathf.Clamp01(e.Amount / 120f);
            Fx.Hit(position, severity > 0.4f);
            AudioBank.Play(severity > 0.4f ? Sfx.HeavyHit : Sfx.Hit, hitPlayer ? 1f : 0.5f);
            director.ScreenFx.FloatingNumber(position, director.Stage.MainCamera, Mathf.RoundToInt(e.Amount).ToString(), hitPlayer ? Palette.Danger : Palette.Accent, 44f + severity * 28f);
            if (hitPlayer)
            {
                playerView.Flash = 1f;
                director.Stage.Shake(0.6f + severity * 2f);
                director.ScreenFx.Flash(Palette.Danger, 0.12f + severity * 0.25f);
                if (severity > 0.4f) GameTime.HitStop(0.06f);
            }
            else if (viewById.TryGetValue(e.ShipId, out var view))
            {
                view.Flash = 1f;
            }
        }

        void PresentEaten(DogfightEvent e)
        {
            // The ship stops flying here, and Swallow takes it from there.
            swallowing = true;
            director.Stage.Shake(1f);
        }

        // The ship goes into the open mouth, the jaws snap shut, and a gulp travels down the whale.
        IEnumerator Swallow()
        {
            var ship = playerView.Model.Root.transform;
            var eater = hazardViews.WhaleNearest(ship.position);
            hazardViews.HoldMouth(eater, 1f, 8f);
            director.Stage.FrameTarget(eater, eater, SwallowCameraOffset, 52f, 0.5f);

            var from = ship.position;
            var fromScale = ship.localScale;
            var glide = Vector3.zero;
            for (var t = 0f; t < SwallowSeconds; t += Time.unscaledDeltaTime)
            {
                // The whale keeps coming, closing over the ship.
                glide += eater.forward * SwallowGlideSpeed * Time.unscaledDeltaTime;
                hazardViews.Gulp(eater, 0f, glide);
                var drawn = t / SwallowSeconds;
                drawn *= drawn;
                ship.position = Vector3.Lerp(from, eater.TransformPoint(SwallowPoint), drawn);
                ship.localScale = Vector3.Lerp(fromScale, fromScale * 0.2f, drawn);
                ship.Rotate(0f, 0f, 420f * Time.unscaledDeltaTime, Space.Self);
                yield return null;
            }

            ship.gameObject.SetActive(false);
            hazardViews.HoldMouth(eater, 0f, 14f);
            AudioBank.Play(Sfx.WhaleGulp, 1f, 0f);
            director.Stage.Shake(3f);
            director.ScreenFx.Flash(Palette.Ink, 0.5f);
            director.ScreenFx.Milestone("SWALLOWED WHOLE!", Palette.Danger);
            Fx.Burst(eater.TransformPoint(0f, 0f, 5f), Palette.DeckWood, 24, 18f, 1.6f, 0.9f, 1f);

            var burped = false;
            for (var t = 0f; t < GulpSeconds; t += Time.unscaledDeltaTime)
            {
                glide += eater.forward * GulpGlideSpeed * Time.unscaledDeltaTime;
                hazardViews.Gulp(eater, Mathf.Sin(t * 7f) * Mathf.Exp(-t * 1.4f) * 0.12f, glide);
                if (!burped && t > GulpSeconds * 0.6f)
                {
                    burped = true;
                    Fx.Burst(eater.TransformPoint(0f, 2f, 6f), Palette.DeckWood, 10, 10f, 1.2f, 1f, 2f);
                    AudioBank.Play(Sfx.WhaleCall, 0.6f, 0f);
                }
                yield return null;
            }
        }

        void BurstTorpedo(DogfightEvent e, float volume)
        {
            ReleaseShot(e.ProjectileId);
            torpedoes.Remove(e.ProjectileId);
            missiles.Remove(e.ProjectileId);
            Effects.Play("MortarImpact", new Vector3(e.X, e.Y + ProjectileHeight, e.Z), 2.4f);
            AudioBank.Play(Sfx.MortarBurst, volume);
        }

        // A torpedo strike is the biggest hit in the sky: a full explosion, and the ship it hit shakes.
        void PresentTorpedoHit(DogfightEvent e, Vector3 position)
        {
            torpedoes.Remove(e.ProjectileId);
            missiles.Remove(e.ProjectileId);
            Effects.Play("ShipExplosion", position, 0.8f);
            Effects.Play("ShipShockwave", position, 0.7f);
            Effects.Play("MortarImpact", position, 2.2f);
            AudioBank.Play(Sfx.Explosion, 0.9f, 0.05f);
            director.Stage.Shake(1.1f);
            GameTime.HitStop(0.05f);
            if (viewById.TryGetValue(e.ShipId, out var struck)) struck.Rattle = 1f;
        }

        void PresentHit(DogfightEvent e)
        {
            var position = new Vector3(e.X, e.Y + ProjectileHeight + 1f, e.Z);
            if (e.AmmoId == Torpedo) PresentTorpedoHit(e, position);
            else if (e.AmmoId == "flak") Effects.Play("FlakBurst", position, 1.5f);
            else if (e.AmmoId == "mortar" || e.AmmoId == "missile") Effects.Play("MortarImpact", position, 1.6f);
            else Effects.Play("HullImpact", position, e.Heavy ? 2f : 1.2f);
            var hitPlayer = e.ShipId == player.Id;
            AudioBank.Play(e.Heavy ? Sfx.HeavyHit : Sfx.Hit, hitPlayer ? 0.85f : 0.6f);
            director.ScreenFx.FloatingNumber(position, director.Stage.MainCamera, Mathf.RoundToInt(e.Amount).ToString(), hitPlayer ? Palette.Danger : Palette.Accent, e.Heavy ? 52f : 40f);

            if (hitPlayer)
            {
                playerView.Flash = 1f;
                director.Stage.Shake(e.Heavy ? 1f : 0.4f);
                if (e.Heavy) director.ScreenFx.Flash(Palette.Danger, 0.2f);
            }
            else if (viewById.TryGetValue(e.ShipId, out var view))
            {
                view.Flash = 1f;
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
            var mesh = AmmoMesh(e.Heavy ? e.AmmoId ?? "round" : "round", out var meshScale);
            shot.GetComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : e.Heavy ? heavyShotMesh : lightShotMesh;
            shot.GetComponent<MeshRenderer>().sharedMaterial = mesh != null ? shotKitMaterial : shotMaterial;
            shot.transform.localScale = Vector3.one * (mesh == null ? 1f : e.Heavy ? meshScale : meshScale * 0.6f);
            var isTorpedo = e.AmmoId == Torpedo;
            shot.transform.rotation = Quaternion.identity;
            shot.transform.position = new Vector3(e.X, e.Y + ProjectileHeight, e.Z);
            var trail = shot.GetComponent<TrailRenderer>();
            trail.enabled = director.Stage.HighDefinition || isTorpedo;
            trail.widthMultiplier = isTorpedo ? 2.6f : e.Heavy ? 0.7f : 0.4f;
            trail.time = isTorpedo ? 0.9f : 0.22f;
            trail.Clear();
            shot.SetActive(true);
            shots[e.ProjectileId] = shot;
        }

        // The kit's model for a shot type, shared by every shot of that type.
        Mesh AmmoMesh(string ammoId, out float scale)
        {
            scale = 3.6f;
            string asset;
            switch (ammoId)
            {
                case "chain": asset = "SM_Prop_ChainShot"; break;
                case "fire": asset = "SM_Prop_FireShot"; break;
                case "grapeshot": asset = "SM_Prop_Grapeshot"; break;
                case "mortar": asset = "SM_Prop_MortarShell"; break;
                case "missile": asset = "SM_Prop_Missile"; scale = 4.5f; break;
                case Torpedo: asset = "SM_Prop_Missile"; scale = 10f; break;
                default: asset = "SM_Prop_Cannonball"; break;
            }
            if (ammoMeshes.TryGetValue(asset, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>($"Art/{asset}_{KitArt.Quality}");
            var filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            ammoMeshes[asset] = mesh;
            return mesh;
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

        IEnumerator Finish()
        {
            hud.SetActive(false);
            CloseOverlays();
            hookLine.enabled = false;
            AudioBank.SetEngine(0f);
            var outcome = Encounter.Outcome;
            var boarded = Encounter.BoardedEnemy != null && viewById.TryGetValue(Encounter.BoardedEnemy.Ship.Id, out var boardedView) ? boardedView : null;
            var focusView = boarded ?? (Encounter.Target != null && viewById.TryGetValue(Encounter.Target.Ship.Id, out var targetView) ? targetView : playerView);

            if (outcome == SkyOutcome.Eaten)
            {
                yield return Swallow();
                if (director.Run != null) director.OnSkyFinished(Encounter);
                else director.OnEatenByWhale();
                yield break;
            }
            if (outcome == SkyOutcome.Lost)
            {
                director.Stage.FrameTarget(playerView.Model.Root.transform, playerView.Model.Body, new Vector3(-30f, 16f, -34f), 42f, 1f);
                yield return new WaitForSecondsRealtime(2.2f);
                if (director.Run != null) director.OnSkyFinished(Encounter);
                else director.OnDogfightLost();
                yield break;
            }

            var banner = outcome == SkyOutcome.Boarded ? "BOARDING!" : outcome == SkyOutcome.TargetEscaped ? "THEY GOT AWAY!" : director.Run != null ? "SKY CLEAR!" : "ENEMY CRIPPLED!";
            director.ScreenFx.Milestone(banner, outcome == SkyOutcome.TargetEscaped ? Palette.Danger : Palette.Accent);
            director.Stage.FrameTarget(focusView.Model.Root.transform, focusView.Model.Body, new Vector3(30f, 16f, -34f), 42f, 1f);
            yield return new WaitForSecondsRealtime(2f);

            if (director.Run != null)
            {
                director.OnSkyFinished(Encounter);
            }
            else if (outcome == SkyOutcome.Boarded)
            {
                director.OnDogfightWon();
                director.Board();
            }
            else
            {
                director.OnDogfightWon();
                BuildBoardPrompt();
            }
        }

        void BuildBoardPrompt()
        {
            var economy = director.Content.economy;
            var boarding = Airships.Core.Encounter.BoardingWin(economy, director.EncounterTier);

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
            foreach (var view in views)
            {
                if (swallowing && view == playerView) continue;
                PlaceShip(view.Model, view.State, view.Previous, view.PreviousHeading, alpha);
                RattleShip(view);
            }
            hazardViews.Update(alpha, new Vector3(player.X, player.Y, player.Z));
            zoneViews.Update(player);
            var riding = player.Alive && Sim.Weather.WindScale(player, 2f) > 1f;
            if (riding && !wasRiding) AudioBank.Play(Sfx.BoostIgnite, 0.6f);
            wasRiding = riding;
            UpdateHookLine();

            if (!ended)
            {
                // The proxy swings behind the ship more slowly than it turns, which reveals the turn.
                var playerRoot = playerView.Model.Root.transform;
                cameraYaw = Mathf.LerpAngle(cameraYaw, playerRoot.eulerAngles.y, 1f - Mathf.Exp(-3f * Time.deltaTime));
                cameraProxy.SetPositionAndRotation(playerRoot.position, Quaternion.Euler(0f, cameraYaw, 0f));
            }

            foreach (var projectile in Sim.Projectiles)
            {
                if (!shots.TryGetValue(projectile.Id, out var shot)) continue;
                shot.transform.position = new Vector3(
                    projectile.X + projectile.VelocityX * DogfightSim.TickSeconds * alpha,
                    projectile.Y + projectile.VelocityY * DogfightSim.TickSeconds * alpha + ProjectileHeight,
                    projectile.Z + projectile.VelocityZ * DogfightSim.TickSeconds * alpha);
                if (missiles.Contains(projectile.Id))
                {
                    // The missile's model points along +Y, and it needs to point where it's going.
                    shot.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(projectile.VelocityX, projectile.VelocityY, projectile.VelocityZ));
                    Fx.Burst(shot.transform.position, Palette.Spark, 1, 1f, 1.6f, 0.35f);
                }
                else
                {
                    shot.transform.Rotate(180f * Time.deltaTime, 240f * Time.deltaTime, 0f, Space.Self);
                }
            }

            foreach (var view in views)
            {
                view.Flash = Mathf.MoveTowards(view.Flash, 0f, Time.deltaTime * 6f);
                var color = view == playerView ? Palette.Danger * 0.7f : Palette.Spark * 0.6f;
                view.Model.Material.SetColor("_Emission", color * view.Flash);
            }

            smokeTimer -= Time.deltaTime;
            if (smokeTimer <= 0f)
            {
                smokeTimer = 0.14f;
                foreach (var view in views)
                {
                    if (view.Model.Root.activeSelf) EmitDamageSmoke(view.State, view.Model);
                }
                MarkWeakPoints();
            }

            foreach (var view in views) UpdateEmitters(view);
            foreach (var pickup in pickupViews)
            {
                pickup.Model.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
                pickup.Model.position = new Vector3(pickup.Pickup.X, pickup.Base + Mathf.Sin(bobTime * 1.4f + pickup.Pickup.Id) * 1.2f, pickup.Pickup.Z);
            }
            if (rain != null) rain.uvRect = new Rect(Time.time * 0.05f, Time.time * 1.2f, rain.rectTransform.rect.width / 512f, rain.rectTransform.rect.height / 512f);
        }

        // Shakes the hull on top of its usual motion, and dies away quickly.
        void RattleShip(ShipView view)
        {
            if (view.Rattle <= 0f) return;
            view.Rattle = Mathf.Max(0f, view.Rattle - Time.deltaTime / RattleSeconds);
            var strength = view.Rattle * view.Rattle;
            var time = Time.time * 38f;
            var body = view.Model.Body;
            body.localPosition += new Vector3(Mathf.PerlinNoise(time, 0f) - 0.5f, Mathf.PerlinNoise(0f, time) - 0.5f, 0f) * (3.2f * strength * view.Model.Scale);
            body.localRotation *= Quaternion.Euler((Mathf.PerlinNoise(time, 7f) - 0.5f) * 22f * strength, 0f, (Mathf.PerlinNoise(7f, time) - 0.5f) * 30f * strength);
        }

        void UpdateEmitters(ShipView view)
        {
            var state = view.State;
            var alive = state.Alive;
            var share = state.Hull / Mathf.Max(1f, state.Loadout.Hull);
            var burning = Encounter.BetrayalStarted && view == playerView || (state.Hull > 0f && !state.Departed && (share < 0.35f || state.BurnTimer > 0f));
            SetActive(view.Fires, burning);
            SetActive(view.Embers, burning);
            SetActive(view.Smoke, burning || (alive && share < 0.5f));
            SetActive(view.Exhaust, alive && state.Speed > state.Loadout.Speed * 0.85f);
            SetActive(view.Burners, alive && (state.BoostTimer > 0f || state.OverdriveTimer > 0f));
            SetActive(view.Leaks, alive && state.EnvelopeOut);
        }

        // With Morrow's knowledge aboard, each enemy's stern glows where the weak point is.
        void MarkWeakPoints()
        {
            if (!player.WeakPoints) return;
            foreach (var view in views)
            {
                if (view == playerView || !view.State.Alive) continue;
                var points = KitArt.MatchSockets(view.Model.Kit, new[] { "weakpoint_*" });
                if (points.Count == 0)
                {
                    Fx.Burst(view.Model.Body.position - view.Model.Root.transform.forward * view.State.Loadout.Radius + Vector3.up * 3f, Palette.Accent, 1, 0.5f, 1.6f, 0.3f);
                }
                foreach (var point in points) Fx.Burst(point.position, Palette.Accent, 1, 0.5f, 1.8f, 0.3f);
            }
        }

        void UpdateHookLine()
        {
            ShipView a = null;
            ShipView b = null;
            foreach (var view in views)
            {
                if (!view.State.Alive) continue;
                if (view.State.TetheredTo >= 0 && viewById.TryGetValue(view.State.TetheredTo, out var other))
                {
                    a = view;
                    b = other;
                }
                else if (view.State.Hooks > 0)
                {
                    a = playerView;
                    b = view;
                }
            }
            hookLine.enabled = a != null && !ended;
            var tethered = a != null && a.State.TetheredTo >= 0;
            if (hookProp == null)
            {
                hookProp = KitArt.Spawn("SM_Prop_GrapplingHook", transform);
                harpoonProp = KitArt.Spawn("SM_Prop_Harpoon", transform);
                KitArt.Paint(hookProp, fittingMaterial);
                KitArt.Paint(harpoonProp, fittingMaterial);
                hookProp.transform.localScale = Vector3.one * 3f;
                harpoonProp.transform.localScale = Vector3.one * 3f;
            }
            hookProp.SetActive(a != null && !tethered && !ended);
            harpoonProp.SetActive(a != null && tethered && !ended);
            if (a == null) return;
            var from = a.Model.Body.position + Vector3.up * 3f;
            var to = b.Model.Body.position + Vector3.up * 3f;
            hookLine.SetPosition(0, from);
            hookLine.SetPosition(1, to);
            var head = tethered ? harpoonProp : hookProp;
            head.transform.position = to;
            head.transform.rotation = Quaternion.FromToRotation(Vector3.up, from - to);

            // The winch throws sparks while it reels someone in.
            winchTimer -= Time.deltaTime;
            if (tethered && winchTimer <= 0f)
            {
                winchTimer = 0.2f;
                Effects.Play("WinchSparks", from, 1.4f);
            }
        }

        static void EmitDamageSmoke(ShipState ship, ShipModel model)
        {
            if (ship.EnginesOut && ship.Alive) Fx.Burst(model.Body.position + model.Root.transform.forward * -ship.Loadout.Radius + Vector3.up * 2f, Palette.Ink, 1, 2f, 3f, 1.2f, 5f);
        }

        void PlaceShip(ShipModel model, ShipState ship, Vector3 previous, float previousHeading, float alpha)
        {
            var at = Vector3.Lerp(previous, new Vector3(ship.X, ship.Y, ship.Z), alpha);
            var heading = Mathf.LerpAngle(previousHeading * Mathf.Rad2Deg, ship.Heading * Mathf.Rad2Deg, alpha);
            model.Root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, heading, 0f));

            var alive = ship.Hull > 0f;
            var bank = alive ? Mathf.Clamp(-ship.TurnRate * Mathf.Rad2Deg * 0.3f, -22f, 22f) : 14f;
            var current = model.Body.localEulerAngles.z;
            var roll = Mathf.LerpAngle(current, bank, 1f - Mathf.Exp(-4f * Time.deltaTime));
            var climbPitch = -ship.ClimbRate / DogfightSim.ClimbSpeed * 14f;
            var pitch = alive ? Mathf.Sin(bobTime * 1.3f + ship.Id) * 1.2f + climbPitch : 6f;

            // A roll spins the way it throws the ship: over the side, or nose up or nose down and over.
            var rollSpin = ship.EvadeTimer > 0f ? (1f - ship.EvadeTimer / director.Content.skyRules.evadeSeconds) * 360f : 0f;
            model.Body.localRotation = Quaternion.Euler(pitch - ship.EvadeLift * rollSpin, 0f, roll - ship.EvadeSide * rollSpin);

            var sink = alive ? 0f : -2.4f;
            var height = Mathf.Sin(bobTime * 1.1f + ship.Id) * 0.5f + sink;
            model.Body.localPosition = Vector3.Lerp(model.Body.localPosition, Vector3.up * height, 1f - Mathf.Exp(-3f * Time.deltaTime));

            var spin = alive && !ship.EnginesOut ? 900f * (ship.Speed / ship.Loadout.Speed) : 60f;
            foreach (var propeller in model.Propellers) propeller.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
        }

        void BuildHud()
        {
            canvas = UIKit.CreateCanvas("Dogfight Hud", transform, 10);
            var safe = UIKit.SafeArea(canvas.transform);
            safeArea = safe;
            hud = safe.gameObject;
            ammoChoices = Campaign.AvailableAmmo(director.Content, director.Save);

            stickZone = UIKit.Node("Stick Zone", safe);
            stickZone.anchorMin = Vector2.zero;
            stickZone.anchorMax = new Vector2(0.48f, 0.82f);
            stickZone.offsetMin = Vector2.zero;
            stickZone.offsetMax = Vector2.zero;
            stick = VirtualStick.Create(stickZone);
            AddStickLabel(stickZone, "CLIMB", 118f, "Hud_AltitudeUp");
            AddStickLabel(stickZone, "DIVE", -118f, "Hud_AltitudeDown");
            Teachable("stick", stickZone.Find("Stick Base"));

            bowButton = BuildFireButton(safe, "TORPEDO", "Ammo_Missile", new Vector2(-30f, 250f), 200f, Palette.PanelLight, out bowCooldown);
            broadsideButton = BuildFireButton(safe, "BROADSIDE", "Hud_FireBroadside", new Vector2(-250f, 40f), 250f, Palette.PlayerPaint, out broadsideCooldown);
            boostButton = BuildFireButton(safe, "BOOST / ROLL", "Hud_ThrottleUp", new Vector2(-30f, 40f), 190f, Palette.PanelLight, out boostCooldown);
            grappleButton = BuildFireButton(safe, "GRAPPLE", "Hud_Grapple", new Vector2(-270f, 300f), 190f, Palette.Brass, out grappleCooldown);
            grappleLabel = grappleButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var signatureName = player.Loadout.Signature != null ? player.Loadout.Signature.name : "";
            signatureButton = BuildFireButton(safe, signatureName, SignatureIcon(player.Loadout.Signature), new Vector2(-30f, 460f), 170f, Palette.Storm, out signatureCooldown);
            Teachable("bow", bowButton.transform);
            Teachable("broadside", broadsideButton.transform);
            Teachable("boost", boostButton.transform);
            Teachable("grapple", grappleButton.transform);
            Teachable("multihook", grappleButton.transform);
            Teachable("signature", signatureButton.transform);

            aimButton = SmallButton(safe, "AIM: HULL", "Target_Reticle", new Vector2(-250f, 500f), new Vector2(230f, 84f), CycleAim, out aimIcon);
            ammoButton = SmallButton(safe, "AMMO", "Ammo_Round", new Vector2(-30f, 640f), new Vector2(250f, 84f), CycleAmmo, out ammoIcon);
            ammoButton.ClickSound = Sfx.AmmoLoad;
            patchButton = SmallButton(safe, "PATCH", "Hud_Repair", new Vector2(-540f, 640f), new Vector2(200f, 84f), () => { patchPressed = true; Encounter.MarkUsed("patch"); }, out _);
            overdriveButton = SmallButton(safe, "OVERDRIVE", "Module_LiftCore", new Vector2(-290f, 640f), new Vector2(240f, 84f), () => { overdrivePressed = true; Encounter.MarkUsed("overdrive"); }, out _);
            Teachable("aim", aimButton.transform);
            Teachable("ammo", ammoButton.transform);
            Teachable("patch", patchButton.transform);
            Teachable("overdrive", overdriveButton.transform);
            RefreshAmmoLabel();

            BuildAltitudeGauge(safe);

            playerHull = BuildHullBar(safe, director.Sky.PlayerShipName, new Vector2(0f, 1f), new Vector2(28f, -20f), Palette.Heal, out playerHullText, out _);
            enemyHull = BuildHullBar(safe, "", new Vector2(1f, 1f), new Vector2(-28f, -20f), Palette.Danger, out enemyHullText, out enemyName);
            boardLine = UIKit.Solid("Boarding Line", enemyHull.transform, Palette.Accent);
            var share = director.Content.skyRules.mainBoardHullShare;
            boardLine.rectTransform.anchorMin = new Vector2(share, 0f);
            boardLine.rectTransform.anchorMax = new Vector2(share, 1f);
            boardLine.rectTransform.sizeDelta = new Vector2(6f, 0f);
            boardLine.gameObject.SetActive(false);
            BuildEnemyParts(safe);
            playerHull.Set(player.Hull / player.Loadout.Hull, true);

            objectiveText = UIKit.Text("Objective", safe, ObjectiveLine(), 28f, Palette.Accent);
            objectiveText.outlineWidth = 0.2f;
            objectiveText.outlineColor = Palette.Ink;
            UIKit.Place(objectiveText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 12f), new Vector2(980f, 40f));
            statusText = UIKit.Text("Status", safe, "", 34f, Palette.Storm);
            statusText.outlineWidth = 0.2f;
            statusText.outlineColor = Palette.Ink;
            UIKit.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 58f), new Vector2(980f, 46f));
            statusIcon = UIKit.Icon("Status Icon", safe, "Hazard_WindCurrent");
            UIKit.Place(statusIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 104f), Vector2.one * 64f);
            statusIcon.enabled = false;
            BuildCrewAbilities(safe);
            var hullIcon = UIKit.Icon("Hull Icon", safe, "Hud_Hull");
            UIKit.Place(hullIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(650f, -30f), Vector2.one * 60f);

            var pause = UIKit.Button("Pause", safe, "", 44f, Palette.WithAlpha(Palette.Panel, 0.9f), Pause);
            var pauseIcon = UIKit.Icon("Pause Icon", pause.transform, "Hud_Pause");
            UIKit.Fill(pauseIcon.rectTransform);
            pauseIcon.rectTransform.offsetMin = Vector2.one * 14f;
            pauseIcon.rectTransform.offsetMax = Vector2.one * -14f;
            UIKit.Place((RectTransform)pause.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(112f, 96f));

            var whaleImage = UIKit.Solid("Whale Marker", safe, Palette.Storm);
            whaleMarker = whaleImage.rectTransform;
            whaleMarker.sizeDelta = new Vector2(38f, 38f);
            whaleMarker.anchorMin = Vector2.zero;
            whaleMarker.anchorMax = Vector2.zero;
            whaleMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            whaleMarkerLabel = UIKit.Text("Whale Distance", safe, "", 26f, Palette.Storm);
            whaleMarkerLabel.outlineWidth = 0.2f;
            whaleMarkerLabel.outlineColor = Palette.Ink;
            whaleMarkerLabel.rectTransform.sizeDelta = new Vector2(260f, 40f);
            whaleMarkerLabel.rectTransform.anchorMin = Vector2.zero;
            whaleMarkerLabel.rectTransform.anchorMax = Vector2.zero;

            whaleWarning = UIKit.Text("Whale Warning", safe, "", 56f, Palette.Danger);
            whaleWarning.outlineWidth = 0.25f;
            whaleWarning.outlineColor = Palette.Ink;
            UIKit.Place(whaleWarning.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1100f, 80f));

            if (Encounter.HasControl("radar")) BuildRadar(safe);
            comms = CommsBar.Create(safe, director.Content.campaign, -124f, false);
            CommsLine line;
            while ((line = Encounter.NextComms()) != null) comms.Say(line);
            RefreshControls();
        }

        // A row of small badges for what the crew brings to the fight.
        void BuildCrewAbilities(RectTransform safe)
        {
            var badges = new List<string>();
            if (Encounter.HasControl("aim")) badges.Add("Ability_RookPrecision");
            if (Encounter.HasControl("winds")) badges.Add("Ability_LyraWinds");
            if (Encounter.HasControl("clouds")) badges.Add("Ability_LyraCloudStealth");
            if (Encounter.HasControl("sabotage")) badges.Add("Ability_SableSabotage");
            if (Encounter.HasControl("radar")) badges.Add("Ability_MorrowRadar");
            if (player.WeakPoints) badges.Add("Marker_WeakPoint");
            for (var i = 0; i < badges.Count; i++)
            {
                var badge = UIKit.Icon("Crew Ability", safe, badges[i]);
                UIKit.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(286f + i * 62f, -124f), Vector2.one * 56f);
                abilityIcons.Add(badge);
            }
        }

        string ObjectiveLine()
        {
            if (director.Run == null) return "";
            switch (director.Sky.Objective)
            {
                case "capture":
                    var main = Encounter.Main;
                    if (main == null) return "";
                    return Sim.IsBoardable(main.Ship) ? $"BOARD {main.Spawn.name}: close in and GRAPPLE" : $"BOARD {main.Spawn.name}: beat them under {Mathf.RoundToInt(director.Content.skyRules.mainBoardHullShare * 100f)}% hull";
                case "disable_engines": return "Knock out their engines";
                case "survive": return "Survive the storm";
                case "collect": return $"Collect them all: {Encounter.PickupsTaken} of {Encounter.Pickups.Count}";
                case "escort":
                    var left = Mathf.CeilToInt(director.Sky.Phase.seconds - Encounter.Time);
                    return left > 0 ? $"Keep them flying: {left} s, or clear the sky" : "Keep them flying";
                case "repel": return "Shoot down the gliders before they land";
                default: return "Clear the sky";
            }
        }

        void Teachable(string control, Transform target) => teachable.Add((control, target));

        static string SignatureIcon(SignatureDef signature)
        {
            if (signature == null) return null;
            switch (signature.kind)
            {
                case "harpoon": return "Module_HarpoonWinch";
                case "afterburner": return "Module_Afterburner";
                case "ram": return "Ship_Bastion";
                default: return "Module_SiegeCannon";
            }
        }

        static GameButton SmallButton(RectTransform safe, string label, string iconName, Vector2 position, Vector2 size, System.Action action, out Image icon)
        {
            var button = UIKit.Button(label, safe, label, 24f, Palette.WithAlpha(Palette.PanelLight, 0.92f), action);
            UIKit.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), position, size);
            button.Label.characterSpacing = 0f;
            button.Label.rectTransform.offsetMin = new Vector2(62f, 4f);
            icon = UIKit.Icon("Icon", button.transform, iconName);
            UIKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), Vector2.one * 56f);
            return button;
        }

        void CycleAim()
        {
            var next = player.Aim == ShipPart.Hull ? ShipPart.Engines : player.Aim == ShipPart.Engines ? ShipPart.Guns : player.Aim == ShipPart.Guns ? ShipPart.Envelope : ShipPart.Hull;
            player.Aim = next;
            aimButton.Label.text = next == ShipPart.Hull ? "AIM: HULL" : next == ShipPart.Engines ? "AIM: ENGINES" : next == ShipPart.Guns ? "AIM: GUNS" : "AIM: BALLOON";
            aimIcon.sprite = KitArt.Icon(next == ShipPart.Hull ? "Target_Reticle" : next == ShipPart.Engines ? "Target_Sails" : next == ShipPart.Guns ? "Target_Guns" : "Target_Envelope");
            Encounter.MarkUsed("aim");
        }

        void CycleAmmo()
        {
            if (ammoChoices.Count == 0) return;
            ammoIndex = (ammoIndex + 1) % ammoChoices.Count;
            player.Ammo = ammoChoices[ammoIndex];
            RefreshAmmoLabel();
            Encounter.MarkUsed("ammo");
        }

        void RefreshAmmoLabel()
        {
            if (player.Ammo == null) return;
            ammoButton.Label.text = player.Ammo.name;
            if (!string.IsNullOrEmpty(player.Ammo.icon) && ammoIcon != null) ammoIcon.sprite = KitArt.Icon(player.Ammo.icon);
        }

        void BuildEnemyParts(RectTransform safe)
        {
            var panel = UIKit.Panel("Enemy Parts", safe, Palette.WithAlpha(Palette.Panel, 0.88f));
            UIKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -122f), new Vector2(620f, 50f));
            enemyParts = panel.gameObject;
            enemyEngines = AddPartBar(panel.transform, "Target_Sails", 8f, Palette.Accent);
            enemyGuns = AddPartBar(panel.transform, "Target_Guns", 210f, Palette.Mana);
            enemyEnvelope = AddPartBar(panel.transform, "Target_Envelope", 412f, Palette.Heal);
        }

        static Bar AddPartBar(Transform parent, string icon, float x, Color color)
        {
            var image = UIKit.Icon("Part", parent, icon);
            UIKit.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), Vector2.one * 42f);
            var bar = UIKit.Bar("Part Bar", parent, color);
            UIKit.Place((RectTransform)bar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 46f, 0f), new Vector2(146f, 26f));
            return bar;
        }

        void BuildRadar(RectTransform safe)
        {
            var image = new GameObject("Radar", typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(safe, false);
            image.sprite = UIKit.Circle;
            image.color = Palette.WithAlpha(Palette.Ink, 0.7f);
            image.raycastTarget = false;
            radar = UIKit.Place(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, -130f), Vector2.one * 190f);
            var center = UIKit.Solid("You", radar, Palette.Heal);
            UIKit.Place(center.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 10f);
        }

        void UpdateRadar()
        {
            if (radar == null) return;
            var index = 0;
            foreach (var view in views)
            {
                if (view == playerView || !view.State.Alive) continue;
                if (index >= radarDots.Count)
                {
                    var dot = UIKit.Solid("Dot", radar, Palette.Danger);
                    dot.rectTransform.sizeDelta = Vector2.one * 12f;
                    dot.rectTransform.anchorMin = Vector2.one * 0.5f;
                    dot.rectTransform.anchorMax = Vector2.one * 0.5f;
                    radarDots.Add(dot.rectTransform);
                }
                radarDots[index].gameObject.SetActive(true);
                radarDots[index].GetComponent<Image>().color = view.State.Team == player.Team ? Palette.Heal : Palette.Danger;
                radarDots[index].anchoredPosition = RadarPosition(view.State.X, view.State.Z);
                index++;
            }
            for (; index < radarDots.Count; index++) radarDots[index].gameObject.SetActive(false);

            for (var i = 0; i < Sim.Field.Whales.Count; i++)
            {
                if (i >= whaleDots.Count)
                {
                    var dot = UIKit.Solid("Whale Dot", radar, Palette.WhaleDot);
                    dot.sprite = UIKit.Circle;
                    dot.rectTransform.sizeDelta = Vector2.one * 18f;
                    dot.rectTransform.anchorMin = Vector2.one * 0.5f;
                    dot.rectTransform.anchorMax = Vector2.one * 0.5f;
                    whaleDots.Add(dot.rectTransform);
                }
                var whale = Sim.Field.Whales[i];
                whaleDots[i].anchoredPosition = RadarPosition(whale.X, whale.Z);
            }
        }

        // Where a point in the sky shows on the radar. Anything out of range sits on the rim.
        Vector2 RadarPosition(float x, float z)
        {
            var offset = new Vector2(x - player.X, z - player.Z);
            var angle = -player.Heading;
            var rotated = new Vector2(offset.x * Mathf.Cos(angle) - offset.y * Mathf.Sin(angle), offset.x * Mathf.Sin(angle) + offset.y * Mathf.Cos(angle));
            return Vector2.ClampMagnitude(rotated * RadarScale, RadarRim);
        }

        // Each control appears only once the story has introduced it, and pulses until it's been tried.
        void RefreshControls()
        {
            stickZone.Find("Stick Base").gameObject.SetActive(Encounter.HasControl("stick") || GameDirector.Autoplay);
            bowButton.gameObject.SetActive(Encounter.HasControl("bow"));
            broadsideButton.gameObject.SetActive(Encounter.HasControl("broadside"));
            boostButton.gameObject.SetActive(Encounter.HasControl("boost"));
            grappleButton.gameObject.SetActive(Encounter.HasControl("grapple"));
            signatureButton.gameObject.SetActive(Encounter.HasControl("signature") && player.Loadout.Signature != null);
            aimButton.gameObject.SetActive(Encounter.HasControl("aim"));
            ammoButton.gameObject.SetActive(Encounter.HasControl("ammo") || ammoChoices.Count > 1);
            patchButton.gameObject.SetActive(Encounter.HasControl("patch"));
            overdriveButton.gameObject.SetActive(Encounter.HasControl("overdrive"));
            altitudeGauge.SetActive(Encounter.CanClimb);
            var altitudeLabels = stickZone.Find("Stick Base");
            foreach (Transform child in altitudeLabels)
            {
                if (child.name == "CLIMB" || child.name == "DIVE") child.gameObject.SetActive(Encounter.CanClimb);
            }

            foreach (var (control, target) in teachable)
            {
                if (Encounter.IsTeaching(control)) target.localScale = Vector3.one * (1f + 0.12f * Mathf.PingPong(Time.unscaledTime * 3f, 1f));
            }
            foreach (var (control, target) in teachable)
            {
                if (!Encounter.IsTeaching(control) && !IsPulsing(target)) target.localScale = Vector3.one;
            }
        }

        bool IsPulsing(Transform target)
        {
            foreach (var (control, item) in teachable)
            {
                if (item == target && Encounter.IsTeaching(control)) return true;
            }
            return false;
        }

        static void AddStickLabel(RectTransform stickZone, string label, float y, string icon)
        {
            var text = UIKit.Text(label, stickZone.Find("Stick Base"), label, 22f, Palette.TextLight);
            text.outlineWidth = 0.2f;
            text.outlineColor = Palette.Ink;
            UIKit.Place(text.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0f, y), new Vector2(160f, 34f));
            var image = UIKit.Icon("Glyph", text.transform, icon);
            UIKit.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), Vector2.one * 34f);
        }

        void BuildAltitudeGauge(RectTransform safe)
        {
            var track = UIKit.Panel("Altitude Gauge", safe, Palette.WithAlpha(Palette.Panel, 0.75f));
            UIKit.Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 90f), new Vector2(22f, GaugeHeight));
            altitudeGauge = track.gameObject;
            var label = UIKit.Text("Altitude Label", track.transform, "ALT", 22f, Palette.TextLight, TextAlignmentOptions.Left);
            UIKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(80f, 30f));
            enemyAltitudeTick = BuildAltitudeTick(track.transform, Palette.Danger, 34f);
            playerAltitudeTick = BuildAltitudeTick(track.transform, Palette.Heal, 44f);
        }

        static RectTransform BuildAltitudeTick(Transform track, Color color, float width)
        {
            var tick = UIKit.Solid("Tick", track, color);
            tick.rectTransform.anchorMin = new Vector2(0f, 0f);
            tick.rectTransform.anchorMax = new Vector2(0f, 0f);
            tick.rectTransform.pivot = new Vector2(0f, 0.5f);
            tick.rectTransform.sizeDelta = new Vector2(width, 10f);
            return tick.rectTransform;
        }

        void PlaceAltitudeTick(RectTransform tick, float altitude)
        {
            var share = Mathf.InverseLerp(DogfightSim.MinAltitude, DogfightSim.MaxAltitude, altitude);
            tick.anchoredPosition = new Vector2(0f, share * GaugeHeight);
        }

        static HoldButton BuildFireButton(RectTransform safe, string label, string iconName, Vector2 position, float size, Color color, out Image cooldown)
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

            var icon = UIKit.Icon("Icon", body.transform, iconName);
            UIKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.09f), Vector2.one * size * 0.56f);
            var text = UIKit.Text("Label", body.transform, label, Mathf.Max(20f, size * 0.105f), Palette.TextLight);
            text.outlineWidth = 0.2f;
            text.outlineColor = Palette.Ink;
            text.characterSpacing = 0f;
            UIKit.Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, size * 0.09f), new Vector2(size * 0.95f, size * 0.22f));
            return body.gameObject.AddComponent<HoldButton>();
        }

        static Bar BuildHullBar(RectTransform safe, string label, Vector2 anchor, Vector2 position, Color color, out TextMeshProUGUI value, out TextMeshProUGUI name)
        {
            var panel = UIKit.Panel(label, safe, Palette.WithAlpha(Palette.Panel, 0.88f));
            UIKit.Place(panel.rectTransform, anchor, anchor, position, new Vector2(620f, 98f));
            name = UIKit.Text("Name", panel.transform, label, 28f, Palette.TextLight, TextAlignmentOptions.Left);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(480f, 38f));
            value = UIKit.Text("Value", panel.transform, "", 28f, Palette.Accent, TextAlignmentOptions.Right);
            UIKit.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -8f), new Vector2(160f, 38f));
            var bar = UIKit.Bar("Hull", panel.transform, color);
            UIKit.Place((RectTransform)bar.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(584f, 38f));
            return bar;
        }

        void UpdateHud()
        {
            var hullFailing = player.Hull < player.Loadout.Hull * DangerHullShare;
            AudioBank.SetDanger(Encounter.BetrayalStarted || hullFailing ? 1f : 0f);
            if (hullFailing && !hullAlarmRang) AudioBank.Play(Sfx.HullAlarm, 0.65f, 0f);
            hullAlarmRang = hullFailing;
            if (!Mathf.Approximately(player.Hull, lastPlayerHull))
            {
                lastPlayerHull = player.Hull;
                playerHull.Set(player.Hull / player.Loadout.Hull);
            }
            playerHullText.text = Mathf.CeilToInt(player.Hull).ToString();
            bowCooldown.fillAmount = player.BowCooldown / player.Loadout.Bow.reload;
            broadsideCooldown.fillAmount = Mathf.Clamp01(player.BroadsideCooldown / player.Loadout.Broadside.reload);
            if (player.Loadout.Signature != null) signatureCooldown.fillAmount = player.SignatureCooldown / Mathf.Max(0.01f, player.Loadout.Signature.cooldown);
            boostCooldown.fillAmount = player.EvadeCooldown / Mathf.Max(0.01f, director.Content.skyRules.evadeCooldown);
            PlaceAltitudeTick(playerAltitudeTick, player.Y);
            UpdateFocus();
            RefreshControls();
            UpdateGrappleButton();
            UpdateStatus();
            UpdateMarkers();
            UpdateRadar();
            UpdateWhaleHud();
            patchButton.Interactable = player.PatchesLeft > 0;
            patchButton.Label.text = player.PatchesLeft > 0 ? "PATCH" : "PATCHED";
            overdriveButton.Interactable = player.OverdriveCooldown <= 0f;
        }

        // The enemy bar follows the objective's ship, or whoever's closest once that's settled.
        void UpdateFocus()
        {
            var target = Encounter.Target?.Ship;
            var next = target != null && target.Alive ? target : Sim.Opponent(player);
            if (next != null && next != focus)
            {
                focus = next;
                enemyName.text = viewById.TryGetValue(next.Id, out var view) ? view.Name : "";
                enemyHull.Set(next.Hull / next.Loadout.Hull, true);
            }
            if (focus == null) return;
            var hullShare = focus.Hull / focus.Loadout.Hull;
            enemyHull.Set(hullShare);
            boardLine.gameObject.SetActive(focus.Boarding == BoardingRule.Main);
            enemyHullText.text = focus.Boarding == BoardingRule.Main && Sim.IsBoardable(focus) ? "BOARD!" : $"{Mathf.CeilToInt(hullShare * 100f)}%";
            PlaceAltitudeTick(enemyAltitudeTick, focus.Y);
            enemyParts.SetActive(Encounter.HasControl("aim"));
            if (enemyParts.activeSelf)
            {
                enemyEngines.Set(focus.Engines / focus.MaxSubsystem);
                enemyGuns.Set(focus.Guns / focus.MaxSubsystem);
                enemyEnvelope.Set(focus.Envelope / focus.MaxSubsystem);
            }
            UpdateEnemyRows();
        }

        // Every other enemy still flying gets its own bar under the main one.
        void UpdateEnemyRows()
        {
            var top = enemyParts.activeSelf ? -176f : -122f;
            var shown = 0;
            foreach (var enemy in Encounter.Enemies)
            {
                var ship = enemy.Ship;
                if (shown >= MaxEnemyRows || ship == focus || ship.Team == player.Team || !ship.Alive || ship.BoardsOnContact) continue;
                if (shown == enemyRows.Count) enemyRows.Add(BuildEnemyRow());
                var row = enemyRows[shown];
                var share = ship.Hull / ship.Loadout.Hull;
                row.Root.SetActive(true);
                ((RectTransform)row.Root.transform).anchoredPosition = new Vector2(-28f, top - shown * EnemyRowHeight);
                if (row.Ship != ship)
                {
                    row.Ship = ship;
                    row.Name.text = viewById.TryGetValue(ship.Id, out var view) ? view.Name : "";
                    row.Hull.Set(share, true);
                }
                row.Hull.Set(share);
                shown++;
            }
            for (var i = shown; i < enemyRows.Count; i++) enemyRows[i].Root.SetActive(false);
        }

        EnemyRow BuildEnemyRow()
        {
            var panel = UIKit.Panel("Enemy Row", safeArea, Palette.WithAlpha(Palette.Panel, 0.88f));
            UIKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(620f, 42f));
            var name = UIKit.Text("Name", panel.transform, "", 22f, Palette.TextLight, TextAlignmentOptions.Left);
            name.characterSpacing = 0f;
            UIKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(280f, 34f));
            var bar = UIKit.Bar("Hull", panel.transform, Palette.Danger);
            UIKit.Place((RectTransform)bar.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(306f, 26f));
            return new EnemyRow { Root = panel.gameObject, Hull = bar, Name = name };
        }

        void UpdateGrappleButton()
        {
            var target = Sim.BoardingTarget(player);
            grappleCooldown.fillAmount = target != null ? 0f : 0.65f;
            var hooked = target != null ? target.Hooks : 0;
            var needed = target != null ? target.HooksRequired : 0;
            var dragged = target != null && target.TetheredTo == player.Id;
            grappleLabel.text = target == null ? "GRAPPLE" : dragged ? "RIDE IT!" : needed > 1 ? $"HOOK {hooked}/{needed}" : "BOARD!";
        }

        void UpdateStatus()
        {
            var text = "";
            var icon = (string)null;
            statusText.color = Palette.Storm;
            var incomingMissile = false;
            foreach (var projectile in Sim.Projectiles)
            {
                if (projectile.HomingOn == player.Id) incomingMissile = true;
            }
            if (incomingMissile || Encounter.VolleyIncoming)
            {
                text = (incomingMissile ? "MISSILE!" : "INCOMING!") + (player.EvadeCooldown <= 0f ? " TAP BOOST TO ROLL" : "");
                icon = incomingMissile ? "Ammo_Missile" : "Hud_FireBroadside";
                statusText.color = Palette.Danger;
            }
            else if (player.TetheredTo >= 0 || TetheredByEnemy())
            {
                text = "TETHERED!";
                icon = "Ammo_Harpoon";
                statusText.color = Palette.Accent;
            }
            else if (InZone(SkyZoneKind.Toxic))
            {
                text = "TOXIC SMOG: GET OUT";
                icon = "Hazard_ToxicCloud";
                statusText.color = Palette.Danger;
            }
            else if (InZone(SkyZoneKind.Updraft))
            {
                text = "UPDRAFT";
                icon = "Hazard_Updraft";
            }
            else if (Sim.Weather.Strikes.Count > 0)
            {
                text = "LIGHTNING!";
                icon = "Hazard_Lightning";
                statusText.color = Palette.Accent;
            }
            else if (player.Hidden)
            {
                text = "HIDDEN IN CLOUD: AMBUSH READY";
                icon = "Ability_LyraCloudStealth";
            }
            else if (player.OverdriveTimer > 0f) text = "OVERDRIVE!";
            else if (Sim.Weather != null && Sim.Weather.WindScale(player, 2f) > 1f)
            {
                text = "RIDING THE WIND";
                icon = "Hazard_WindCurrent";
            }
            statusText.text = text;
            statusIcon.enabled = icon != null;
            if (icon != null) statusIcon.sprite = KitArt.Icon(icon);
            objectiveText.text = ObjectiveLine();
        }

        bool TetheredByEnemy()
        {
            foreach (var enemy in Encounter.Enemies)
            {
                if (enemy.Ship.TetheredTo == player.Id && enemy.Ship.Alive) return true;
            }
            return false;
        }

        bool InZone(SkyZoneKind kind)
        {
            foreach (var zone in Sim.Weather.Zones)
            {
                if (zone.Kind != kind) continue;
                if (DogfightSim.DistanceSquared(player.X, player.Z, zone.X, zone.Z) <= zone.Radius * zone.Radius) return true;
            }
            return false;
        }

        void UpdateMarkers()
        {
            foreach (var view in views)
            {
                if (view == playerView || view.Marker == null || !view.Marker.gameObject.activeSelf) continue;
                if (!view.State.Alive) continue;
                var distance = Vector2.Distance(new Vector2(player.X, player.Z), new Vector2(view.State.X, view.State.Z));
                var above = Mathf.RoundToInt(view.State.Y - player.Y);

                // The label is bright when the broadside can reach the enemy's altitude, and dark when it can't.
                view.Label.text = !Encounter.CanClimb || Mathf.Abs(above) < 3 ? $"{Mathf.RoundToInt(distance)} m" : $"{Mathf.RoundToInt(distance)} m  {(above > 0 ? "+" : "")}{above}";
                view.Label.color = DogfightSim.CanElevate(player, view.State, player.Loadout.Broadside) ? Palette.Accent : Palette.Ink;
                PlaceMarker(view.Marker, view.Label.rectTransform, view.Model.Body.position + Vector3.up * 13f * view.Model.Scale);
                if (view.MarkerHull != null)
                {
                    view.MarkerHull.Set(view.State.Hull / view.State.Loadout.Hull, true);
                    ((RectTransform)view.MarkerHull.transform).anchoredPosition = view.Marker.anchoredPosition + Vector2.down * 38f;
                }
            }
            foreach (var pickup in pickupViews)
            {
                if (pickup.Pickup.Taken) continue;
                var world = pickup.Model.position + Vector3.up * 5f;
                PlaceMarker(pickup.Marker, pickup.Marker, world);
            }
        }

        void UpdateWhaleHud()
        {
            // Show whichever whale matters most: the one hunting, or else the closest.
            WhaleState whale = null;
            var distance = float.MaxValue;
            foreach (var candidate in Sim.Field.Whales)
            {
                var to = new Vector3(candidate.X - player.X, candidate.Y - player.Y, candidate.Z - player.Z).magnitude;
                if (whale != null && (whale.MouthOpen || (to >= distance && !candidate.MouthOpen))) continue;
                whale = candidate;
                distance = to;
            }
            var show = whale != null && (whale.MouthOpen || distance < WhaleMarkerRange);
            whaleMarker.gameObject.SetActive(show);
            whaleMarkerLabel.gameObject.SetActive(show);
            if (show)
            {
                whaleMarkerLabel.text = $"WHALE {Mathf.RoundToInt(distance)} m";
                whaleMarkerLabel.color = whale.MouthOpen ? Palette.Danger : Palette.Storm;
                PlaceMarker(whaleMarker, whaleMarkerLabel.rectTransform, new Vector3(whale.X, whale.Y, whale.Z));
            }

            var hunted = whale != null && whale.TargetId == player.Id;
            whaleWarning.text = !hunted ? "" : whale.Mode == WhaleMode.Lunging ? "IT'S COMING! GET OUT OF THE WAY!" : "THE SKY WHALE IS HUNTING YOU!";
            whaleWarning.color = Color.Lerp(Palette.Danger, Palette.Accent, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f));
        }

        // Marks a world position on screen, or pins to the screen edge pointing toward it when out of view.
        void PlaceMarker(RectTransform marker, RectTransform label, Vector3 target)
        {
            var parent = (RectTransform)marker.parent;
            var camera = director.Stage.MainCamera;
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
            label.anchoredPosition = local + Vector2.up * 44f;
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
