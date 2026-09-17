using System;
using UnityEngine;

namespace Escape.Core
{
    public enum NoiseType
    {
        Footstep,
        Sprint,
        Door,
        ThrownObject,
        Terminal,
        Alarm,
        Environmental
    }

    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly float Strength;
        public readonly NoiseType Type;
        public readonly UnityEngine.Object Source;

        public NoiseEvent(Vector3 position, float radius, float strength, NoiseType type, UnityEngine.Object source)
        {
            Position = position;
            Radius = radius;
            Strength = strength;
            Type = type;
            Source = source;
        }
    }

    /// <summary>
    /// One funnel for every noise in the game. Footsteps, doors, thrown
    /// objects and alarms all flow through here — no per-object AI code.
    /// </summary>
    public interface INoiseService
    {
        event Action<NoiseEvent> OnNoise;
        void Emit(NoiseEvent noise);
    }

    public sealed class NoiseService : INoiseService
    {
        public event Action<NoiseEvent> OnNoise;

        public void Emit(NoiseEvent noise) => OnNoise?.Invoke(noise);
    }
}
