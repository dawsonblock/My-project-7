using UnityEngine;

namespace Escape.Data
{
    /// <summary>
    /// How an objective is satisfied. Activation is always gated by
    /// RequiredObjectives; completion is separate so that "what makes an
    /// objective available" never gets conflated with "what finishes it".
    /// </summary>
    public enum ObjectiveCompletionMode
    {
        /// <summary>Completes automatically once all RequiredEvidence is collected.</summary>
        Evidence = 0,
        /// <summary>Completes only via an explicit CompleteObjectiveCommand
        /// (terminal action, scene entry, interactable).</summary>
        Explicit = 1
    }

    [CreateAssetMenu(menuName = "Escape/Objective", fileName = "OBJ_New")]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(2, 5)]
        public string Description;
        public ObjectiveDefinition[] RequiredObjectives = new ObjectiveDefinition[0];
        public EvidenceDefinition[] RequiredEvidence = new EvidenceDefinition[0];
        public ObjectiveCompletionMode Completion = ObjectiveCompletionMode.Evidence;
        public bool Optional;
    }
}
