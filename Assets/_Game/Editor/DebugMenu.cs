using Escape.Core;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Play-mode debug commands — the Unity-native replacement for the web
    /// build's __ETE_TEST__ hooks.
    /// </summary>
    public static class DebugMenu
    {
        private static bool Ready => Application.isPlaying && GameRoot.Instance != null;
        private static IGameCommandDispatcher D => GameRoot.Instance.Services.Get<IGameCommandDispatcher>();
        private static IContentDatabase C => GameRoot.Instance.Services.Get<IContentDatabase>();

        [MenuItem("Tools/Escape the Elites/Debug/Give All Evidence")]
        private static void GiveAll()
        {
            if (!Ready) return;
            foreach (var e in C.Evidence)
                D.Dispatch(new CollectEvidenceCommand(e.Id, "debug"));
        }

        [MenuItem("Tools/Escape the Elites/Debug/Complete All Objectives")]
        private static void CompleteAll()
        {
            if (!Ready) return;
            foreach (var o in C.Objectives)
                D.Dispatch(new CompleteObjectiveCommand(o.Id, "debug"));
        }

        [MenuItem("Tools/Escape the Elites/Debug/Trigger Alert")]
        private static void Alert() { if (Ready) D.Dispatch(new SetAlertCommand(true, "debug")); }

        [MenuItem("Tools/Escape the Elites/Debug/Clear Alert")]
        private static void ClearAlert() { if (Ready) D.Dispatch(new SetAlertCommand(false, "debug")); }

        [MenuItem("Tools/Escape the Elites/Debug/Trigger Lockdown")]
        private static void Lockdown() { if (Ready) D.Dispatch(new SetLockdownCommand(true, "debug")); }

        [MenuItem("Tools/Escape the Elites/Debug/Evaluate Ending")]
        private static void Ending()
        {
            if (!Ready) return;
            var r = GameRoot.Instance.Services.Get<EndingService>().Evaluate();
            Debug.Log($"[Debug] Ending: {(r.Ending != null ? r.Ending.Id : "none")} | " +
                      $"transmitted={r.TransmittedEvidence.Count} missing={r.MissingEvidence.Count}");
        }

        [MenuItem("Tools/Escape the Elites/Debug/Dump Command Journal")]
        private static void Journal()
        {
            if (Ready) Debug.Log(GameRoot.Instance.Journal.Dump());
        }
    }
}
