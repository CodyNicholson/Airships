using System;
using System.Collections;
using System.Collections.Generic;
using Airships.Core;
using UnityEngine;

namespace Airships.Game
{
    public enum GameState { Booting, Title, Port, Dogfight, BoardPrompt, Battle, Results }

    // Owns the save file and moves the game between screens, each behind a fade.
    public class GameDirector : MonoBehaviour
    {
        public const string PlayerShipId = "player_sloop";
        public const string EnemyShipId = "pirate_raider";
        public const string AutoplaySessionKey = "Airships.Autoplay";

        public static GameDirector Instance { get; private set; }

        // Plays the whole loop with no input, for automated checks. Never enabled for players.
        public static bool Autoplay { get; set; }

        public GameContent Content { get; private set; }
        public SaveData Save { get; private set; }
        public WorldStage Stage { get; private set; }
        public ScreenFx ScreenFx { get; private set; }
        public GameState State { get; private set; } = GameState.Booting;
        public int EncounterTier { get; private set; }
        public int LoopsCompleted { get; private set; }
        public MonoBehaviour CurrentScreen { get; private set; }
        public Transform ScreenRoot => screenRoot;
        public int Seed => seed;

        Transform screenRoot;
        Coroutine transition;
        int seed;
        EncounterReward dogfightReward;
        readonly List<string> levelUps = new List<string>();

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
#if UNITY_EDITOR
            Autoplay = UnityEditor.SessionState.GetBool(AutoplaySessionKey, false);
            SaveStore.SelectFile(Autoplay);
#endif
            seed = Environment.TickCount;
            Content = JsonUtility.FromJson<GameContent>(Resources.Load<TextAsset>("game_content").text);
            Save = SaveStore.Load(Content);

            GameTime.Reset();
            Stage = WorldStage.Create();
            Fx.Create(transform);
            AudioBank.Create(transform);
            UIKit.EnsureEventSystem(transform);
            ScreenFx = ScreenFx.Create(transform);
            screenRoot = new GameObject("Screen").transform;
            screenRoot.SetParent(transform, false);

            if (!Save.settings.definitionChosen)
            {
                // Sensible first-run default. The player can change it at any time in Settings.
                Save.settings.highDefinition = SystemInfo.systemMemorySize >= 4000;
                Save.settings.definitionChosen = true;
            }
            ApplySettings();
            Debug.Log($"[Flow] Boot. Gold {Save.gold}, encounters won {Save.encountersWon}, high definition {Save.settings.highDefinition}");
            ShowTitle();
        }

        void Update() => GameTime.Tick();

