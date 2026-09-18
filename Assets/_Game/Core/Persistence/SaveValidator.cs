using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Rejects corrupt or poisoned saves before they can touch live state.
    /// Fatal problems (non-finite pose, unknown scene, future schema) fail
    /// the load; repairable ones (unknown ids, duplicates, empties) are
    /// dropped, logged as warnings, and mark the result WasRepaired.
    /// </summary>
    public static class SaveValidator
    {
        public static SaveValidationResult Validate(SaveData data, IContentDatabase content)
        {
            var r = new SaveValidationResult();
            if (data == null) { r.Errors.Add("Save is null."); return Done(r); }
            if (data.version < 1 || data.version > SaveData.CurrentVersion)
                r.Errors.Add($"Unsupported save version {data.version}.");
            if (!content.IsKnownScene(data.sceneId))
                r.Errors.Add($"Unknown scene id '{data.sceneId}'.");
            if (data.player != null)
            {
                if (!IsFinite(data.player.Position) ||
                    !float.IsFinite(data.player.Yaw) || !float.IsFinite(data.player.Pitch))
                    r.Errors.Add("Player pose contains non-finite values.");
            }
            if (!float.IsFinite(data.detection) || data.detection < 0f || data.detection > 100f)
                r.Errors.Add($"Detection value invalid: {data.detection}.");

            FilterUnknown(data.collectedEvidence, id => content.TryGetEvidence(id, out _), "evidence", r);
            FilterUnknown(data.readDocuments, id => content.TryGetDocument(id, out _), "document", r);
            FilterUnknown(data.completedObjectives, id => content.TryGetObjective(id, out _), "objective", r);
            FilterUnknown(data.activeObjectives, id => content.TryGetObjective(id, out _), "objective", r);
            FilterUnknown(data.gainedInsights, id => content.TryGetInsight(id, out _), "insight", r);
            FilterUnknown(data.unlockedTerminals, id => content.TryGetTerminal(id, out _), "terminal", r);
            // Doors/cameras/lures/terminal-commands are scene-authored ids;
            // only null/empty entries and duplicates are invalid.
            CleanSet(data.unlockedDoors, "door", r);
            CleanSet(data.disabledCameras, "camera", r);
            CleanSet(data.usedTerminalCommands, "terminal command", r);
            CleanSet(data.collectedLures, "lure", r);
            return Done(r);
        }

        private static SaveValidationResult Done(SaveValidationResult r)
        {
            r.IsValid = r.Errors.Count == 0;
            return r;
        }

        private static void FilterUnknown(List<string> ids, System.Func<string, bool> known,
            string kind, SaveValidationResult r)
        {
            if (ids == null) return;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                var id = ids[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id) || !known(id))
                {
                    r.Warnings.Add($"Dropped {kind} id '{id}'" +
                        (string.IsNullOrEmpty(id) ? " (empty)." :
                         !known(id) ? " (unknown)." : " (duplicate)."));
                    ids.RemoveAt(i);
                    r.WasRepaired = true;
                }
            }
        }

        private static void CleanSet(List<string> ids, string kind, SaveValidationResult r)
        {
            if (ids == null) return;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = ids.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(ids[i]) || !seen.Add(ids[i]))
                {
                    r.Warnings.Add($"Dropped {kind} id '{ids[i]}'.");
                    ids.RemoveAt(i);
                    r.WasRepaired = true;
                }
        }

        private static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
