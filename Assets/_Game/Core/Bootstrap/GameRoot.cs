using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape.Core
{
    /// <summary>
    /// Composition root. Lives in Bootstrap.unity, survives scene loads, and
    /// constructs/registers every service. This is the ONLY singleton — no
    /// scattering of static instances across the codebase.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        public GameServices Services { get; private set; }
        public CommandJournal Journal { get; private set; }

        private IDetectionService _detection;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Services = new GameServices();
            Journal = new CommandJournal();
            var dispatcher = new GameCommandDispatcher(Journal);
            var events = new GameEventBus();
            var content = ContentDatabase.Load();
            var state = new GameStateService();
            var objectives = new ObjectiveService(state, content, events);
            var insights = new InsightService(state, content, events, objectives);
            var evidence = new EvidenceService(state, content, events, insights, objectives);
            var endings = new EndingService(state, content);
            var world = new WorldService(state, events, endings);
            _detection = new DetectionService(state, content.Tuning, events, dispatcher);
            var noise = new NoiseService();
            var settings = new SettingsService();
            var audio = new AudioService(transform);
            var inputGate = new InputGate();
            var saveCoordinator = new SaveCoordinator();
            var saves = new SaveService(state, content, events, dispatcher,
                coordinator: saveCoordinator);
            var scenes = new SceneService(this, Services, state, content, events);

            Services.Register<IGameCommandDispatcher>(dispatcher);
            Services.Register<IGameEventBus>(events);
            Services.Register<IContentDatabase>(content);
            Services.Register<IGameStateService>(state);
            Services.Register<IObjectiveService>(objectives);
            Services.Register<IEvidenceService>(evidence);
            Services.Register<IWorldService>(world);
            Services.Register<IDetectionService>(_detection);
            Services.Register<INoiseService>(noise);
            Services.Register<ISettingsService>(settings);
            Services.Register<IAudioService>(audio);
            Services.Register<IInputGate>(inputGate);
            Services.Register<ISaveCoordinator>(saveCoordinator);
            Services.Register<ISaveService>(saves);
            Services.Register<ISceneService>(scenes);
            Services.Register(endings);

            // Master volume applies immediately and follows setting changes.
            AudioListener.volume = settings.MasterVolume;
            settings.Changed += () => AudioListener.volume = settings.MasterVolume;

            dispatcher.Register<CollectEvidenceCommand>(evidence);
            dispatcher.Register<ReadDocumentCommand>(evidence);
            dispatcher.Register<ActivateObjectiveCommand>(objectives);
            dispatcher.Register<CompleteObjectiveCommand>(objectives);
            dispatcher.Register<GainInsightCommand>(insights);
            dispatcher.Register<UnlockDoorCommand>(world);
            dispatcher.Register<DisableCameraCommand>(world);
            dispatcher.Register<UnlockTerminalCommand>(world);
            dispatcher.Register<SetAlertCommand>(world);
            dispatcher.Register<SetLockdownCommand>(world);
            dispatcher.Register<StartBroadcastCommand>(world);
            dispatcher.Register<CompleteBroadcastCommand>(world);
            dispatcher.Register<RecordTerminalUseCommand>(world);
            dispatcher.Register<ShowSystemMessageCommand>(world);
            dispatcher.Register<SaveGameCommand>(saves);
            dispatcher.Register<LoadGameCommand>(saves);
            dispatcher.Register<ChangeSceneCommand>(scenes);
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == "Bootstrap")
                Services.Get<ISceneService>().LoadScene(Data.SceneId.MainMenu);
        }

        private void Update()
        {
            _detection?.Tick(Time.deltaTime);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Services.Get<ISettingsService>().Persist();
        }

        private void OnApplicationQuit() =>
            Services.Get<ISettingsService>().Persist();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