        void OnDestroy()
        {
            if (Instance == this) GameTime.Reset();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Save != null) SaveStore.Save(Save);
        }

        public void ApplySettings()
        {
            Stage.ApplyDefinition(Save.settings.highDefinition);
            AudioBank.SetVolumes(Save.settings.musicVolume, Save.settings.sfxVolume);
        }

        public void SaveGame() => SaveStore.Save(Save);

        public void ShowTitle() => Go(GameState.Title, root => root.gameObject.AddComponent<TitleScreen>());
        public void ShowPort() => Go(GameState.Port, root => root.gameObject.AddComponent<PortScreen>());

        public void StartDogfight()
        {
            EncounterTier = Encounter.Tier(Save);
            levelUps.Clear();
            seed++;
            Go(GameState.Dogfight, root => root.gameObject.AddComponent<DogfightScreen>());
        }

        // The dogfight reward is banked at once, so losing the boarding battle can't take it away.
        public void OnDogfightWon()
        {
            dogfightReward = Encounter.DogfightWin(Content.economy, EncounterTier);
            Encounter.Apply(Save, Content, dogfightReward, levelUps);
            Save.encountersWon++;
            SaveGame();
            State = GameState.BoardPrompt;
            Debug.Log($"[Flow] Dogfight won at tier {EncounterTier}. +{dogfightReward.Gold} gold, +{dogfightReward.Xp} xp");
        }

        public void OnDogfightLost()
        {
            var reward = Encounter.Defeat(Content.economy);
            Encounter.Apply(Save, Content, reward);
            SaveGame();
            Debug.Log($"[Flow] Dogfight lost. +{reward.Gold} gold");
            ShowResults("OUTGUNNED", Palette.Danger, new List<string>
            {
                "Your crew limped back to port.",
                $"Salvaged {reward.Gold} gold",
            });
        }

        public void RetreatToPort()
        {
            Debug.Log("[Flow] Retreated to port");
            ShowPort();
        }

        public void Board()
        {
            Debug.Log("[Flow] Boarding");
            seed++;
            Go(GameState.Battle, root => root.gameObject.AddComponent<BoardingScreen>());
        }

        public void DeclineBoarding()
        {
            Debug.Log("[Flow] Declined boarding");
            ShowResults("PLUNDER", Palette.Accent, RewardLines(dogfightReward, null));
        }

        public void OnBattleFinished(bool victory)
        {
            if (victory)
            {
                var reward = Encounter.BoardingWin(Content.economy, EncounterTier);
                Encounter.Apply(Save, Content, reward, levelUps);
                SaveGame();
                Debug.Log($"[Flow] Boarding won. +{reward.Gold} gold, +{reward.Xp} xp");
                ShowResults("SHIP TAKEN", Palette.Accent, RewardLines(dogfightReward, reward));
            }
            else
            {
                Debug.Log("[Flow] Boarding lost");
                ShowResults("REPELLED", Palette.Danger, RewardLines(dogfightReward, null, "The enemy crew drove you off their deck."));
            }
        }

        public void FinishResults()
        {
            LoopsCompleted++;
            ShowPort();
        }

        List<string> RewardLines(EncounterReward dogfight, EncounterReward? boarding, string note = null)
        {
            var lines = new List<string>();
            if (note != null) lines.Add(note);
            lines.Add($"Dogfight: {dogfight.Gold} gold, {dogfight.Xp} XP");
            if (boarding.HasValue) lines.Add($"Boarding: {boarding.Value.Gold} gold, {boarding.Value.Xp} XP");
            foreach (var id in levelUps)
            {
                var member = Array.Find(Content.crew, c => c.id == id);
                var progress = Save.crew.Find(c => c.id == id);
                lines.Add($"{member.name} reached level {progress.level}!");
            }
            return lines;
        }

        void ShowResults(string title, Color color, List<string> lines)
        {
            var hadLevelUp = levelUps.Count > 0;
            Go(GameState.Results, root =>
            {
                var screen = root.gameObject.AddComponent<ResultsScreen>();
                screen.Setup(title, color, lines, hadLevelUp);
                return screen;
            });
        }

        void Go(GameState state, Func<Transform, MonoBehaviour> build)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Transition(state, build));
        }

        IEnumerator Transition(GameState state, Func<Transform, MonoBehaviour> build)
        {
            if (State != GameState.Booting) yield return ScreenFx.FadeOut();
            GameTime.Reset();
            Fx.Clear();
            if (CurrentScreen != null) Destroy(CurrentScreen.gameObject);
            var root = new GameObject(state + " Screen").transform;
            root.SetParent(screenRoot, false);
            State = state;
            CurrentScreen = build(root);
            Debug.Log($"[Flow] State {state}");
            yield return null;
            yield return ScreenFx.FadeIn();
            transition = null;
        }
    }

    // Slow idle orbit shared by the title, port and results backdrops.
    public class OrbitBackdrop : MonoBehaviour
    {
        ShipModel ship;
        Transform pivot;
        Transform look;
        float bobTime;

        public static OrbitBackdrop Create(Transform parent, WorldStage stage, Vector3 offset, Vector3 lookOffset, float fieldOfView)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);
            var backdrop = go.AddComponent<OrbitBackdrop>();
            backdrop.ship = Models.CreateShip("Gilded Gale", ShipHull.Sloop, KitArt.Crimson, go.transform);
            backdrop.pivot = new GameObject("Orbit Pivot").transform;
            backdrop.pivot.SetParent(go.transform, false);
            backdrop.pivot.localRotation = Quaternion.Euler(0f, -150f, 0f);
            backdrop.look = new GameObject("Look Target").transform;
            backdrop.look.SetParent(backdrop.pivot, false);
            backdrop.look.localPosition = lookOffset;
            stage.FrameTarget(backdrop.pivot, backdrop.look, offset, fieldOfView, 0f);
            return backdrop;
        }

        void Update()
        {
            pivot.Rotate(0f, 5f * Time.unscaledDeltaTime, 0f, Space.World);
            bobTime += Time.unscaledDeltaTime;
            ship.Body.localPosition = Vector3.up * (Mathf.Sin(bobTime * 0.9f) * 0.5f);
            ship.Body.localRotation = Quaternion.Euler(Mathf.Sin(bobTime * 0.7f) * 1.4f, 0f, Mathf.Sin(bobTime * 0.5f) * 2.4f);
            foreach (var propeller in ship.Propellers) propeller.Rotate(0f, 0f, 500f * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
