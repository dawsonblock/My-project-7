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
        // Broadcast contract ids. These live in content, but the broadcast
        // state machine is domain knowledge; the checks below are skipped when
        // the objective is absent from content, so a content rename degrades
        // to "no check" instead of corrupting a save.
        private const string RoutingObjectiveId = "route_broadcast";
        private const string TransmissionObjectiveId = "broadcast_truth";

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
                // HasPose is authoritative from v3. A file that carries a
                // position without the flag is repaired with the same
                // inference the v2→v3 migration used.
                if (!data.player.HasPose && data.player.Position != Vector3.zero)
                {
                    r.Warnings.Add("Pose recorded without HasPose — repaired.");
                    data.player.HasPose = true;
                    r.WasRepaired = true;
                }
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

            // Cross-field invariants — repairable contradictions in the
            // relationship between fields, not inside any single one.
            if (data.activeObjectives != null && data.completedObjectives != null)
            {
                for (int i = data.activeObjectives.Count - 1; i >= 0; i--)
                    if (data.completedObjectives.Contains(data.activeObjectives[i]))
                    {
                        r.Warnings.Add($"Dropped active objective '{data.activeObjectives[i]}' (also completed).");
                        data.activeObjectives.RemoveAt(i);
                        r.WasRepaired = true;
                    }
            }
            if (data.lures < 0)
            {
                r.Warnings.Add($"Clamped negative lure count {data.lures} to 0.");
                data.lures = 0;
                r.WasRepaired = true;
            }
            if (data.broadcastCompleted && !data.broadcastStarted)
            {
                r.Warnings.Add("BroadcastCompleted without BroadcastStarted — repaired.");
                data.broadcastStarted = true;
                r.WasRepaired = true;
            }
            if (!string.IsNullOrEmpty(data.endingId) &&
                !content.TryGetEnding(data.endingId, out _))
            {
                r.Warnings.Add($"Dropped unknown ending id '{data.endingId}'.");
                data.endingId = "";
                r.WasRepaired = true;
            }

            // Progression coherence. The broadcast flags describe a state
            // machine the command layer now enforces, so a save claiming a
            // later state must also claim everything that state implies.
            // Without this a poisoned save could re-enter the world with a
            // relay that was never routed, or an ending with no transmission.
            if (data.broadcastStarted && content.TryGetObjective(RoutingObjectiveId, out _))
                RequireCompleted(data, RoutingObjectiveId, "broadcastStarted", r);
            if (data.broadcastCompleted && content.TryGetObjective(TransmissionObjectiveId, out _))
                RequireCompleted(data, TransmissionObjectiveId, "broadcastCompleted", r);

            if (data.completedObjectives != null &&
                data.completedObjectives.Contains(TransmissionObjectiveId) &&
                !data.broadcastCompleted)
            {
                // The objective is the stronger claim — the transmission
                // happened, the flag just was not recorded.
                r.Warnings.Add("Transmission objective complete without BroadcastCompleted — repaired.");
                data.broadcastCompleted = true;
                data.broadcastStarted = true;
                r.WasRepaired = true;
            }

            if (!string.IsNullOrEmpty(data.endingId) && !data.broadcastCompleted)
            {
                r.Warnings.Add("Ending recorded without a completed broadcast — dropped.");
                data.endingId = "";
                r.WasRepaired = true;
            }

            return Done(r);
        }

        /// <summary>
        /// Restores a prerequisite objective that a later-state flag implies.
        /// Repairs upward, matching how BroadcastCompleted implies
        /// BroadcastStarted above.
        /// </summary>
        private static void RequireCompleted(SaveData data, string objectiveId, string because,
            SaveValidationResult r)
        {
            if (data.completedObjectives == null) data.completedObjectives = new List<string>();
            if (data.completedObjectives.Contains(objectiveId)) return;
            r.Warnings.Add($"Objective '{objectiveId}' missing though {because} is set — repaired.");
            data.completedObjectives.Add(objectiveId);
            data.activeObjectives?.Remove(objectiveId);
            r.WasRepaired = true;
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
