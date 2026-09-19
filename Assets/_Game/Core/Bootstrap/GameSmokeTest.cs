using System.Collections;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Deterministic in-player qualification run, enabled only by the
    /// <c>-gameSmokeTest</c> command-line switch. It exercises the parts a boot
    /// soak cannot: real scene transitions through the SceneReady handshake,
    /// the progression chain driven through the domain commands, and a
    /// save/load round-trip — then prints a PASS/FAIL marker and exits with a
    /// status code so CI can gate on it.
    ///
    /// Nothing here runs in a normal build: the switch is absent, so the hook
    /// returns immediately and no object is created.
    /// </summary>
    public sealed class GameSmokeTest : MonoBehaviour
    {
        public const string Switch = "-gameSmokeTest";
        public const string PassMarker = "SMOKE PASS";
        public const string FailMarker = "SMOKE FAIL";

        private const float StepTimeout = 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MaybeStart()
        {
            if (!Requested()) return;
            var go = new GameObject("GameSmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<GameSmokeTest>();
        }

        private static bool Requested()
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg == Switch) return true;
            return false;
        }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            string failure = null;

            yield return WaitUntil(() => GameRoot.Instance != null);
            if (GameRoot.Instance == null) { Finish("GameRoot never appeared"); yield break; }

            var services = GameRoot.Instance.Services;
            var scenes = services.Get<ISceneService>();
            var state = services.Get<IGameStateService>();
            var dispatcher = services.Get<IGameCommandDispatcher>();
            var content = services.Get<IContentDatabase>();
            var saves = services.Get<ISaveService>();
            var events = services.Get<IGameEventBus>();

            // A transition that fails its readiness contract must fail the run.
            bool transitionFailed = false;
            System.Action<SceneTransitionFailedEvent> onFailed = e => transitionFailed = true;
            events.Subscribe(onFailed);
            try
            {
                // 1. Every gameplay scene must load and signal readiness.
                foreach (var id in new[] { Data.SceneId.Dock, Data.SceneId.ServiceEntrance,
                         Data.SceneId.MansionOffice, Data.SceneId.SecurityWing,
                         Data.SceneId.BunkerServerRoom, Data.SceneId.BroadcastTower })
                {
                    scenes.LoadScene(id, "default");
                    yield return WaitUntil(() => !scenes.IsTransitioning);
                    if (scenes.IsTransitioning) { failure = $"transition to '{id}' never completed"; break; }

                    if (transitionFailed)
                    {
                        failure = $"'{id}' never signalled SceneReady";
                        break;
                    }
                    if (state.State.SceneId != id)
                    {
                        failure = $"scene state is '{state.State.SceneId}' after loading '{id}'";
                        break;
                    }
                }

                // 2. The progression chain, through the domain commands. Same
                //    shape as the EditMode graph validator, but against the
                //    real services inside the built player.
                if (failure == null)
                {
                    foreach (var ev in content.Evidence)
                        dispatcher.Dispatch(new CollectEvidenceCommand(ev.Id, "smoke"));

                    // Complete everything the critical path requires, to a
                    // fixpoint: an objective whose prerequisites are not yet
                    // complete is refused, so one pass in content order would
                    // leave gaps. Explicit objectives are not completable this
                    // way at all — they need the actions below.
                    bool progressed = true;
                    int guard = 0;
                    while (progressed && guard++ < 32)
                    {
                        progressed = false;
                        foreach (var obj in content.Objectives)
                        {
                            if (obj.Optional) continue;
                            if (state.State.CompletedObjectives.Contains(obj.Id)) continue;
                            dispatcher.Dispatch(new CompleteObjectiveCommand(obj.Id, "smoke"));
                            if (state.State.CompletedObjectives.Contains(obj.Id)) progressed = true;
                        }
                    }

                    foreach (var obj in content.Objectives)
                        if (!obj.Optional &&
                            obj.Completion != Data.ObjectiveCompletionMode.Explicit &&
                            !state.State.CompletedObjectives.Contains(obj.Id))
                        {
                            failure = $"objective '{obj.Id}' is not completable on the critical path";
                            break;
                        }
                }
                if (failure == null)
                {
                    dispatcher.Dispatch(new RouteBroadcastCommand("route_broadcast", "smoke"));
                    if (!state.State.BroadcastStarted) failure = "routing did not start the relay";
                }
                if (failure == null)
                {
                    dispatcher.Dispatch(new CompleteBroadcastCommand("broadcast_truth"));
                    if (!state.State.BroadcastCompleted) failure = "the transmission did not complete";
                    else if (!state.State.CompletedObjectives.Contains("broadcast_truth"))
                        failure = "the transmission objective was not completed by the domain";
                    else if (string.IsNullOrEmpty(state.State.EndingId))
                        failure = "the transmission produced no ending";
                }

                // 3. Save round-trip through the real storage layer. Loading
                //    reloads the scene, so let the transition settle.
                if (failure == null)
                {
                    const string slot = "smoke_slot";
                    if (!saves.Save(slot)) failure = "save failed";
                    else if (!saves.HasSave(slot)) failure = "the save is not discoverable";
                    else if (!saves.Load(slot)) failure = "load failed";
                    else
                    {
                        yield return WaitUntil(() => !scenes.IsTransitioning);
                        if (scenes.IsTransitioning)
                            failure = "the load's scene transition never completed";
                        else if (!state.State.BroadcastCompleted)
                            failure = "the loaded save lost the completed broadcast";
                    }
                    saves.Delete(slot);
                }
            }
            finally
            {
                events.Unsubscribe(onFailed);
            }

            Finish(failure);
        }

        private static IEnumerator WaitUntil(System.Func<bool> pred)
        {
            float t = 0f;
            while (!pred() && t < StepTimeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void Finish(string failure)
        {
            if (failure == null)
            {
                Debug.Log(PassMarker + " — scenes, progression and save round-trip verified.");
                Quit(0);
            }
            else
            {
                Debug.LogError($"{FailMarker} — {failure}");
                Quit(1);
            }
        }

        private static void Quit(int code)
        {
            Application.Quit(code);
#if UNITY_EDITOR
            // Quit is a request; in editor play mode it is ignored, so stop
            // the run rather than spinning.
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
