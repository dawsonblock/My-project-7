using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Minimal spatial/2D audio facade. Mixer snapshots can be layered on
    /// later without changing call sites.
    /// </summary>
    public interface IAudioService
    {
        void Play2D(AudioClip clip, float volume = 1f);
        void Play3D(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 25f);
        void SetSnapshot(string snapshotName);
    }

    public sealed class AudioService : IAudioService
    {
        private readonly Transform _root;
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private AudioSource _source2D;

        public AudioService(Transform root) => _root = root;

        private AudioSource Get2D()
        {
            if (_source2D == null)
            {
                var go = new GameObject("Audio2D");
                go.transform.SetParent(_root, false);
                _source2D = go.AddComponent<AudioSource>();
                _source2D.spatialBlend = 0f;
            }
            return _source2D;
        }

        public void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            Get2D().PlayOneShot(clip, volume);
        }

        public void Play3D(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 25f)
        {
            if (clip == null) return;
            AudioSource src = null;
            foreach (var s in _pool)
                if (!s.isPlaying) { src = s; break; }
            if (src == null)
            {
                var go = new GameObject("Audio3D_" + _pool.Count);
                go.transform.SetParent(_root, false);
                src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                _pool.Add(src);
            }
            src.transform.position = position;
            src.maxDistance = maxDistance;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }

        public void SetSnapshot(string snapshotName)
        {
            // Mixer snapshots land with the audio pass; interface is stable now.
        }
    }
}
