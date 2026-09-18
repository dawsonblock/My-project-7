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
            WriteWav($"{GenDir}/amb_dock.wav", AmbDock(), AmbRate);
            WriteWav($"{GenDir}/amb_service.wav", AmbService(), AmbRate);
            WriteWav($"{GenDir}/amb_office.wav", AmbOffice(), AmbRate);
            WriteWav($"{GenDir}/amb_security.wav", AmbSecurity(), AmbRate);
            WriteWav($"{GenDir}/amb_bunker.wav", AmbBunker(), AmbRate);
            WriteWav($"{GenDir}/amb_tower.wav", AmbTower(), AmbRate);
            WriteWav($"{GenDir}/drip_loop.wav", DripLoop(), AmbRate);
            WriteWav($"{GenDir}/fan_loop.wav", FanLoop(), AmbRate);
            AssetDatabase.Refresh();
        }

        private const int AmbRate = 22050;

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

        /// <summary>Wind wash + water lapping the pier, 8s — dock ambience.</summary>
        private static float[] AmbDock()
        {
            const float dur = 8f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            var rng = new System.Random(23);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.035f * (noise - lp);
                lp2 += 0.005f * (lp - lp2);
                // All periodic terms complete integer cycles in `dur` → seamless.
                float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * (2f / dur) * t);
                float lap = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (5f / dur) * t + 1.7f), 3f);
                raw[i] = lp2 * swell * 1.9f + lp * lap * 1.1f;
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>HVAC hum + vent rumble + two pipe drips, 4s — service corridor.</summary>
        private static float[] AmbService()
        {
            const float dur = 4f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            var rng = new System.Random(31);
            float lp = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.02f * (noise - lp);
                float hum = 0.22f * Mathf.Sin(2f * Mathf.PI * 60f * t)
                          + 0.09f * Mathf.Sin(2f * Mathf.PI * 120f * t)
                          + 0.04f * Mathf.Sin(2f * Mathf.PI * 180f * t);
                float drip = DripPing(t - 1.3f, 2100f) + DripPing(t - 3.4f, 1700f);
                raw[i] = hum + lp * 0.5f + drip;
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>Muffled rain on glass + ticking clock, 6s — office ambience.</summary>
        private static float[] AmbOffice()
        {
            const float dur = 6f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            var rng = new System.Random(47);
            float lp = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.07f * (noise - lp);
                float gust = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * (3f / dur) * t + 0.9f);
                float tick = 0f;
                for (int k = 0; k < 6; k++)
                {
                    float tt = t - (0.5f + k);
                    if (tt >= 0f && tt < 0.05f)
                        tick += Mathf.Sin(2f * Mathf.PI * (k % 2 == 0 ? 1900f : 1500f) * tt)
                                * Mathf.Exp(-tt * 90f) * 0.35f;
                }
                raw[i] = lp * gust * 0.8f + tick;
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>Monitor whine + equipment hum + walkie squelch blips, 6s — security hub.</summary>
        private static float[] AmbSecurity()
        {
            const float dur = 6f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            var rng = new System.Random(59);
            float lp = 0f;
            float squelchPhase = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.09f * (noise - lp);
                float hum = 0.14f * Mathf.Sin(2f * Mathf.PI * 60f * t)
                          + 0.06f * Mathf.Sin(2f * Mathf.PI * 180f * t);
                // faint CRT whine — amplitude-wobbled so it breathes
                float whine = 0.02f * Mathf.Sin(2f * Mathf.PI * 3900f * t)
                            * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (2f / dur) * t));
                // two walkie squelch blips at integer-safe positions
                float blip = 0f;
                foreach (var tb in new[] { 1.8f, 4.6f })
                {
                    float td = t - tb;
                    if (td >= 0f && td < 0.12f)
                    {
                        squelchPhase += 2f * Mathf.PI * 1400f / AmbRate;
                        blip += lp * Mathf.Exp(-td * 30f) * 4f;
                    }
                }
                raw[i] = hum + whine + lp * 0.35f + blip;
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>Server fan drone + drive clicks + deep electrical hum, 6s — server vault.</summary>
        private static float[] AmbBunker()
        {
            const float dur = 6f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            var rng = new System.Random(67);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.15f * (noise - lp);   // fan rush — brighter than wind
                lp2 += 0.008f * (noise - lp2); // deep rumble
                float drone = 0.2f * Mathf.Sin(2f * Mathf.PI * 120f * t)
                            + 0.08f * Mathf.Sin(2f * Mathf.PI * 240f * t)
                            + 0.03f * Mathf.Sin(2f * Mathf.PI * 480f * t);
                // sparse disk clicks
                float click = 0f;
                foreach (var tb in new[] { 0.9f, 2.3f, 3.1f, 4.9f })
                {
                    float td = t - tb;
                    if (td >= 0f && td < 0.015f)
                        click += noise * Mathf.Exp(-td * 400f) * 0.3f;
                }
                raw[i] = drone + lp * 0.5f + lp2 * 1.2f + click;
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>Hard rooftop wind + antenna wire-song + distant metal groan, 8s — tower.</summary>
        private static float[] AmbTower()
        {
            const float dur = 8f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            var rng = new System.Random(83);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.05f * (noise - lp);
                lp2 += 0.004f * (noise - lp2);
                float gust = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (3f / dur) * t + 0.4f);
                // wire-song: two detuned high sines, swelling with the gusts
                float song = (Mathf.Sin(2f * Mathf.PI * 860f * t) + Mathf.Sin(2f * Mathf.PI * 867f * t))
                             * 0.03f * gust;
                // low metal groan once per loop, decaying before wrap
                float td = t - 3.2f;
                float groan = td >= 0f && td < 1.6f
                    ? Mathf.Sin(2f * Mathf.PI * 55f * td + Mathf.Sin(td * 7f)) * Mathf.Exp(-td * 3f) * 0.25f
                    : 0f;
                raw[i] = lp * gust * 1.1f + lp2 * gust * 1.4f + song + groan;
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>Steady machine fan — rush + blade chug, 2s — localized 3D loop.</summary>
        private static float[] FanLoop()
        {
            const float dur = 2f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            var rng = new System.Random(97);
            float lp = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.18f * (noise - lp);
                // blade pass at 8Hz — integer cycles per loop → seamless
                float chug = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 8f * t);
                raw[i] = lp * chug * 1.1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 120f * t);
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>Two water drips, 3s — localized 3D loop for puddles.</summary>
        private static float[] DripLoop()
        {
            const float dur = 3f;
            int n = (int)(AmbRate * dur), fade = AmbRate / 8;
            var raw = new float[n + fade];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / AmbRate;
                raw[i] = DripPing(t - 0.4f, 2300f) + DripPing(t - 1.9f, 1800f);
            }
            return LoopFold(raw, n, fade);
        }

        /// <summary>Short decaying ping with a slight downward pitch bend.</summary>
        private static float DripPing(float td, float freq)
        {
            if (td < 0f || td > 0.35f) return 0f;
            float f = freq * Mathf.Exp(-td * 2.5f);
            return Mathf.Sin(2f * Mathf.PI * f * td) * Mathf.Exp(-td * 26f) * 0.5f;
        }

        /// <summary>
        /// Folds the extra `fade` samples at the tail of `raw` back onto the
        /// head, producing a seamless `n`-sample loop.
        /// </summary>
        private static float[] LoopFold(float[] raw, int n, int fade)
        {
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = raw[i];
                if (i < fade) v = Mathf.Lerp(raw[n + i], raw[i], (float)i / fade);
                s[i] = v;
            }
            return s;
        }

        private static float Envelope(float t) =>
            t <= 0f ? 0f : Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / 0.15f);

        private static void WriteWav(string assetPath, float[] samples, int rate = Rate)
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
                w.Write(rate);
                w.Write(rate * 2);      // byte rate
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
