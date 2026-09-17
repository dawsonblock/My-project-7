using UnityEngine;

namespace Escape.Data
{
    public enum EvidenceCategory
    {
        Primary,
        Corroborating,
        Contradictory,
        Operational,
        Hidden
    }

    [CreateAssetMenu(menuName = "Escape/Evidence", fileName = "EVD_New")]
    public sealed class EvidenceDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(3, 8)]
        public string Description;
        public EvidenceCategory Category;
        public EvidenceDefinition[] Corroborates = new EvidenceDefinition[0];
        public EvidenceDefinition[] Contradicts = new EvidenceDefinition[0];
        public bool RequiredForBroadcast;
        public int EndingWeight = 1;
    }
}
