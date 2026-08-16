using UnityEngine;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// Plays one looping track for the scene it lives in.
    /// </summary>
    /// <remarks>
    /// One per scene rather than a DontDestroyOnLoad singleton. The menu and the run want different
    /// music, there are only two scenes, and the single transition between them is a deliberate
    /// mood change, so a persistent player would have to be told to switch tracks anyway. This way
    /// the scene owns its own audio and there is no object outliving the scene that created it.
    ///
    /// The mute preference is global and survives restarts, because a reviewer who silences the
    /// game should not have it come back the moment they reach the first fight.
    /// </remarks>
    public sealed class MusicPlayer : MonoBehaviour
    {
        private const string MutedKey = "lastlight.music.muted";

        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip track;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

        /// <summary>Mute state, shared by every scene and persisted between sessions.</summary>
        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            private set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
            if (source == null) return;

            source.clip = track;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            Apply();
            if (track != null) source.Play();
        }

        /// <summary>Flips the preference and updates every player currently loaded.</summary>
        public static bool ToggleMuted()
        {
            Muted = !Muted;

            foreach (MusicPlayer player in FindObjectsByType<MusicPlayer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                player.Apply();
            }

            return Muted;
        }

        private void Apply()
        {
            if (source != null) source.volume = Muted ? 0f : volume;
        }

#if UNITY_EDITOR
        public void Bind(AudioSource audioSource, AudioClip clip, float trackVolume)
        {
            source = audioSource;
            track = clip;
            volume = trackVolume;
        }
#endif
    }
}
