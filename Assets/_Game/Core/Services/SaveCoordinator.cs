using System.Collections.Generic;

namespace Escape.Core
{
    /// <summary>
    /// Implemented by runtime objects that own volatile state which must
    /// be captured into (or restored from) GameState around save/load.
    /// Doors/cameras/terminals already participate via
    /// IWorldObject.RestoreFromState — this interface is for live pose and
    /// device state that no GameState list covers.
    /// </summary>
    public interface ISaveParticipant
    {
        void CaptureSaveState(GameState state);
        void RestoreSaveState(GameState state);
    }

    /// <summary>
    /// Registry + apply point for ISaveParticipant. SaveService calls
    /// CaptureInto before serializing; SceneBootstrap calls RestoreFrom on
    /// the load path after the player exists.
    /// </summary>
    public interface ISaveCoordinator
    {
        void Register(ISaveParticipant participant);
        void Unregister(ISaveParticipant participant);
        void CaptureInto(GameState state);
        void RestoreFrom(GameState state);
    }

    public sealed class SaveCoordinator : ISaveCoordinator
    {
        private readonly List<ISaveParticipant> _participants = new List<ISaveParticipant>();

        public void Register(ISaveParticipant participant)
        {
            if (participant != null && !_participants.Contains(participant))
                _participants.Add(participant);
        }

        public void Unregister(ISaveParticipant participant) =>
            _participants.Remove(participant);

        public void CaptureInto(GameState state)
        {
            for (int i = 0; i < _participants.Count; i++)
                _participants[i].CaptureSaveState(state);
        }

        public void RestoreFrom(GameState state)
        {
            for (int i = 0; i < _participants.Count; i++)
                _participants[i].RestoreSaveState(state);
        }
    }
}
