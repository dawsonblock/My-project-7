using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// In-memory settings model. Loaded from PlayerPrefs once at startup;
    /// gameplay systems read cached values — no PlayerPrefs traffic per frame.
    /// Writes mark the model dirty; Persist() flushes when the settings UI
    /// closes or the app pauses/quits.
    /// </summary>
    public sealed class GameSettings
    {
        public float MouseSensitivity = 1f;
        public float ControllerSensitivity = 1f;
        public bool InvertY;
        public float Fov = 70f;
        public bool HeadBob = true;
        public bool ReduceMotion;
        public bool ToggleSprint;
        public bool ToggleCrouch;
        public bool ToggleFlashlight = true;
        public bool LargeUI;
        public bool HighContrast;
        public bool Subtitles = true;
        public float MasterVolume = 1f;
        public float MusicVolume = 0.8f;
        public float AmbienceVolume = 0.9f;
        public float SfxVolume = 1f;
    }

    public interface ISettingsService
    {
        /// <summary>Live settings — cheap to read every frame.</summary>
        GameSettings Current { get; }
        /// <summary>Raised after any setting changes so appliers can react.</summary>
        event System.Action Changed;
        /// <summary>Flush pending changes to PlayerPrefs (dirty-only).</summary>
        void Persist();

        float MouseSensitivity { get; set; }
        float ControllerSensitivity { get; set; }
        bool InvertY { get; set; }
        float Fov { get; set; }
        bool HeadBob { get; set; }
        bool ReduceMotion { get; set; }
        bool ToggleSprint { get; set; }
        bool ToggleCrouch { get; set; }
        bool ToggleFlashlight { get; set; }
        bool LargeUI { get; set; }
        bool HighContrast { get; set; }
        bool Subtitles { get; set; }
        float MasterVolume { get; set; }
        float MusicVolume { get; set; }
        float AmbienceVolume { get; set; }
        float SfxVolume { get; set; }
    }

    public sealed class SettingsService : ISettingsService
    {
        private readonly GameSettings _s = new GameSettings();
        private bool _dirty;

        public GameSettings Current => _s;
        public event System.Action Changed;

        public SettingsService() => Load();

        private void Load()
        {
            _s.MouseSensitivity = GetF("mouse_sens", 1f);
            _s.ControllerSensitivity = GetF("pad_sens", 1f);
            _s.InvertY = GetB("invert_y", false);
            _s.Fov = GetF("fov", 70f);
            _s.HeadBob = GetB("headbob", true);
            _s.ReduceMotion = GetB("reduce_motion", false);
            _s.ToggleSprint = GetB("toggle_sprint", false);
            _s.ToggleCrouch = GetB("toggle_crouch", false);
            _s.ToggleFlashlight = GetB("toggle_flashlight", true);
            _s.LargeUI = GetB("large_ui", false);
            _s.HighContrast = GetB("high_contrast", false);
            _s.Subtitles = GetB("subtitles", true);
            _s.MasterVolume = GetF("vol_master", 1f);
            _s.MusicVolume = GetF("vol_music", 0.8f);
            _s.AmbienceVolume = GetF("vol_ambience", 0.9f);
            _s.SfxVolume = GetF("vol_sfx", 1f);
        }

        public void Persist()
        {
            if (!_dirty) return;
            PlayerPrefs.SetFloat("set_mouse_sens", _s.MouseSensitivity);
            PlayerPrefs.SetFloat("set_pad_sens", _s.ControllerSensitivity);
            PlayerPrefs.SetInt("set_invert_y", _s.InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("set_fov", _s.Fov);
            PlayerPrefs.SetInt("set_headbob", _s.HeadBob ? 1 : 0);
            PlayerPrefs.SetInt("set_reduce_motion", _s.ReduceMotion ? 1 : 0);
            PlayerPrefs.SetInt("set_toggle_sprint", _s.ToggleSprint ? 1 : 0);
            PlayerPrefs.SetInt("set_toggle_crouch", _s.ToggleCrouch ? 1 : 0);
            PlayerPrefs.SetInt("set_toggle_flashlight", _s.ToggleFlashlight ? 1 : 0);
            PlayerPrefs.SetInt("set_large_ui", _s.LargeUI ? 1 : 0);
            PlayerPrefs.SetInt("set_high_contrast", _s.HighContrast ? 1 : 0);
            PlayerPrefs.SetInt("set_subtitles", _s.Subtitles ? 1 : 0);
            PlayerPrefs.SetFloat("set_vol_master", _s.MasterVolume);
            PlayerPrefs.SetFloat("set_vol_music", _s.MusicVolume);
            PlayerPrefs.SetFloat("set_vol_ambience", _s.AmbienceVolume);
            PlayerPrefs.SetFloat("set_vol_sfx", _s.SfxVolume);
            PlayerPrefs.Save();
            _dirty = false;
        }

        private static float GetF(string k, float d) => PlayerPrefs.GetFloat("set_" + k, d);
        private static bool GetB(string k, bool d) => PlayerPrefs.GetInt("set_" + k, d ? 1 : 0) == 1;

        private void Set<T>(ref T field, T value)
        {
            if (Equals(field, value)) return;
            field = value;
            _dirty = true;
            Changed?.Invoke();
        }

        public float MouseSensitivity { get => _s.MouseSensitivity; set => Set(ref _s.MouseSensitivity, value); }
        public float ControllerSensitivity { get => _s.ControllerSensitivity; set => Set(ref _s.ControllerSensitivity, value); }
        public bool InvertY { get => _s.InvertY; set => Set(ref _s.InvertY, value); }
        public float Fov { get => _s.Fov; set => Set(ref _s.Fov, value); }
        public bool HeadBob { get => _s.HeadBob; set => Set(ref _s.HeadBob, value); }
        public bool ReduceMotion { get => _s.ReduceMotion; set => Set(ref _s.ReduceMotion, value); }
        public bool ToggleSprint { get => _s.ToggleSprint; set => Set(ref _s.ToggleSprint, value); }
        public bool ToggleCrouch { get => _s.ToggleCrouch; set => Set(ref _s.ToggleCrouch, value); }
        public bool ToggleFlashlight { get => _s.ToggleFlashlight; set => Set(ref _s.ToggleFlashlight, value); }
        public bool LargeUI { get => _s.LargeUI; set => Set(ref _s.LargeUI, value); }
        public bool HighContrast { get => _s.HighContrast; set => Set(ref _s.HighContrast, value); }
        public bool Subtitles { get => _s.Subtitles; set => Set(ref _s.Subtitles, value); }
        public float MasterVolume { get => _s.MasterVolume; set => Set(ref _s.MasterVolume, value); }
        public float MusicVolume { get => _s.MusicVolume; set => Set(ref _s.MusicVolume, value); }
        public float AmbienceVolume { get => _s.AmbienceVolume; set => Set(ref _s.AmbienceVolume, value); }
        public float SfxVolume { get => _s.SfxVolume; set => Set(ref _s.SfxVolume, value); }
    }
}
