using UnityEngine;

namespace Escape.Data
{
    public enum TerminalActionType
    {
        ShowMessage,
        CollectEvidence,
        CompleteObjective,
        UnlockDoor,
        DisableCamera,
        SetLockdown,
        StartBroadcast,
        ListDoors,
        ListCameras,
        ListStatus
    }

    /// <summary>
    /// One selectable command on a terminal. Requirements are checked against
    /// GameState; on success the action is dispatched through the command
    /// dispatcher so terminal UI never mutates state directly.
    /// </summary>
    [System.Serializable]
    public sealed class TerminalCommandDefinition
    {
        public string Command;
        public string Label;
        [TextArea(1, 4)]
        public string SuccessText;
        [TextArea(1, 4)]
        public string FailureText;
        public EvidenceDefinition[] RequiredEvidence = new EvidenceDefinition[0];
        public ObjectiveDefinition[] RequiredObjectives = new ObjectiveDefinition[0];
        public InsightDefinition[] RequiredInsights = new InsightDefinition[0];
        public TerminalActionType Action = TerminalActionType.ShowMessage;
        [Tooltip("Target id for the action: evidence id, door id, camera id, objective id.")]
        public string TargetId;
        [Tooltip("Objective completed when this command runs successfully.")]
        public ObjectiveDefinition CompletesObjective;
        [Tooltip("Hide this command until its requirements are met.")]
        public bool HideUntilUnlocked;
    }

    [CreateAssetMenu(menuName = "Escape/Terminal", fileName = "TRM_New")]
    public sealed class TerminalDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [Tooltip("Code the player must enter. Empty means unlocked by default.")]
        public string UnlockCode;
        [Tooltip("Objective that must be complete before this terminal can be used.")]
        public ObjectiveDefinition RequiredObjective;
        public TerminalCommandDefinition[] Commands = new TerminalCommandDefinition[0];
        public string[] AssociatedDoorIds = new string[0];
        public string[] AssociatedCameraIds = new string[0];
    }
}
