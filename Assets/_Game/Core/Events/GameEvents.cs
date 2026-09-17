using UnityEngine;

namespace Escape.Core
{
    public interface IGameEvent { }

    public readonly struct EvidenceCollectedEvent : IGameEvent
    {
        public readonly string EvidenceId;
        public EvidenceCollectedEvent(string evidenceId) => EvidenceId = evidenceId;
    }

    public readonly struct DocumentReadEvent : IGameEvent
    {
        public readonly string DocumentId;
        public DocumentReadEvent(string documentId) => DocumentId = documentId;
    }

    public readonly struct ObjectiveActivatedEvent : IGameEvent
    {
        public readonly string ObjectiveId;
        public ObjectiveActivatedEvent(string objectiveId) => ObjectiveId = objectiveId;
    }

    public readonly struct ObjectiveCompletedEvent : IGameEvent
    {
        public readonly string ObjectiveId;
        public ObjectiveCompletedEvent(string objectiveId) => ObjectiveId = objectiveId;
    }

    public readonly struct ObjectiveUpdatedEvent : IGameEvent { }

    public readonly struct InsightGainedEvent : IGameEvent
    {
        public readonly string InsightId;
        public InsightGainedEvent(string insightId) => InsightId = insightId;
    }

    public readonly struct DoorUnlockedEvent : IGameEvent
    {
        public readonly string DoorId;
        public readonly string SourceId;
        public DoorUnlockedEvent(string doorId, string sourceId)
        {
            DoorId = doorId;
            SourceId = sourceId;
        }
    }

    public readonly struct DoorStateChangedEvent : IGameEvent
    {
        public readonly string DoorId;
        public DoorStateChangedEvent(string doorId) => DoorId = doorId;
    }

    public readonly struct CameraDisabledEvent : IGameEvent
    {
        public readonly string CameraId;
        public CameraDisabledEvent(string cameraId) => CameraId = cameraId;
    }

    public readonly struct TerminalUnlockedEvent : IGameEvent
    {
        public readonly string TerminalId;
        public TerminalUnlockedEvent(string terminalId) => TerminalId = terminalId;
    }

    public readonly struct DetectionChangedEvent : IGameEvent
    {
        public readonly float Detection;
        public readonly DetectionLevel Level;
        public DetectionChangedEvent(float detection, DetectionLevel level)
        {
            Detection = detection;
            Level = level;
        }
    }

    public readonly struct AlertChangedEvent : IGameEvent
    {
        public readonly bool Alert;
        public AlertChangedEvent(bool alert) => Alert = alert;
    }

    public readonly struct LockdownChangedEvent : IGameEvent
    {
        public readonly bool Lockdown;
        public LockdownChangedEvent(bool lockdown) => Lockdown = lockdown;
    }

    public readonly struct SceneChangedEvent : IGameEvent
    {
        public readonly string SceneId;
        public SceneChangedEvent(string sceneId) => SceneId = sceneId;
    }

    public readonly struct BroadcastStartedEvent : IGameEvent { }

    public readonly struct BroadcastCompletedEvent : IGameEvent
    {
        public readonly string EndingId;
        public BroadcastCompletedEvent(string endingId) => EndingId = endingId;
    }

    public readonly struct PlayerCaughtEvent : IGameEvent
    {
        public readonly string SourceId;
        public PlayerCaughtEvent(string sourceId) => SourceId = sourceId;
    }

    public readonly struct SystemMessageEvent : IGameEvent
    {
        public readonly string Message;
        public readonly float Duration;
        public SystemMessageEvent(string message, float duration = 3f)
        {
            Message = message;
            Duration = duration;
        }
    }

    public readonly struct GameSavedEvent : IGameEvent
    {
        public readonly string Slot;
        public GameSavedEvent(string slot) => Slot = slot;
    }

    public readonly struct GameLoadedEvent : IGameEvent
    {
        public readonly string Slot;
        public GameLoadedEvent(string slot) => Slot = slot;
    }
}
