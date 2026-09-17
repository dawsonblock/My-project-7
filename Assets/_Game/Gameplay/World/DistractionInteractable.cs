using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Generic distraction prop: clattering pipe, breaker, bottle. Emits a
    /// loud NoiseEvent — guards investigate, no bespoke AI needed.
    /// </summary>
        public sealed class DistractionInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string label = "Make noise";
        [SerializeField] private float radius = 14f;
        [SerializeField] private float cooldown = 4f;
        [SerializeField] private AudioSource audioSource;

        private float _cooldownUntil;

        public InteractionPrompt GetPrompt(PlayerContext player) =>
            Time.time < _cooldownUntil
                ? InteractionPrompt.None
                : new InteractionPrompt($"[E] {label}");

        public bool CanInteract(PlayerContext player) => Time.time >= _cooldownUntil;

        public void Interact(PlayerContext player)
        {
            _cooldownUntil = Time.time + cooldown;
            if (audioSource != null)
            {
                var clip = Data.ClipLibrary.Pick(Data.ClipLibrary.Get()?.metalImpacts);
                if (clip != null) audioSource.PlayOneShot(clip);
                else audioSource.Play();
            }
            player.Services.Get<INoiseService>().Emit(new NoiseEvent(
                transform.position, radius, 2f, NoiseType.Environmental, this));
        }
    }
}
