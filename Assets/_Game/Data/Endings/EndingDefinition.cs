using UnityEngine;

namespace Escape.Data
{
    [CreateAssetMenu(menuName = "Escape/Ending", fileName = "END_New")]
    public sealed class EndingDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(4, 12)]
        public string Description;
        [Tooltip("Minimum count of collected Primary-category evidence.")]
        public int MinPrimaryEvidence;
        public EvidenceDefinition[] RequiredEvidence = new EvidenceDefinition[0];
        public InsightDefinition[] RequiredInsights = new InsightDefinition[0];
        [Tooltip("Priority used when multiple endings qualify. Higher wins.")]
        public int Priority;
        public bool IsSecret;
    }
}
