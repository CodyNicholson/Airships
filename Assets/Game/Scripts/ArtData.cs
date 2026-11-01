using System;

namespace Airships.Game
{
    // Effects and skies from the kit's manifest, reshaped into arrays that JsonUtility can read. The
    // Airships/Import Art Kit menu writes this to Resources/Art/art_data.json.
    [Serializable]
    public class FlipbookVariant
    {
        public string file;
        public int columns;
        public int rows;
        public int frames;
    }

    [Serializable]
    public class FlipbookDef
    {
        public string id;
        public float fps;
        public bool loop;
        public string blend; // premultiplied or additive.
        public float anchorX;
        public float anchorY;
        public FlipbookVariant hq;
        public FlipbookVariant lq;
    }

    [Serializable]
    public class EffectPresetDef
    {
        public string id;
        public string flipbook;
        public string[] sockets;
        public float sizeMeters;
        public float lifetime;
        public float rate;
        public int burst;
        public int maxHq;
        public int maxLq;
        public float velocityX;
        public float velocityY;
        public float velocityZ;
        public bool depthTest;
        public bool attached;
        public string followUp;
        public string fallbackMesh;
    }

    [Serializable]
    public class SkyDef
    {
        public string id;
        public string fileHq;
        public string fileLq;
        public float sunX;
        public float sunY;
        public float sunZ;
        public float sunR;
        public float sunG;
        public float sunB;
        public float sunIntensity;
        public float ambientR;
        public float ambientG;
        public float ambientB;
        public float fogR;
        public float fogG;
        public float fogB;
        public float fogStart;
        public float fogEnd;
    }

    [Serializable]
    public class ArtData
    {
        public FlipbookDef[] flipbooks;
        public EffectPresetDef[] presets;
        public SkyDef[] skies;

        public FlipbookDef Flipbook(string id) => Array.Find(flipbooks, f => f.id == id);
        public EffectPresetDef Preset(string id) => Array.Find(presets, p => p.id == id);
        public SkyDef Sky(string id) => Array.Find(skies, s => s.id == id);
    }
}
