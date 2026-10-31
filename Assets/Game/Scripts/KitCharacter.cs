using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Airships.Game
{
    // Plays the clips baked into a kit character with a small playable graph and no controller asset.
    // Clip time is driven here, so looping doesn't depend on how the importer flagged each clip.
    public class KitCharacter : MonoBehaviour
    {
        const float DefaultFade = 0.12f;

        [SerializeField] AnimationClip[] clips;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current;
        AnimationClipPlayable previous;
        float currentLength;
        float currentTime;
        bool currentLoops;
        float previousTime;
        float fade;
        float fadeLength;
        string loopName;

        public AnimationClip[] Clips { get => clips; set => clips = value; }

        void EnsureGraph()
        {
            if (graph.IsValid()) return;
            graph = PlayableGraph.Create(name + " Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var animator = GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            var output = AnimationPlayableOutput.Create(graph, "Output", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        // Starts a looping clip, which one-shots return to when they finish. The offset keeps a crowd
        // of characters from moving in lockstep.
        public void Loop(string clipName, float startOffset = 0f)
        {
            loopName = clipName;
            Begin(clipName, true, DefaultFade);
            currentTime = startOffset;
        }

        // Plays a clip once and holds its last frame. Returns its length in seconds.
        public float Play(string clipName, float fadeSeconds = DefaultFade)
        {
            CancelInvoke(nameof(ReturnToLoop));
            return Begin(clipName, false, fadeSeconds);
        }

        // Plays a clip once, then blends back to the current loop. Returns its length in seconds.
        public float PlayThenReturn(string clipName)
        {
            var length = Play(clipName);
            Invoke(nameof(ReturnToLoop), Mathf.Max(0.05f, length - DefaultFade));
            return length;
        }

        void ReturnToLoop()
        {
            if (loopName != null) Begin(loopName, true, DefaultFade);
        }

        float Begin(string clipName, bool loops, float fadeSeconds)
        {
            EnsureGraph();
            var clip = System.Array.Find(clips, c => c.name == clipName);
            if (clip == null) throw new System.ArgumentException($"{name} has no clip '{clipName}'");

            if (previous.IsValid()) graph.DestroyPlayable(previous);
            previous = current;
            previousTime = currentTime;
            mixer.DisconnectInput(0);
            mixer.DisconnectInput(1);

            current = AnimationClipPlayable.Create(graph, clip);
            current.Pause();
            current.SetTime(0);
            currentLength = clip.length;
            currentTime = 0f;
            currentLoops = loops;
            mixer.ConnectInput(0, current, 0);
            if (previous.IsValid()) mixer.ConnectInput(1, previous, 0);
            fadeLength = previous.IsValid() ? fadeSeconds : 0f;
            fade = 0f;
            ApplyWeights();
            return clip.length;
        }

        void Update()
        {
            if (!current.IsValid()) return;
            currentTime += Time.deltaTime;
            var time = currentLoops ? Mathf.Repeat(currentTime, currentLength) : Mathf.Min(currentTime, currentLength - 0.001f);
            current.SetTime(time);
            if (previous.IsValid()) previous.SetTime(previousTime);

            if (fadeLength > 0f && fade < 1f)
            {
                fade += Time.deltaTime / fadeLength;
                ApplyWeights();
            }
        }

        void ApplyWeights()
        {
            var weight = fadeLength <= 0f ? 1f : Mathf.Clamp01(fade);
            mixer.SetInputWeight(0, weight);
            mixer.SetInputWeight(1, previous.IsValid() ? 1f - weight : 0f);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
