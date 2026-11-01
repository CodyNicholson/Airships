using System;
using System.Collections;
using System.Collections.Generic;
using Airships.Core;
using UnityEngine;

namespace Airships.Game
{
    public enum GameState { Booting, Title, Hub, Port, Dogfight, BoardPrompt, Battle, Results, Story, Choice, Assign }

    // Everything a boarding battle needs, whichever mode set it up.
    public class DeckSetup
    {
        public List<Combatant> Party;
        public List<Combatant> Enemies;
        public Combatant Spare;
        public int SurrenderAfterRound;
        public string Location;
        public bool Below;
        public string Faction = KitArt.Obsidian;
        public string Arena;
        public bool Boarded; // The party swings in on grapple lines.
        public bool Storm;
        public CommsLine[] Lines;
    }

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

        public SkyConfig Sky { get; private set; }
        public DeckSetup Deck { get; private set; }
        public CampaignRun Run { get; private set; }

        // Automated campaign runs can also play the side jobs as they appear.
        public bool AutoplaySideMissions { get; set; } = true;

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
            Content.campaign = JsonUtility.FromJson<CampaignDef>(Resources.Load<TextAsset>("campaign_content").text);
            Save = SaveStore.Load(Content);

            GameTime.Reset();
            Stage = WorldStage.Create();
            Fx.Create(transform);
            Effects.Create(this);
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

        public void ShowTitle() => Go(GameState.Title, root =>
        {
            Stage.ApplyWeather(Content.Weather("clear"));
            return root.gameObject.AddComponent<TitleScreen>();
        });

        public void ShowHub()
        {
            Run = null;
            Go(GameState.Hub, root =>
            {
                Stage.ApplyWeather(Content.Weather("hub"));
                return root.gameObject.AddComponent<QuarterdeckScreen>();
            });
        }

        public void ShowPort() => Go(GameState.Port, root =>
        {
            Stage.ApplyWeather(Content.Weather("clear"));
            return root.gameObject.AddComponent<PortScreen>();
        });

        // Leaving the title goes straight into the story on a new game, and to the deck afterward.
        public void LeaveTitle()
        {
            if (Campaign.PrologueDone(Save, Content))
            {
                ShowHub();
                return;
            }
            var first = Content.campaign.missions[0];
            PlayMission(first, Save.campaign.activeMission == first.id ? Save.campaign.activePhase : 0);
        }

        // Which ship backdrops show: the active one, or the Gale before the player owns anything.
        public string DisplayShip()
        {
            if (Run != null && !string.IsNullOrEmpty(Run.LastShipFlown)) return Run.LastShipFlown;
            return string.IsNullOrEmpty(Save.activeShip) ? "sovereign_gale" : Save.activeShip;
        }

        public void PlayMission(MissionDef mission, int phase)
        {
            Debug.Log($"[Flow] Mission {mission.id} from phase {phase}");
            Run = new CampaignRun(Content, Save, mission, phase);
            EncounterTier = mission.act;
            levelUps.Clear();
            SaveGame();
            RunPhase();
        }

        // Moves past the current phase, saving a checkpoint.
        public void AdvancePhase()
        {
            Run.Advance();
            SaveGame();
            RunPhase();
        }

        void RunPhase()
        {
            if (Run.Finished)
            {
                FinishMission();
                return;
            }
            var phase = Run.Phase;
            Debug.Log($"[Flow] Phase {Run.PhaseIndex} of {Run.Mission.id}: {phase.kind}");
            seed++;
            switch (phase.kind)
            {
                case "story":
                    Go(GameState.Story, root =>
                    {
                        var screen = root.gameObject.AddComponent<StoryScreen>();
                        screen.Setup(phase);
                        return screen;
                    });
                    break;
                case "sky":
                    Sky = Run.BuildSky(phase, seed);
                    Go(GameState.Dogfight, root => root.gameObject.AddComponent<DogfightScreen>());
                    break;
                case "deck":
                    if (Run.ShouldSkipDeck(phase))
                    {
                        AdvancePhase();
                        return;
                    }
                    Deck = BuildCampaignDeck(phase);
                    Go(GameState.Battle, root => root.gameObject.AddComponent<BoardingScreen>());
                    break;
                case "assign":
                    Go(GameState.Assign, root =>
                    {
                        var screen = root.gameObject.AddComponent<AssignScreen>();
                        screen.Setup(phase);
                        return screen;
                    });
                    break;
                case "choice":
                    Go(GameState.Choice, root =>
                    {
                        var screen = root.gameObject.AddComponent<ChoiceScreen>();
                        screen.Setup(phase);
                        return screen;
                    });
                    break;
                default:
                    throw new InvalidOperationException($"Unknown phase kind '{phase.kind}'");
            }
        }

