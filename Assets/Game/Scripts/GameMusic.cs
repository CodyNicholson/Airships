using Airships.Core;

namespace Airships.Game
{
    // Which song plays where. A phase's own music, set in the campaign content, always wins.
    public static class GameMusic
    {
        public const string Adventure = "skyward_regatta";
        public const string Harbor = "harbor_of_lanterns";
        public const string SkyBattle = "broadsides_at_dawn";
        public const string DeckBattle = "cannons_over_cumulus";
        public const string Boss = "leviathan_of_the_upper_winds";
        public const string Finale = "the_sovereign_descends";
        public const string Victory = "victory_fanfare";

        public static readonly string[] Songs = { Adventure, Harbor, SkyBattle, DeckBattle, Boss, Finale };

        const float VictoryGapSeconds = 45f;

        // Null means whatever is playing carries on.
        public static string For(GameState state, PhaseDef phase)
        {
            if (phase != null && !string.IsNullOrEmpty(phase.music)) return phase.music;
            switch (state)
            {
                case GameState.Title:
                case GameState.Story:
                case GameState.Choice:
                case GameState.Assign:
                    return Adventure;
                case GameState.Hub:
                case GameState.Port:
                    return Harbor;
                case GameState.Dogfight:
                    return SkyBattle;
                case GameState.Battle:
                    return DeckBattle;
                default:
                    return null;
            }
        }

        // A boss's Choice and the end of its mission can be seconds apart, and one fanfare is enough.
        public static void PlayVictory()
        {
            if (AudioBank.SecondsSinceJingle > VictoryGapSeconds) AudioBank.PlayJingle(Victory);
        }
    }
}
