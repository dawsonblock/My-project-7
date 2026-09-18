using System.Collections;
using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Guard reached the player: disable controls, sting, fade, reload the
    /// last checkpoint. No combat.
    /// </summary>
    public sealed class CaughtSequence : MonoBehaviour
    {
        [SerializeField] private PlayerState playerState;

        private IGameEventBus _events;
        private IGameCommandDispatcher _dispatcher;
        private GameServices _services;
        private bool _running;

        // Bind + subscribe on enable, unwind on disable — symmetric, so a
        // disable/enable cycle leaves the event subscription intact exactly
        // once. Start retries the bind in case GameRoot lagged scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_events != null || GameRoot.Instance == null) return;
            _services = GameRoot.Instance.Services;
            _events = _services.Get<IGameEventBus>();
            _dispatcher = _services.Get<IGameCommandDispatcher>();
            _events.Subscribe<PlayerCaughtEvent>(OnCaught);
            if (playerState == null) playerState = FindAnyObjectByType<PlayerState>();
        }

        private void OnDisable()
        {
            _events?.Unsubscribe<PlayerCaughtEvent>(OnCaught);
            _events = null;
        }

        private void OnCaught(PlayerCaughtEvent evt)
        {
            if (_running) return;
            StartCoroutine(Routine());
        }

        private IEnumerator Routine()
        {
            _running = true;
            if (playerState != null) playerState.Caught = true;
            _events.Publish(new SystemMessageEvent("CAUGHT", 2f));

            if (_services.TryGet<IScreenFader>(out var fader))
                yield return fader.FadeOut();
            else
                yield return new WaitForSeconds(1.2f);

            // Reload the checkpoint. Guard/scene state restores from save.
            _dispatcher.Dispatch(new LoadGameCommand(SaveService.Autosave));
            _running = false;
        }
    }
}
