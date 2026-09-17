using UnityEngine;

namespace Escape.Data
{
    [CreateAssetMenu(menuName = "Escape/Objective", fileName = "OBJ_New")]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(2, 5)]
        public string Description;
        public ObjectiveDefinition[] RequiredObjectives = new ObjectiveDefinition[0];
        public EvidenceDefinition[] RequiredEvidence = new EvidenceDefinition[0];
        public bool Optional;
    }
}
