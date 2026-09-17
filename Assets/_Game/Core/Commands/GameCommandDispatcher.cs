using System;
using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    public interface IGameCommandHandler<in T> where T : IGameCommand
    {
        void Handle(T command);
    }

    /// <summary>
    /// Single entry point for all game mutations. Every command is journaled
    /// before it reaches its handler, giving a free audit log for QA.
    /// </summary>
    public interface IGameCommandDispatcher
    {
        void Register<T>(IGameCommandHandler<T> handler) where T : IGameCommand;
        void Dispatch<T>(T command) where T : IGameCommand;
    }

    public sealed class GameCommandDispatcher : IGameCommandDispatcher
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();
        private readonly CommandJournal _journal;

        public GameCommandDispatcher(CommandJournal journal) => _journal = journal;

        public void Register<T>(IGameCommandHandler<T> handler) where T : IGameCommand
        {
            _handlers[typeof(T)] = handler;
        }

        public void Dispatch<T>(T command) where T : IGameCommand
        {
            _journal?.Record(command.ToString());
            if (_handlers.TryGetValue(typeof(T), out var h))
            {
                ((IGameCommandHandler<T>)h).Handle(command);
            }
            else
            {
                Debug.LogWarning($"[Dispatcher] No handler for {typeof(T).Name}: {command}");
            }
        }
    }
}
