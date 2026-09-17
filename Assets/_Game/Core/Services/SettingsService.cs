using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Player preferences stored separately from save games (PlayerPrefs).
    /// Accessibility settings live here from day one.
    /// </summary>
    public interface ISettingsService
    {
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
        private float GetF(string k, float d) => PlayerPrefs.GetFloat("set_" + k, d);
        private void SetF(string k, float v) { PlayerPrefs.SetFloat("set_" + k, v); PlayerPrefs.Save(); }
        private bool GetB(string k, bool d) => PlayerPrefs.GetInt("set_" + k, d ? 1 : 0) == 1;
        private void SetB(string k, bool v) { PlayerPrefs.SetInt("set_" + k, v ? 1 : 0); PlayerPrefs.Save(); }

        public float MouseSensitivity { get => GetF("mouse_sens", 1f); set => SetF("mouse_sens", value); }
        public float ControllerSensitivity { get => GetF("pad_sens", 1f); set => SetF("pad_sens", value); }
        public bool InvertY { get => GetB("invert_y", false); set => SetB("invert_y", value); }
        public float Fov { get => GetF("fov", 70f); set => SetF("fov", value); }
        public bool HeadBob { get => GetB("headbob", true); set => SetB("headbob", value); }
        public bool ReduceMotion { get => GetB("reduce_motion", false); set => SetB("reduce_motion", value); }
        public bool ToggleSprint { get => GetB("toggle_sprint", false); set => SetB("toggle_sprint", value); }
        public bool ToggleCrouch { get => GetB("toggle_crouch", false); set => SetB("toggle_crouch", value); }
        public bool ToggleFlashlight { get => GetB("toggle_flashlight", true); set => SetB("toggle_flashlight", value); }
        public bool LargeUI { get => GetB("large_ui", false); set => SetB("large_ui", value); }
        public bool HighContrast { get => GetB("high_contrast", false); set => SetB("high_contrast", value); }
        public bool Subtitles { get => GetB("subtitles", true); set => SetB("subtitles", value); }
        public float MasterVolume { get => GetF("vol_master", 1f); set => SetF("vol_master", value); }
        public float MusicVolume { get => GetF("vol_music", 0.8f); set => SetF("vol_music", value); }
        public float AmbienceVolume { get => GetF("vol_ambience", 0.9f); set => SetF("vol_ambience", value); }
        public float SfxVolume { get => GetF("vol_sfx", 1f); set => SetF("vol_sfx", value); }
    }
}
