namespace Escape.Core
{
    public interface IGameCommand { }

    public readonly struct CollectEvidenceCommand : IGameCommand
    {
        public readonly string EvidenceId;
        public readonly string SourceId;
        public CollectEvidenceCommand(string evidenceId, string sourceId = "")
        {
            EvidenceId = evidenceId;
            SourceId = sourceId;
        }
        public override string ToString() => $"COLLECT_EVIDENCE {EvidenceId}";
    }

    public readonly struct ReadDocumentCommand : IGameCommand
    {
        public readonly string DocumentId;
        public ReadDocumentCommand(string documentId) => DocumentId = documentId;
        public override string ToString() => $"READ_DOCUMENT {DocumentId}";
    }

    public readonly struct ActivateObjectiveCommand : IGameCommand
    {
        public readonly string ObjectiveId;
        public ActivateObjectiveCommand(string objectiveId) => ObjectiveId = objectiveId;
        public override string ToString() => $"ACTIVATE_OBJECTIVE {ObjectiveId}";
    }

    public readonly struct CompleteObjectiveCommand : IGameCommand
    {
        public readonly string ObjectiveId;
        public readonly string SourceId;
        public CompleteObjectiveCommand(string objectiveId, string sourceId = "")
        {
            ObjectiveId = objectiveId;
            SourceId = sourceId;
        }
        public override string ToString() => $"COMPLETE_OBJECTIVE {ObjectiveId}";
    }

    public readonly struct UnlockDoorCommand : IGameCommand
    {
        public readonly string DoorId;
        public readonly string SourceId;
        public UnlockDoorCommand(string doorId, string sourceId)
        {
            DoorId = doorId;
            SourceId = sourceId;
        }
        public override string ToString() => $"UNLOCK_DOOR {DoorId} (src {SourceId})";
    }

    public readonly struct UnlockTerminalCommand : IGameCommand
    {
        public readonly string TerminalId;
        public UnlockTerminalCommand(string terminalId) => TerminalId = terminalId;
        public override string ToString() => $"UNLOCK_TERMINAL {TerminalId}";
    }

    public readonly struct DisableCameraCommand : IGameCommand
    {
        public readonly string CameraId;
        public readonly string SourceId;
        public DisableCameraCommand(string cameraId, string sourceId)
        {
            CameraId = cameraId;
            SourceId = sourceId;
        }
        public override string ToString() => $"DISABLE_CAMERA {CameraId} (src {SourceId})";
    }

    public readonly struct GainInsightCommand : IGameCommand
    {
        public readonly string InsightId;
        public GainInsightCommand(string insightId) => InsightId = insightId;
        public override string ToString() => $"GAIN_INSIGHT {InsightId}";
    }

    public readonly struct SetAlertCommand : IGameCommand
    {
        public readonly bool Alert;
        public readonly string SourceId;
        public SetAlertCommand(bool alert, string sourceId = "")
        {
            Alert = alert;
            SourceId = sourceId;
        }
        public override string ToString() => $"SET_ALERT {Alert}";
    }

    public readonly struct SetLockdownCommand : IGameCommand
    {
        public readonly bool Lockdown;
        public readonly string SourceId;
        public SetLockdownCommand(bool lockdown, string sourceId = "")
        {
            Lockdown = lockdown;
            SourceId = sourceId;
        }
        public override string ToString() => $"SET_LOCKDOWN {Lockdown}";
    }

    /// <summary>
    /// Routes the broadcast signal through a node. Carries the routing
    /// objective so the domain validates the route against content instead of
    /// trusting whichever surface raised it.
    /// </summary>
    public readonly struct RouteBroadcastCommand : IGameCommand
    {
        public readonly string ObjectiveId;
        public readonly string SourceId;
        public RouteBroadcastCommand(string objectiveId, string sourceId)
        {
            ObjectiveId = objectiveId;
            SourceId = sourceId;
        }
        public override string ToString() => $"ROUTE_BROADCAST {ObjectiveId}";
    }

    /// <summary>
    /// Transmits the evidence. Carries the transmission objective: the domain
    /// refuses an unrouted relay and completes the objective itself, so a
    /// caller cannot manufacture a broadcast state by sequencing commands.
    /// </summary>
    public readonly struct CompleteBroadcastCommand : IGameCommand
    {
        public readonly string ObjectiveId;
        public CompleteBroadcastCommand(string objectiveId) => ObjectiveId = objectiveId;
        public override string ToString() => $"COMPLETE_BROADCAST {ObjectiveId}";
    }

    public readonly struct SaveGameCommand : IGameCommand
    {
        public readonly string Slot;
        public SaveGameCommand(string slot) => Slot = slot;
        public override string ToString() => $"SAVE_GAME {Slot}";
    }

    public readonly struct LoadGameCommand : IGameCommand
    {
        public readonly string Slot;
        public LoadGameCommand(string slot) => Slot = slot;
        public override string ToString() => $"LOAD_GAME {Slot}";
    }

    public readonly struct ChangeSceneCommand : IGameCommand
    {
        public readonly string SceneId;
        public readonly string SpawnId;
        public ChangeSceneCommand(string sceneId, string spawnId = "")
        {
            SceneId = sceneId;
            SpawnId = spawnId;
        }
        public override string ToString() => $"CHANGE_SCENE {SceneId}";
    }

    public readonly struct RecordTerminalUseCommand : IGameCommand
    {
        public readonly string TerminalId;
        public readonly string Command;
        public RecordTerminalUseCommand(string terminalId, string command)
        {
            TerminalId = terminalId;
            Command = command;
        }
        public override string ToString() => $"TERMINAL_USE {TerminalId}:{Command}";
    }

    public readonly struct ShowSystemMessageCommand : IGameCommand
    {
        public readonly string Message;
        public readonly float Duration;
        public ShowSystemMessageCommand(string message, float duration = 3f)
        {
            Message = message;
            Duration = duration;
        }
        public override string ToString() => $"MESSAGE \"{Message}\"";
    }
}
