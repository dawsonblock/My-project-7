using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Security device root: registers with WorldService so terminal
    /// DISABLE commands can reach it, and restores disabled state on load.
    /// </summary>
    public sealed class SecurityCamera : MonoBehaviour, ISecurityDeviceObject
    {
        [SerializeField] private string id;
        [SerializeField] private CameraMotor motor;
        [SerializeField] private CameraSensor sensor;
        [SerializeField] private CameraConeRenderer cone;
        [SerializeField] private Light statusLight;
        [SerializeField] private AudioSource motorAudio;

        private IWorldService _world;

        public string Id => id;
        public bool Disabled { get; private set; }

        private void Start()
        {
            _world = GameRoot.Instance.Services.Get<IWorldService>();
            if (motorAudio == null) motorAudio = GetComponent<AudioSource>();
            if (motorAudio != null && motorAudio.clip == null)
                motorAudio.clip = Data.ClipLibrary.Get()?.cameraHum;
            _world.Register(this);
            if (!Disabled && motorAudio != null && motorAudio.clip != null && !motorAudio.isPlaying)
                motorAudio.Play();
        }

        private void OnDisable() => _world?.Unregister(this);

        public void RestoreFromState(GameState state)
        {
            if (state.DisabledCameras.Contains(id)) ApplyDisabled();
        }

        public void Disable(string sourceId) => ApplyDisabled();

        private void ApplyDisabled()
        {
            Disabled = true;
            if (motor != null) motor.enabled = false;
            if (sensor != null) sensor.enabled = false;
            if (cone != null) cone.SetDisabled(true);
            if (statusLight != null) statusLight.enabled = false;
            if (motorAudio != null) motorAudio.Stop();
        }
    }
}