        DeckSetup BuildCampaignDeck(PhaseDef phase)
        {
            var party = Run.BuildDeckParty(phase);
            var enemies = Run.BuildDeckEnemies(phase);
            var defending = phase.party == "defending";
            var shipId = string.IsNullOrEmpty(Run.LastShipFlown) ? Save.activeShip : Run.LastShipFlown;
            return new DeckSetup
            {
                Party = party,
                Enemies = enemies,
                Spare = Run.SpareIn(phase, enemies),
                SurrenderAfterRound = Run.SabotageRounds(party, phase),
                Location = phase.location,
                Below = phase.below,
                Faction = !string.IsNullOrEmpty(phase.arenaFaction) ? phase.arenaFaction : !string.IsNullOrEmpty(shipId) ? Content.Ship(shipId).faction : KitArt.Obsidian,
                Arena = phase.arena,
                Boarded = !defending && !phase.below && phase.party != "solo",
                Storm = phase.arena == "SM_Env_DreadnoughtDeck",
                Lines = phase.lines,
            };
        }

        public void OnSkyFinished(SkyEncounter encounter)
        {
            var survived = Run.FinishSky(encounter);
            if (!survived)
            {
                SaveGame();
                ShowFailure(encounter.Outcome == SkyOutcome.Eaten ? "SWALLOWED" : "SHOT DOWN", new List<string> { "Your crew hauled you out of the wreck.", "The sky's still waiting." });
                return;
            }

            // A successful boarding leaves your own ship exposed until the party gets back.
            if (encounter.Outcome == SkyOutcome.Boarded && string.IsNullOrEmpty(Run.Phase.ship)) Campaign.ApplyBoardingExposure(Content, Save, Save.activeShip);
            AdvancePhase();
        }

        void FinishMission()
        {
            var mission = Run.Mission;
            var crewBefore = Save.crew.Count;
            var goldBefore = Save.gold;
            var shipBefore = Save.activeShip;
            Run.CompleteMission();
            if (mission.kind == "bounty") Campaign.ClearNemesis(Save, mission.id.Replace("bounty_", ""));
            SaveGame();
            Debug.Log($"[Flow] Mission {mission.id} complete. Gold {Save.gold}, crew {Save.crew.Count}");

            var lines = new List<string> { Run.TargetLost ? $"Scrap only: {Save.gold - goldBefore} gold" : $"{Save.gold - goldBefore} gold", $"{mission.xp} XP to every crew member" };
            for (var i = crewBefore; i < Save.crew.Count; i++) lines.Add($"{Content.Crew(Save.crew[i].id).name} joined the crew!");
            if (Save.activeShip != shipBefore && !string.IsNullOrEmpty(Save.activeShip)) lines.Add($"You stole {Content.Ship(Save.activeShip).name}!");
            foreach (var id in Run.LevelUps)
            {
                var progress = Save.crew.Find(c => c.id == id);
                if (progress != null) lines.Add($"{Content.Crew(id).name} reached level {progress.level}!");
            }
            var hadLevelUp = Run.LevelUps.Count > 0;
            var title = mission.id == Content.campaign.missions[Array.FindLastIndex(Content.campaign.missions, m => m.IsStory)].id ? "THE SKY IS YOURS" : "MISSION COMPLETE";
            GameMusic.PlayVictory();
            AudioBank.PlayMusic(GameMusic.Harbor);
            Go(GameState.Results, root =>
            {
                var screen = root.gameObject.AddComponent<ResultsScreen>();
                screen.Setup(title, Palette.Accent, lines, hadLevelUp);
                return screen;
            });
        }

        void ShowFailure(string title, List<string> lines)
        {
            AudioBank.PlayMusic(null);
            Go(GameState.Results, root =>
            {
                var screen = root.gameObject.AddComponent<ResultsScreen>();
                screen.Setup(title, Palette.Danger, lines, false);
                screen.SetupRetry(() => RunPhase(), ShowHub);
                return screen;
            });
        }

