using System;
using System.IO;
using Escape.Data;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Builds Resources/ClipLibrary.asset from the Kenney audio packs under
    /// Assets/_Game/Audio. Sounds the packs lack (whistle, alert sting,
    /// camera hum, door clunk, whoosh) are synthesized to WAV files under
    /// Audio/Generated. Idempotent — safe to run on every Build All.
    /// </summary>
    public static class AudioLibraryBuilder
    {
        private const string AudioDir = "Assets/_Game/Audio";
        private const string ImpactDir = AudioDir + "/SFX/ImpactSounds/Audio";
        private const string IfaceDir = AudioDir + "/SFX/InterfaceSounds/Audio";
        private const string UiDir = AudioDir + "/UI/UIAudio/Audio";
        private const string GenDir = AudioDir + "/Generated";
        private const string LibPath = "Assets/_Game/Resources/ClipLibrary.asset";
        private const int Rate = 44100;

        [MenuItem("Tools/Escape the Elites/Build Audio Library")]
        public static void Build()
        {
            Blockout.EnsureFolder(GenDir);
            GenerateAll();

            var lib = AssetDatabase.LoadAssetAtPath<ClipLibrary>(LibPath);
            bool create = lib == null;
            if (create) lib = ScriptableObject.CreateInstance<ClipLibrary>();

            lib.stepsConcrete = FindSet("footstep_concrete");
            lib.stepsCarpet = FindSet("footstep_carpet");
            lib.stepsWood = FindSet("footstep_wood");
            lib.stepsMetal = FindSet("impactTin_medium");
            lib.glassImpacts = FindSet("impactGlass_medium");
            lib.metalImpacts = FindSet("impactMetal_medium");

            lib.whistle = Gen("whistle");
            lib.throwWhoosh = Gen("throw_whoosh");
            lib.doorClunk = Gen("door_clunk");
            lib.cameraHum = Gen("camera_hum");
            lib.alertSting = Gen("alert_sting");
            lib.pickup = Load($"{IfaceDir}/select_004.ogg");

            lib.uiClick = Load($"{UiDir}/click3.ogg");
            lib.uiConfirm = Load($"{IfaceDir}/confirmation_001.ogg");
            lib.uiError = Load($"{IfaceDir}/error_002.ogg");
            lib.uiBack = Load($"{IfaceDir}/back_001.ogg");
            lib.uiHover = Load($"{UiDir}/rollover1.ogg");

            if (create) AssetDatabase.CreateAsset(lib, LibPath);
            else EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioLibrary] Built {LibPath} — " +
                      $"steps:{Count(lib.stepsConcrete)}/{Count(lib.stepsCarpet)}/{Count(lib.stepsWood)}/{Count(lib.stepsMetal)} " +
                      $"impacts:{Count(lib.glassImpacts)}/{Count(lib.metalImpacts)}");
        }

        private static int Count(AudioClip[] set) => set?.Length ?? 0;

        private static AudioClip Load(string path) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        private static AudioClip Gen(string name) =>
            Load($"{GenDir}/{name}.wav");

        private static AudioClip[] FindSet(string prefix)
        {
            var guids = AssetDatabase.FindAssets($"t:AudioClip {prefix}", new[] { AudioDir });
            Array.Sort(guids, StringComparer.Ordinal);
            var clips = new AudioClip[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return clips;
        }

        // ---------- Synthesized clips ----------

        private static void GenerateAll()
        {
            WriteWav($"{GenDir}/whistle.wav", Whistle());
            WriteWav($"{GenDir}/alert_sting.wav", AlertSting());
            WriteWav($"{GenDir}/camera_hum.wav", CameraHum());
            WriteWav($"{GenDir}/door_clunk.wav", DoorClunk());
            WriteWav($"{GenDir}/throw_whoosh.wav", ThrowWhoosh());
            AssetDatabase.Refresh();
        }

        /// <summary>Two-tone whistle warble, ~0.6s.</summary>
        private static float[] Whistle()
        {
            var n = (int)(Rate * 0.62f);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                // two toots: silence 0.26–0.32
                float env = Envelope(t < 0.26f ? t / 0.26f : t < 0.32f ? 0f : (t - 0.32f) / 0.30f);
                float baseF = t < 0.32f ? 1900f : 2300f;
                float f = baseF + 90f * Mathf.Sin(2f * Mathf.PI * 28f * t);
                phase += 2f * Mathf.PI * f / Rate;
                s[i] = 0.55f * env * Mathf.Sin(phase);
            }
            return s;
        }

        /// <summary>Dissonant two-tone swell, 0.7s — security alert.</summary>
        private static float[] AlertSting()
        {
            var n = (int)(Rate * 0.7f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.7f - t) / 0.25f);
                float trem = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                s[i] = 0.5f * env * trem *
                       (Mathf.Sin(2f * Mathf.PI * 392f * t) + Mathf.Sin(2f * Mathf.PI * 415f * t)) * 0.5f;
            }
            return s;
        }

        /// <summary>50Hz mains hum + harmonic, exact integer cycles → seamless loop.</summary>
        private static float[] CameraHum()
        {
            int cycles = 20; // 0.4s at 50Hz
            int n = (int)(Rate * (cycles / 50f));
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                s[i] = 0.35f * Mathf.Sin(2f * Mathf.PI * 50f * t)
                     + 0.12f * Mathf.Sin(2f * Mathf.PI * 100f * t)
                     + 0.05f * Mathf.Sin(2f * Mathf.PI * 150f * t);
            }
            return s;
        }

        /// <summary>Low clunk + latch click, 0.45s — heavy sliding door.</summary>
        private static float[] DoorClunk()
        {
            var n = (int)(Rate * 0.45f);
            var s = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.12f * (noise - lp); // rough lowpass
                float body = Mathf.Sin(2f * Mathf.PI * 82f * t) * Mathf.Exp(-t * 18f);
                float clatter = lp * Mathf.Exp(-t * 22f) * 0.9f;
                float latch = t > 0.3f ? lp * Mathf.Exp(-(t - 0.3f) * 40f) * 0.7f : 0f;
                s[i] = body * 0.9f + clatter + latch;
            }
            return s;
        }

        /// <summary>Short airy whoosh, 0.28s — thrown lure.</summary>
        private static float[] ThrowWhoosh()
        {
            var n = (int)(Rate * 0.28f);
            var s = new float[n];
            var rng = new System.Random(11);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float k = Mathf.Lerp(0.02f, 0.25f, Mathf.Clamp01(t / 0.2f)); // opens up over time
                lp += k * (noise - lp);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.28f));
                s[i] = lp * env * 1.6f;
            }
            return s;
        }

        private static float Envelope(float t) =>
            t <= 0f ? 0f : Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / 0.15f);

        private static void WriteWav(string assetPath, float[] samples)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            using (var fs = new FileStream(full, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataLen = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataLen);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);            // PCM chunk size
                w.Write((short)1);      // PCM format
                w.Write((short)1);      // mono
                w.Write(Rate);
                w.Write(Rate * 2);      // byte rate
                w.Write((short)2);      // block align
                w.Write((short)16);     // bits
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataLen);
                foreach (var smp in samples)
                    w.Write((short)(Mathf.Clamp(smp, -1f, 1f) * 32760f));
            }
        }
    }
}
