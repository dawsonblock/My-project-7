using UnityEngine;

namespace Escape.Data
{
    /// <summary>
    /// Named clip collections for gameplay + UI audio. Built by
    /// AudioLibraryBuilder into Resources/ClipLibrary.asset; loaded lazily
    /// via Get(). All lookups tolerate a missing library so audio is an
    /// optional layer on top of the noise-simulation systems.
    /// </summary>
    public sealed class ClipLibrary : ScriptableObject
    {
        [Header("Footsteps")]
        public AudioClip[] stepsConcrete;
        public AudioClip[] stepsCarpet;
        public AudioClip[] stepsWood;
        public AudioClip[] stepsMetal;

        [Header("Impacts")]
        public AudioClip[] glassImpacts;
        public AudioClip[] metalImpacts;

        [Header("Player verbs")]
        public AudioClip whistle;
        public AudioClip throwWhoosh;
        public AudioClip pickup;

        [Header("World")]
        public AudioClip doorClunk;
        public AudioClip cameraHum;
        public AudioClip alertSting;

        [Header("UI")]
        public AudioClip uiClick;
        public AudioClip uiConfirm;
        public AudioClip uiError;
        public AudioClip uiBack;
        public AudioClip uiHover;

        private static ClipLibrary _cached;
        private static bool _looked;

        public static ClipLibrary Get()
        {
            if (!_looked)
            {
                _looked = true;
                _cached = Resources.Load<ClipLibrary>("ClipLibrary");
            }
            return _cached;
        }

        /// <summary>Random clip from a set, or null when the set is empty.</summary>
        public static AudioClip Pick(AudioClip[] set) =>
            set != null && set.Length > 0 ? set[Random.Range(0, set.Length)] : null;
    }
}
