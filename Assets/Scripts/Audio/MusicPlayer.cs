using UnityEngine;

namespace KartRush
{
    /// <summary>Plays the looping background track; the pause menu can toggle it.</summary>
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }
        public float volume = 0.22f;
        AudioSource src;

        public bool MusicOn { get => !src.mute; set => src.mute = !value; }

        void Awake() => Instance = this;

        void Start()
        {
            Sfx.Build();
            src = gameObject.AddComponent<AudioSource>();
            src.clip = Sfx.Music;
            src.loop = true;
            src.volume = volume;
            src.spatialBlend = 0f;
            src.Play();
        }
    }
}
