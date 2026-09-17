using UnityEngine;

namespace Escape.Data
{
    /// <summary>
    /// All stealth tuning lives here so designers never touch code.
    /// </summary>
    [CreateAssetMenu(menuName = "Escape/Stealth Tuning", fileName = "StealthTuning")]
    public sealed class StealthTuning : ScriptableObject
    {
        [Header("Movement (m/s)")]
        public float WalkSpeed = 3.0f;
        public float CrouchSpeed = 1.6f;
        public float SprintSpeed = 5.0f;
        public float Acceleration = 14f;
        public float Deceleration = 18f;
        public float Gravity = -18f;

        [Header("Detection thresholds (0-100)")]
        public float SuspiciousThreshold = 25f;
        public float ImminentThreshold = 60f;
        public float DetectedThreshold = 100f;

        [Header("Detection rates")]
        public float CameraDetectionPerSecond = 45f;
        public float GuardDetectionPerSecond = 70f;
        public float DecayHiddenPerSecond = 50f;
        public float DecayOutOfSightPerSecond = 18f;
        public float DecayLockdownPerSecond = 6f;

        [Header("Cameras")]
        public float CameraRange = 14f;
        public float CameraFovDegrees = 62f;
        public float CameraSweepDegrees = 45f;
        public float CameraSweepPeriod = 6f;

        [Header("Guards")]
        public float GuardVisionRange = 16f;
        public float GuardFovDegrees = 100f;
        public float GuardHearingRadiusMultiplier = 1f;
        public float GuardWalkSpeed = 1.7f;
        public float GuardInvestigateSpeed = 2.6f;
        public float GuardChaseSpeed = 4.6f;
        public float GuardCatchDistance = 1.1f;
        public float SearchDuration = 12f;
        public float SearchRadius = 5f;
        public float LoseSightAfterSeconds = 4f;

        [Header("Noise (base radius metres)")]
        public float FootstepBaseRadius = 6f;
        public float SprintNoiseMultiplier = 2.4f;
        public float WalkNoiseMultiplier = 1.0f;
        public float CrouchNoiseMultiplier = 0.3f;
        public float DoorNoiseRadius = 7f;
        public float TerminalNoiseRadius = 2f;
        public float AlarmNoiseRadius = 40f;
        public float ThrownNoiseRadius = 14f;
        public float WhistleNoiseRadius = 9f;
        public float WhistleCooldown = 2.5f;

        [Header("Lures")]
        public int StartLures = 3;
        public int MaxLures = 6;
        public float ThrowForce = 9f;
        public float ThrowArc = 3f;

        [Header("Stamina")]
        public float MaxStamina = 100f;
        public float SprintStaminaDrain = 20f;
        public float StaminaRegenRate = 15f;
        public float StaminaRegenDelay = 0.8f;
        public float StaminaResumeThreshold = 25f;

        [Header("Focus aim")]
        public float FocusFovScale = 0.65f;
        public float FocusSensitivityScale = 0.6f;

        [Header("Visibility multipliers")]
        public float BrightVisibility = 1.3f;
        public float NormalVisibility = 1.0f;
        public float DimVisibility = 0.7f;
        public float DarkVisibility = 0.45f;
        public float HidingVisibility = 0.25f;
        public float CrouchVisibilityBonus = 0.8f;
        public float FlashlightVisibilityPenalty = 1.35f;

        [Header("Interaction")]
        public float InteractDistance = 2.6f;
    }
}
