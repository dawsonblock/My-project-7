using UnityEngine;

namespace Escape.Data
{
    /// <summary>
    /// An investigation conclusion unlocked when the player holds a specific
    /// combination of evidence. Insights can unlock objectives, terminal
    /// commands, broadcast options and better endings.
    /// </summary>
    [CreateAssetMenu(menuName = "Escape/Insight", fileName = "INS_New")]
    public sealed class InsightDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(2, 5)]
        public string Description;
        public EvidenceDefinition[] RequiredEvidence = new EvidenceDefinition[0];
        [Tooltip("Objective activated when this insight is gained (optional).")]
        public ObjectiveDefinition UnlocksObjective;
    }
}
