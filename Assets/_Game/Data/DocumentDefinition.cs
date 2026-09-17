using UnityEngine;

namespace Escape.Data
{
    /// <summary>
    /// Readable world document. Optionally linked to evidence collected on read.
    /// </summary>
    [CreateAssetMenu(menuName = "Escape/Document", fileName = "DOC_New")]
    public sealed class DocumentDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(6, 20)]
        public string Body;
        public EvidenceDefinition GrantsEvidence;
    }
}
