using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Rejects corrupt or poisoned saves before they can touch live state.
    /// Unknown content ids are the classic failure mode — they get dropped
    /// with a warning instead of silently corrupting progression.
    /// </summary>
    public static class SaveValidator
    {
        public static bool Validate(SaveData data, IContentDatabase content, List<string> errors)
        {
            if (data == null) { errors?.Add("Save is null."); return false; }
            if (data.version < 1 || data.version > SaveData.CurrentVersion)
            { errors?.Add($"Unsupported save version {data.version}."); return false; }
            if (!content.IsKnownScene(data.sceneId))
            { errors?.Add($"Unknown scene id '{data.sceneId}'."); return false; }
            if (data.player != null && (!IsFinite(data.player.Position) || !IsFinite(data.player.EulerRotation)))
            { errors?.Add("Player position contains non-finite values."); return false; }
            if (!float.IsFinite(data.detection) || data.detection < 0f || data.detection > 100f)
            { errors?.Add($"Detection value invalid: {data.detection}."); return false; }

            FilterUnknown(data.collectedEvidence, id => content.TryGetEvidence(id, out _), "evidence", errors);
            FilterUnknown(data.readDocuments, id => content.TryGetDocument(id, out _), "document", errors);
            FilterUnknown(data.completedObjectives, id => content.TryGetObjective(id, out _), "objective", errors);
            FilterUnknown(data.activeObjectives, id => content.TryGetObjective(id, out _), "objective", errors);
            FilterUnknown(data.gainedInsights, id => content.TryGetInsight(id, out _), "insight", errors);
            FilterUnknown(data.unlockedTerminals, id => content.TryGetTerminal(id, out _), "terminal", errors);
            // Doors/cameras are scene-authored ids; only null/empty is invalid.
            data.unlockedDoors?.RemoveAll(string.IsNullOrEmpty);
            data.disabledCameras?.RemoveAll(string.IsNullOrEmpty);
            data.usedTerminalCommands?.RemoveAll(string.IsNullOrEmpty);
            return true;
        }

        private static void FilterUnknown(List<string> ids, System.Func<string, bool> known, string kind, List<string> errors)
        {
            if (ids == null) return;
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(ids[i]) || !known(ids[i]))
                {
                    errors?.Add($"Dropped unknown {kind} id '{ids[i]}'.");
                    ids.RemoveAt(i);
                }
            }
        }

        private static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
