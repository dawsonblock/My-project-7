using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Implemented by scene objects (doors, cameras) so services can reach
    /// the live instance behind an id without hard references.
    /// </summary>
    public interface IWorldObject
    {
        string Id { get; }
        void RestoreFromState(GameState state);
    }

    public interface IDoorObject : IWorldObject
    {
        void Unlock(string sourceId);
        void Open();
    }

    public interface ISecurityDeviceObject : IWorldObject
    {
        void Disable(string sourceId);
    }

    /// <summary>
    /// Registry of live scene objects keyed by id, plus the command handlers
    /// for world mutations (doors, cameras, terminals, alert, lockdown,
    /// broadcast, system messages).
    /// </summary>
    public interface IWorldService
    {
        void Register(IWorldObject obj);
        void Unregister(IWorldObject obj);
        bool TryGet<T>(string id, out T obj) where T : class, IWorldObject;
    }

    public sealed class WorldService : IWorldService,
        IGameCommandHandler<UnlockDoorCommand>,
        IGameCommandHandler<DisableCameraCommand>,
        IGameCommandHandler<UnlockTerminalCommand>,
        IGameCommandHandler<SetAlertCommand>,
        IGameCommandHandler<SetLockdownCommand>,
        IGameCommandHandler<RouteBroadcastCommand>,
        IGameCommandHandler<CompleteBroadcastCommand>,
        IGameCommandHandler<RecordTerminalUseCommand>,
        IGameCommandHandler<ShowSystemMessageCommand>
    {
        private readonly IGameStateService _state;
        private readonly IGameEventBus _events;
        private readonly EndingService _endings;
        private readonly IObjectiveService _objectives;
        private readonly Dictionary<string, IWorldObject> _objects = new Dictionary<string, IWorldObject>();

        public WorldService(IGameStateService state, IGameEventBus events, EndingService endings,
            IObjectiveService objectives)
        {
            _state = state;
            _events = events;
            _endings = endings;
            _objectives = objectives;
        }

        public void Register(IWorldObject obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.Id)) return;
            // Two authored objects sharing an id would silently fight over the
            // same persisted state, and one would be unreachable. Re-registering
            // the same instance (a disable/enable cycle) is legitimate.
            if (_objects.TryGetValue(obj.Id, out var existing) && !ReferenceEquals(existing, obj))
            {
                Debug.LogError($"[WorldService] Duplicate world object id '{obj.Id}' — " +
                               "refusing to overwrite the registered object.");
                return;
            }
            _objects[obj.Id] = obj;
            obj.RestoreFromState(_state.State);
        }

        public void Unregister(IWorldObject obj)
        {
            if (obj != null && _objects.TryGetValue(obj.Id, out var existing) && existing == obj)
                _objects.Remove(obj.Id);
        }

        public bool TryGet<T>(string id, out T obj) where T : class, IWorldObject
        {
            obj = null;
            return _objects.TryGetValue(id ?? "", out var o) && (obj = o as T) != null;
        }

        public void Handle(UnlockDoorCommand command)
        {
            var s = _state.State;
            if (!s.UnlockedDoors.Contains(command.DoorId))
                s.UnlockedDoors.Add(command.DoorId);
            _events.Publish(new DoorUnlockedEvent(command.DoorId, command.SourceId));
            if (TryGet<IDoorObject>(command.DoorId, out var door)) door.Unlock(command.SourceId);
        }

        public void Handle(DisableCameraCommand command)
        {
            var s = _state.State;
            if (!s.DisabledCameras.Contains(command.CameraId))
                s.DisabledCameras.Add(command.CameraId);
            _events.Publish(new CameraDisabledEvent(command.CameraId));
            if (TryGet<ISecurityDeviceObject>(command.CameraId, out var cam)) cam.Disable(command.SourceId);
        }

        public void Handle(UnlockTerminalCommand command)
        {
            var s = _state.State;
            if (!s.UnlockedTerminals.Contains(command.TerminalId))
            {
                s.UnlockedTerminals.Add(command.TerminalId);
                _events.Publish(new TerminalUnlockedEvent(command.TerminalId));
            }
        }

        public void Handle(SetAlertCommand command)
        {
            if (_state.State.Alert == command.Alert) return;
            _state.State.Alert = command.Alert;
            _events.Publish(new AlertChangedEvent(command.Alert));
            if (command.Alert)
                _events.Publish(new SystemMessageEvent("SECURITY ALERT", 4f));
        }

        public void Handle(SetLockdownCommand command)
        {
            if (_state.State.Lockdown == command.Lockdown) return;
            _state.State.Lockdown = command.Lockdown;
            _events.Publish(new LockdownChangedEvent(command.Lockdown));
            _events.Publish(new SystemMessageEvent(command.Lockdown ? "LOCKDOWN INITIATED" : "LOCKDOWN LIFTED", 4f));
        }

        /// <summary>
        /// Routes the signal. Refuses unless the routing objective's own
        /// prerequisites are satisfied, so a caller cannot start a broadcast
        /// the content says is not yet legal.
        /// </summary>
        public void Handle(RouteBroadcastCommand command)
        {
            if (_state.State.BroadcastStarted) return;
            if (!_objectives.CanComplete(command.ObjectiveId))
            {
                _events.Publish(new SystemMessageEvent(
                    "RELAY REFUSED — routing requirements not met", 4f));
                return;
            }
            _objectives.CompleteFromAction(command.ObjectiveId, command.SourceId);
            _state.State.BroadcastStarted = true;
            _events.Publish(new BroadcastStartedEvent());
        }

        /// <summary>
        /// Transmits. A transmission is only legal from a routed relay, and
        /// only once the transmission objective's prerequisites hold — the
        /// domain completes that objective itself, so the ending can never be
        /// reached by sequencing commands from outside.
        /// </summary>
        public void Handle(CompleteBroadcastCommand command)
        {
            var s = _state.State;
            if (s.BroadcastCompleted) return;
            if (!s.BroadcastStarted)
            {
                _events.Publish(new SystemMessageEvent("RELAY COLD — route the signal first", 4f));
                return;
            }
            if (!_objectives.CanComplete(command.ObjectiveId))
            {
                _events.Publish(new SystemMessageEvent(
                    "TRANSMISSION REFUSED — prerequisites unmet", 4f));
                return;
            }
            _objectives.CompleteFromAction(command.ObjectiveId, "broadcast");
            s.BroadcastCompleted = true;
            var result = _endings.Evaluate();
            s.EndingId = result.Ending != null ? result.Ending.Id : "";
            _events.Publish(new BroadcastCompletedEvent(s.EndingId));
        }

        public void Handle(RecordTerminalUseCommand command)
        {
            var key = command.TerminalId + ":" + command.Command;
            var used = _state.State.UsedTerminalCommands;
            if (!used.Contains(key)) used.Add(key);
        }

        public void Handle(ShowSystemMessageCommand command)
        {
            _events.Publish(new SystemMessageEvent(command.Message, command.Duration));
        }
    }
}
