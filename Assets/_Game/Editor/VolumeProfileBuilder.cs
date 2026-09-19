using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Escape.EditorTools
{
    /// <summary>
    /// Authors the post-processing stack from code so the look is reviewable
    /// and reproducible rather than hand-tuned in the Inspector.
    ///
    /// URP stacks two profiles: the Global Settings "Default Volume Profile"
    /// and the render-pipeline asset's own profile, the latter winning. The
    /// project shipped the URP template in both — a sample profile overriding
    /// the global one, plus dead test components. This writes the intended
    /// look into the global profile and empties the per-asset one, so there is
    /// exactly one source of truth.
    /// </summary>
    public static class VolumeProfileBuilder
    {
        private const string GlobalProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";
        private const string QualityProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        [MenuItem("Tools/Escape the Elites/Author Volume Profile")]
        public static void Author()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlobalProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"[VolumeProfileBuilder] No profile at {GlobalProfilePath}");
                return;
            }

            StripJunk(profile);

            var tone = Ensure<Tonemapping>(profile);
            tone.active = true;
            Override(tone.mode, TonemappingMode.Neutral);

            // Lamps, monitors and the relay glow read as light sources only if
            // bloom actually fires — the template shipped intensity 0.
            var bloom = Ensure<Bloom>(profile);
            bloom.active = true;
            Override(bloom.threshold, 0.85f);
            Override(bloom.intensity, 0.5f);
            Override(bloom.scatter, 0.6f);
            Override(bloom.highQualityFiltering, true);

            var vignette = Ensure<Vignette>(profile);
            vignette.active = true;
            Override(vignette.intensity, 0.3f);
            Override(vignette.smoothness, 0.45f);

            var color = Ensure<ColorAdjustments>(profile);
            color.active = true;
            Override(color.postExposure, 0.2f);   // lift the very dark interiors
            Override(color.contrast, 10f);
            Override(color.saturation, -8f);      // rain-soaked, drained palette

            var grain = Ensure<FilmGrain>(profile);
            grain.active = true;
            Override(grain.type, FilmGrainLookup.Medium1);
            Override(grain.intensity, 0.2f);
            Override(grain.response, 0.85f);

            var white = Ensure<WhiteBalance>(profile);
            white.active = true;
            Override(white.temperature, -6f);

            Disable<DepthOfField>(profile);      // clarity beats bokeh in a stealth game
            Disable<MotionBlur>(profile);
            Disable<LensDistortion>(profile);
            Disable<ChromaticAberration>(profile);
            Disable<ColorLookup>(profile);
            Disable<ColorCurves>(profile);
            Disable<SplitToning>(profile);
            Disable<LiftGammaGain>(profile);
            Disable<ShadowsMidtonesHighlights>(profile);
            Disable<ChannelMixer>(profile);
            Disable<ScreenSpaceLensFlare>(profile);

            // NOTE: a player build also re-adds PaniniProjection and
            // ProbeVolumesOptions to this asset as documents Unity does not
            // load, so they cannot be removed or disabled from here. They are
            // inert (panini distance 0, no probe volumes) — revert them by hand
            // if you don't mean to commit them. See AGENTS.md.

            // The per-asset profile is stacked above the global one, so its
            // template values (bloom 0.25, tonemapping None) would win over
            // everything above. Empty it and keep one source of truth.
            var quality = AssetDatabase.LoadAssetAtPath<VolumeProfile>(QualityProfilePath);
            if (quality != null && quality.components.Count > 0) quality.components.Clear();

            EditorUtility.SetDirty(profile);
            if (quality != null) EditorUtility.SetDirty(quality);
            AssetDatabase.SaveAssets();

            // Emptying the list leaves the old component documents orphaned in
            // the asset file; a forced reimport drops what nothing references.
            if (quality != null)
                AssetDatabase.ImportAsset(QualityProfilePath, ImportAssetOptions.ForceUpdate);

            Debug.Log("[VolumeProfileBuilder] Volume profile authored.");
        }

        private static void Override<T>(VolumeParameter<T> param, T value)
        {
            param.overrideState = true;
            param.value = value;
        }

        private static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing)) return existing;
            return profile.Add<T>(true);
        }

        private static void Disable<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) component.active = false;
        }

        /// <summary>
        /// Drops components whose script failed to resolve. Unity already
        /// omits unresolvable entries from the loaded list, so this is a
        /// guard rather than the main cleanup — the template's dead test
        /// entries stay in the YAML as inert data and cost nothing at runtime.
        /// </summary>
        private static void StripJunk(VolumeProfile profile)
        {
            for (int i = profile.components.Count - 1; i >= 0; i--)
                if (profile.components[i] == null) profile.components.RemoveAt(i);
        }
    }
}
