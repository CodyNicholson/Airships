using UnityEngine;

namespace Airships.Game
{
    // The art bible's single color source. Style: sunlit sky-pirate adventure, with chunky faceted
    // low-poly forms, soft two-tone shading, and painted wood, brass and canvas under an endless
    // azure sky. Mood: high golden sun, with cool blue bounce keeping every shadow side alive.
    public static class Palette
    {
        public static readonly Color SkyTop = Hex(0x1F6FCF);
        public static readonly Color SkyHorizon = Hex(0xBDE8F7);
        public static readonly Color SkyBelow = Hex(0x8FB6E0);
        public static readonly Color Sun = Hex(0xFFE9B8);
        public static readonly Color SunLight = Hex(0xFFF0D2);
        public static readonly Color AmbientSky = Hex(0x6E8FC4);
        public static readonly Color AmbientGround = Hex(0x4A4468);
        public static readonly Color Cloud = Hex(0xFFF8EA);
        public static readonly Color Fog = Hex(0xC5E4F4);

        public static readonly Color HullWood = Hex(0x8A4F2A);
        public static readonly Color DarkWood = Hex(0x5A3320);
        public static readonly Color DeckWood = Hex(0xC9945C);
        public static readonly Color Canvas = Hex(0xF4E4C1);
        public static readonly Color Brass = Hex(0xE8B23A);
        public static readonly Color Iron = Hex(0x3B4258);

        public static readonly Color PlayerPaint = Hex(0xC8342B);
        public static readonly Color EnemyPaint = Hex(0x54387E);
        public static readonly Color EnemySail = Hex(0x3C2C57);

        public static readonly Color IslandRock = Hex(0x8C6F5A);
        public static readonly Color IslandGrass = Hex(0x6FB04A);

        public static readonly Color Skin = Hex(0xF0C09A);
        public static readonly Color Coat = Hex(0x274A8F);
        public static readonly Color Leather = Hex(0x6E4326);
        public static readonly Color Storm = Hex(0x5BC6E8);
        public static readonly Color WhaleDot = Hex(0x2E8BFF);
        public static readonly Color Medic = Hex(0xF2F0E2);
        public static readonly Color Herb = Hex(0x4CA36A);

        public static readonly Color Shadow = Hex(0x33466F);
        public static readonly Color Ink = Hex(0x141D38);
        public static readonly Color Panel = Hex(0x1F2E57);
        public static readonly Color PanelLight = Hex(0x304A82);
        public static readonly Color TextLight = Hex(0xF8EFD9);
        public static readonly Color TextMuted = Hex(0xA9B8D8);
        public static readonly Color Accent = Hex(0xF2B134);
        public static readonly Color Danger = Hex(0xE5533D);
        public static readonly Color Heal = Hex(0x63D68C);
        public static readonly Color Mana = Hex(0x57A6F0);
        public static readonly Color Spark = Hex(0xFFC857);
        public static readonly Color Smoke = Hex(0xD8D5D0);
        public static readonly Color DarkSmoke = Hex(0x5D6585);

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        static Color Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }
    }
}
