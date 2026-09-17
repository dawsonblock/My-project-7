using Escape.Core;
using Escape.Data;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// A world prop that grants an EvidenceDefinition on pickup. Registers
    /// with WorldService; if the evidence is already collected it removes
    /// itself on scene load.
    /// </summary>
        public sealed class EvidenceInteractable : MonoBehaviour, IInteractable, IWorldObject
    {
        [SerializeField] private EvidenceDefinition evidence;
        [SerializeField] private string promptLabel = "";

        private IWorldService _world;

        public string Id => evidence != null ? evidence.Id : "";

        private void Start()
        {
            var services = GameRoot.Instance.Services;
            _world = services.Get<IWorldService>();
            _world.Register(this);
        }

        private void OnDisable() => _world?.Unregister(this);

        public void RestoreFromState(GameState state)
        {
            if (evidence != null && state.CollectedEvidence.Contains(evidence.Id))
                gameObject.SetActive(false);
        }

        public InteractionPrompt GetPrompt(PlayerContext player)
        {
            if (evidence == null) return InteractionPrompt.None;
            if (player.GameState.CollectedEvidence.Contains(evidence.Id)) return InteractionPrompt.None;
            var label = string.IsNullOrEmpty(promptLabel) ? evidence.Title : promptLabel;
            return new InteractionPrompt($"[E] {label}");
        }

        public bool CanInteract(PlayerContext player) =>
            evidence != null && !player.GameState.CollectedEvidence.Contains(evidence.Id);

        public void Interact(PlayerContext player)
        {
            player.Dispatcher.Dispatch(new CollectEvidenceCommand(evidence.Id, name));
            if (player.Services.TryGet<IAudioService>(out var audio))
                audio.Play2D(ClipLibrary.Get()?.pickup, 0.8f);
            gameObject.SetActive(false);
        }
    }
}