        public void StartDogfight()
        {
            EncounterTier = Encounter.Tier(Save);
            levelUps.Clear();
            seed++;
            Run = null;
            Deck = null;
            var shipId = Save.activeShip;
            var loadout = Encounter.PlayerLoadout(Content, Save, shipId);
            Sky = new SkyConfig
            {
                Content = Content,
                Seed = seed,
                Tier = EncounterTier,
                Player = loadout,
                PlayerShipName = Content.Ship(shipId).name,
                PlayerHull = loadout.Hull,
                AllControls = true,
                Weather = "clear",
                Patches = Campaign.HasMechanic(Save, "patch") ? 1 : 0,
            };
            foreach (var mechanic in Save.campaign.mechanics) Sky.Mechanics.Add(mechanic);
            Sky.Enemies.Add(new SkySpawn
            {
                shipId = EnemyShipId,
                name = $"Enemy Raider  (Rank {EncounterTier + 1})",
                accuracy = Mathf.Max(4f, 14f - EncounterTier * 2f),
                target = true,
            });
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

        // The whale ends the encounter at once, with no salvage. The ship itself isn't lost.
        public void OnEatenByWhale()
        {
            SaveGame();
            Debug.Log("[Flow] Eaten by the sky whale");
            ShowResults("SWALLOWED", Palette.Danger, new List<string>
            {
                "The sky whale ate your ship.",
                "Your crew limped back to port.",
                "No salvage this time.",
            });
        }

        public void RetreatToPort()
        {
            Debug.Log("[Flow] Retreated to port");
            if (Run != null) ShowHub();
            else ShowPort();
        }

        public void Board()
        {
            Debug.Log("[Flow] Boarding");
            seed++;
            Deck = new DeckSetup
            {
                Party = BattleSetup.BuildParty(Content, Save),
                Enemies = BattleSetup.BuildEnemies(Content, EncounterTier),
                Boarded = true,
            };
            Go(GameState.Battle, root => root.gameObject.AddComponent<BoardingScreen>());
        }

        public void DeclineBoarding()
        {
            Debug.Log("[Flow] Declined boarding");
            ShowResults("PLUNDER", Palette.Accent, RewardLines(dogfightReward, null));
        }

        public void OnBattleFinished(bool victory, PartyBattle battle)
        {
            if (Run != null)
            {
                if (victory)
                {
                    Run.FinishDeck(Run.Phase, battle.Party);
                    AdvancePhase();
                }
                else
                {
                    ShowFailure(battle.SpareKilled ? "TOO FAR" : "REPELLED", new List<string> { battle.SpareKilled ? "You wanted that blade alive." : "They drove you back to your ship.", "Catch your breath and go again." });
                }
                return;
            }

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
            if (Run != null) ShowHub();
            else ShowPort();
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
            if (color == Palette.Accent)
            {
                GameMusic.PlayVictory();
                AudioBank.PlayMusic(GameMusic.Harbor);
            }
            else
            {
                AudioBank.PlayMusic(null);
            }
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
            Effects.Clear();
            if (CurrentScreen != null) Destroy(CurrentScreen.gameObject);
            var root = new GameObject(state + " Screen").transform;
            root.SetParent(screenRoot, false);
            State = state;
            var inPhase = Run != null && (state == GameState.Story || state == GameState.Dogfight || state == GameState.Battle || state == GameState.Choice || state == GameState.Assign);
            var song = GameMusic.For(state, inPhase ? Run.Phase : null);
            if (song != null) AudioBank.PlayMusic(song);
            AudioBank.SetDanger(0f);
            if (state == GameState.Choice) GameMusic.PlayVictory();
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

        public static OrbitBackdrop Create(Transform parent, WorldStage stage, Vector3 offset, Vector3 lookOffset, float fieldOfView, string shipId = null)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);
            var backdrop = go.AddComponent<OrbitBackdrop>();
            shipId = shipId ?? GameDirector.Instance.DisplayShip();
            backdrop.ship = Models.CreateShip("Backdrop Ship", GameDirector.Instance.Content.Ship(shipId), go.transform);
            var size = 0f;
            foreach (var renderer in backdrop.ship.Kit.GetComponentsInChildren<Renderer>()) size = Mathf.Max(size, Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z));
            offset *= Mathf.Clamp(size / 34f, 0.85f, 1.5f);
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
