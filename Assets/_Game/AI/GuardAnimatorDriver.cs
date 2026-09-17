using UnityEngine;
using UnityEngine.AI;

namespace Escape.AI
{
    /// <summary>
    /// Drives Animator params from AI state. Animation never drives AI.
    /// </summary>
    public sealed class GuardAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private GuardBrain brain;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (brain == null) brain = GetComponent<GuardBrain>();
        }

        private void Update()
        {
            if (animator == null || agent == null) return;
            animator.SetFloat("Speed", agent.velocity.magnitude);
            animator.SetInteger("State", (int)brain.State);
        }
    }
}
