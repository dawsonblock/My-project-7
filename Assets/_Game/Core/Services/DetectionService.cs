using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    public enum DetectionType
    {
        Camera,
        GuardVision,
        GuardHearing,
        Alarm
    }

    /// <summary>
    /// Aggregates per-source detection contributions into one global 0-100
    /// meter. Sensors call Submit() each frame they perceive the player;
    /// GameRoot calls Tick() once per frame to integrate and decay.
    /// </summary>
    public interface IDetectionService
    {
        float Detection { get; }
        DetectionLevel Level { get; }
        void Submit(string sourceId, DetectionType type, float amountPerSecond);
        void Tick(float deltaTime);
        void Reset();
    }

    public sealed class DetectionService : IDetectionService
    {
        private struct Contribution
        {
            public DetectionType Type;
            public float Amount;
        }

        private readonly IGameStateService _state;
        private readonly Escape.Data.StealthTuning _tuning;
        private readonly IGameEventBus _events;
        private readonly IGameCommandDispatcher _dispatcher;
        private readonly Dictionary<string, Contribution> _frame = new Dictionary<string, Contribution>();

        /// <summary>Backed by GameState so save/load and NewGame stay authoritative.</summary>
        public float Detection => _state.State.Detection;
        public DetectionLevel Level { get; private set; } = DetectionLevel.Safe;
        /// <summary>True while any sensor saw the player this frame.</summary>
        public bool AnyContributionThisFrame => _frame.Count > 0;

        public DetectionService(IGameStateService state, Escape.Data.StealthTuning tuning,
            IGameEventBus events, IGameCommandDispatcher dispatcher)
        {
            _state = state;
            _tuning = tuning;
            _events = events;
            _dispatcher = dispatcher;
        }

        public void Submit(string sourceId, DetectionType type, float amountPerSecond)
        {
            _frame[sourceId] = new Contribution { Type = type, Amount = amountPerSecond };
        }

        public void Tick(float deltaTime)
        {
            float max = 0f;
            foreach (var kv in _frame) max = Mathf.Max(max, kv.Value.Amount);

            float decay = _state.State.Lockdown ? _tuning.DecayLockdownPerSecond : _tuning.DecayOutOfSightPerSecond;
            _state.State.Detection = max > 0f
                ? Mathf.Min(_tuning.DetectedThreshold, Detection + max * deltaTime)
                : Mathf.Max(0f, Detection - decay * deltaTime);

            _frame.Clear();

            var level = ComputeLevel(Detection);
            if (level != Level)
            {
                Level = level;
                _events.Publish(new DetectionChangedEvent(Detection, Level));
                if (Level == DetectionLevel.Detected)
                    _dispatcher.Dispatch(new SetAlertCommand(true, "detection"));
            }
        }

        private DetectionLevel ComputeLevel(float d)
        {
            if (d >= _tuning.DetectedThreshold) return DetectionLevel.Detected;
            if (d >= _tuning.ImminentThreshold) return DetectionLevel.Imminent;
            if (d >= _tuning.SuspiciousThreshold) return DetectionLevel.Suspicious;
            if (d <= 0.01f) return DetectionLevel.Hidden;
            return DetectionLevel.Safe;
        }

        public void Reset()
        {
            _state.State.Detection = 0f;
            Level = DetectionLevel.Hidden;
            _frame.Clear();
        }
    }
}
