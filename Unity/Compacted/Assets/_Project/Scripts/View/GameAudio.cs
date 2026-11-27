using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.View
{
    /// <summary>
    /// Ambience playlist and sound effects, driven by World events.
    /// Ambience: the tracks play one after another in a shuffled loop with a crossfade; volume halves while paused.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public GameController game;

        [Header("Ambience")]
        public AudioClip[] ambience;
        [Range(0, 1)] public float ambienceVolume = 0.5f;
        [Tooltip("Ambience volume multiplier while the game is paused.")]
        [Range(0, 1)] public float pausedMultiplier = 0.5f;
        public float crossfadeSeconds = 4f;

        [Header("Sound effects")]
        [Range(0, 1)] public float sfxVolume = 0.8f;
        [Tooltip("A tech node is completed.")] public AudioClip techComplete;
        [Tooltip("Items are taken from a machine by hand.")] public AudioClip manualPickup;
        [Tooltip("A block other than a belt or pipe is placed.")] public AudioClip placeBlock;
        public float placeBlockPitch = 0.5f;
        [Tooltip("The selected machine finishes one craft.")] public AudioClip craftDone;

        AudioSource[] ambienceSources;
        int activeSource;
        float pauseFactor = 1, fade = 1;
        readonly List<int> playOrder = new List<int>();
        int orderIndex = -1;

        AudioSource[] sfxPool;
        int nextSfx;

        World subscribed;

        void Awake()
        {
            ambienceSources = new[] { MakeSource("Ambience A"), MakeSource("Ambience B") };
            sfxPool = new AudioSource[8];
            for (int i = 0; i < sfxPool.Length; i++) sfxPool[i] = MakeSource("SFX " + i);
        }

        AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0;
            return s;
        }

        void Start()
        {
            game.Changed += Resubscribe;
            Resubscribe();
            if (ambience != null && ambience.Length > 0) PlayNextTrack(instant: true);
        }

        void OnDestroy()
        {
            if (game != null) game.Changed -= Resubscribe;
            Unsubscribe();
        }

        // ---------- world events ----------
        /// <summary>The World is replaced on load and new game; follow it.</summary>
        void Resubscribe()
        {
            if (subscribed == game.World) return;
            Unsubscribe();
            subscribed = game.World;
            subscribed.TechCompleted += OnTechCompleted;
            subscribed.ItemsTakenByHand += OnItemsTaken;
            subscribed.BlockPlaced += OnBlockPlaced;
            subscribed.CraftCompleted += OnCraftCompleted;
        }

        void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.TechCompleted -= OnTechCompleted;
            subscribed.ItemsTakenByHand -= OnItemsTaken;
            subscribed.BlockPlaced -= OnBlockPlaced;
            subscribed.CraftCompleted -= OnCraftCompleted;
            subscribed = null;
        }

        void OnTechCompleted(string id) => Play(techComplete);
        void OnItemsTaken(Entity e, int count) => Play(manualPickup);
        void OnBlockPlaced(Entity e) { if (!e.Def.Free) Play(placeBlock, placeBlockPitch); }   // not belts or pipes
        void OnCraftCompleted(Entity e) { if (e.Id == game.SelectedEntity) Play(craftDone); }

        public void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null) return;
            var s = sfxPool[nextSfx];
            nextSfx = (nextSfx + 1) % sfxPool.Length;
            s.Stop();
            s.clip = clip;
            s.pitch = pitch;
            s.volume = sfxVolume;
            s.Play();
        }

        // ---------- ambience ----------
        void Update()
        {
            if (ambienceSources == null || ambience == null || ambience.Length == 0) return;
            // Ease toward half volume while paused, back to full when resumed.
            float target = game.Paused ? pausedMultiplier : 1f;
            pauseFactor = Mathf.MoveTowards(pauseFactor, target, Time.unscaledDeltaTime * 2f);

            var current = ambienceSources[activeSource];
            var previous = ambienceSources[1 - activeSource];
            if (fade < 1) fade = Mathf.Min(1, fade + Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds));
            current.volume = ambienceVolume * pauseFactor * fade;
            previous.volume = ambienceVolume * pauseFactor * (1 - fade);
            if (fade >= 1 && previous.isPlaying) previous.Stop();

            // Start the next track so it fades in as the current one ends.
            if (current.clip != null && current.isPlaying && current.clip.length - current.time <= crossfadeSeconds) PlayNextTrack(instant: false);
            else if (!current.isPlaying) PlayNextTrack(instant: true);
        }

        void PlayNextTrack(bool instant)
        {
            if (++orderIndex >= playOrder.Count) Reshuffle();
            var clip = ambience[playOrder[orderIndex]];
            activeSource = 1 - activeSource;
            var s = ambienceSources[activeSource];
            s.clip = clip;
            s.loop = ambience.Length == 1;   // a single track simply loops
            s.time = 0;
            s.Play();
            fade = instant ? 1 : 0;
            if (instant) ambienceSources[1 - activeSource].Stop();
        }

        /// <summary>New random order of all tracks, never starting with the track that just played.</summary>
        void Reshuffle()
        {
            int last = playOrder.Count > 0 ? playOrder[playOrder.Count - 1] : -1;
            playOrder.Clear();
            for (int i = 0; i < ambience.Length; i++) playOrder.Add(i);
            for (int i = playOrder.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (playOrder[i], playOrder[j]) = (playOrder[j], playOrder[i]); }
            if (playOrder.Count > 1 && playOrder[0] == last) (playOrder[0], playOrder[1]) = (playOrder[1], playOrder[0]);
            orderIndex = 0;
        }
    }
}
