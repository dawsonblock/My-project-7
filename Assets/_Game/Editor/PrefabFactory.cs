using Escape.AI;
using Escape.Gameplay;
using Escape.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Escape.EditorTools
{
    /// <summary>
    /// Builds the game's core prefabs deterministically from code.
    /// Tools → Escape the Elites → Build Prefabs.
    /// </summary>
    public static class PrefabFactory
    {
        private const string PrefabDir = "Assets/_Game/Prefabs";

        [MenuItem("Tools/Escape the Elites/Build Prefabs")]
        public static void BuildAll()
        {
            Lure();
            Player();
            Door();
            Terminal();
            EvidencePickup();
            DocumentPickup();
            BroadcastConsole();
            SpawnPoint();
            HidingLocker();
            Distraction();
            SecurityCameraPrefab();
            Guard();
            GameUi();
            AssetDatabase.SaveAssets();
            // NOTE: prefab files must NOT be fileID-normalized — scenes
            // reference prefab internals via {fileID, guid} pairs, so
            // renumbering corrupts every prefab instance reference.
            Debug.Log("[PrefabFactory] All prefabs built.");
        }

        private static GameObject Save(string sub, string name, GameObject go)
        {
            Blockout.EnsureFolder($"{PrefabDir}/{sub}");
            var path = $"{PrefabDir}/{sub}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        // ---------- Player ----------

        public static GameObject Player()
        {
            var go = new GameObject("Player");
            go.layer = LayerMask.NameToLayer(GameLayers.PlayerName);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.95f, 0);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            var input = go.AddComponent<PlayerInputReader>();
            Blockout.Set(input, "actions",
                Blockout.LoadAsset<InputActionAsset>(
                    "Assets/_Game/Gameplay/Player/PlayerInputActions.inputactions"));
            go.AddComponent<PlayerMovement>();
            go.AddComponent<PlayerState>();
            go.AddComponent<PlayerInteraction>();
            go.AddComponent<PlayerNoiseEmitter>();
            go.AddComponent<PlayerVisibility>();
            var thrower = go.AddComponent<PlayerThrower>();
            Blockout.Set(thrower, "lurePrefab",
                Blockout.LoadAsset<GameObject>($"{PrefabDir}/Gameplay/Lure.prefab"));
            var whistle = go.AddComponent<PlayerWhistle>();
            var wAudio = go.AddComponent<AudioSource>();
            wAudio.spatialBlend = 0f;
            wAudio.playOnAwake = false;
            Blockout.Set(whistle, "audioSource", wAudio);
            go.AddComponent<PlayerSaveParticipant>();

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(go.transform, false);
            pivot.transform.localPosition = new Vector3(0, 1.62f, 0);
            pivot.AddComponent<PlayerLook>();
            pivot.AddComponent<FlashlightController>();

            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(pivot.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;

            var torch = new GameObject("Flashlight");
            torch.transform.SetParent(pivot.transform, false);
            torch.transform.localPosition = new Vector3(0.25f, -0.15f, 0.1f);
            var light = torch.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 55f;
            light.innerSpotAngle = 30f;
            light.range = 14f;
            light.intensity = 4f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.shadows = LightShadows.Soft;
            light.enabled = false;
            var fc = pivot.GetComponent<FlashlightController>();
            Blockout.Set(fc, "flashlight", light);

            return Save("Gameplay", "Player", go);
        }

        // ---------- Door ----------

        public static GameObject Door()
        {
            var go = new GameObject("Door");
            var ctrl = go.AddComponent<DoorController>();
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = new Vector3(1.4f, 2.4f, 0.4f);
            obstacle.center = new Vector3(0, 1.2f, 0);
            var audio = go.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.playOnAwake = false;

            var panel = Blockout.Box(go.transform, "Panel", Vector3.zero,
                new Vector3(1.3f, 2.4f, 0.12f),
                Blockout.Mat("MAT_Metal_Dark", new Color(0.16f, 0.18f, 0.22f), 0.6f, 0.5f),
                GameLayers.InteractableName);
            panel.transform.localPosition = new Vector3(0, 1.2f, 0);
            var interact = panel.AddComponent<DoorInteractable>();
            Blockout.Set(interact, "door", ctrl);

            Blockout.Set(ctrl, "panel", panel.transform);
            Blockout.Set(ctrl, "openOffset", new Vector3(0, 2.35f, 0));
            Blockout.Set(ctrl, "audioSource", audio);
            return Save("Gameplay", "Door", go);
        }

        // ---------- Terminal ----------

        public static GameObject Terminal()
        {
            var go = new GameObject("Terminal");
            var body = Blockout.Box(go.transform, "Body", Vector3.zero,
                new Vector3(0.7f, 1.2f, 0.35f),
                Blockout.Mat("MAT_Metal_Dark", new Color(0.16f, 0.18f, 0.22f), 0.6f, 0.5f),
                GameLayers.InteractableName);
            body.transform.localPosition = new Vector3(0, 0.6f, 0);
            var screen = Blockout.Box(go.transform, "Screen", Vector3.zero,
                new Vector3(0.55f, 0.4f, 0.03f),
                Blockout.Mat("MAT_Screen", new Color(0.1f, 0.7f, 0.4f), 0f, 0.6f, true), null);
            screen.transform.localPosition = new Vector3(0, 1.05f, 0.19f);
            go.AddComponent<TerminalInteractable>();
            return Save("Gameplay", "Terminal", go);
        }

        // ---------- Pickups ----------

        public static GameObject EvidencePickup()
        {
            var go = Blockout.Box(null, "EvidencePickup", Vector3.zero,
                new Vector3(0.18f, 0.12f, 0.25f),
                Blockout.Mat("MAT_Keycard", new Color(0.85f, 0.7f, 0.15f), 0.2f, 0.7f),
                GameLayers.InteractableName);
            go.AddComponent<EvidenceInteractable>();
            return Save("Gameplay", "EvidencePickup", go);
        }

        public static GameObject DocumentPickup()
        {
            var go = Blockout.Box(null, "DocumentPickup", Vector3.zero,
                new Vector3(0.3f, 0.02f, 0.4f),
                Blockout.Mat("MAT_Paper", new Color(0.85f, 0.83f, 0.72f), 0f, 0.2f),
                GameLayers.InteractableName);
            go.AddComponent<DocumentInteractable>();
            return Save("Gameplay", "DocumentPickup", go);
        }

        public static GameObject BroadcastConsole()
        {
            var go = new GameObject("BroadcastConsole");
            var desk = Blockout.Box(go.transform, "Desk", Vector3.zero,
                new Vector3(1.6f, 0.9f, 0.7f),
                Blockout.Mat("MAT_Metal_Dark", new Color(0.16f, 0.18f, 0.22f), 0.6f, 0.5f),
                GameLayers.InteractableName);
            desk.transform.localPosition = new Vector3(0, 0.45f, 0);
            var screen = Blockout.Box(go.transform, "Screen", Vector3.zero,
                new Vector3(1.2f, 0.7f, 0.05f),
                Blockout.Mat("MAT_Screen_Red", new Color(0.8f, 0.15f, 0.1f), 0f, 0.6f, true), null);
            screen.transform.localPosition = new Vector3(0, 1.4f, -0.15f);
            screen.transform.localRotation = Quaternion.Euler(-15f, 0, 0);
            var console = go.AddComponent<BroadcastConsoleInteractable>();
            Blockout.Set(console, "completesObjectiveId", "broadcast_truth");
            return Save("Gameplay", "BroadcastConsole", go);
        }

        public static GameObject SpawnPoint()
        {
            var go = new GameObject("SpawnPoint");
            go.AddComponent<PlayerSpawnPoint>();
            return Save("Gameplay", "SpawnPoint", go);
        }

        public static GameObject HidingLocker()
        {
            var go = new GameObject("HidingLocker");
            var body = Blockout.Box(go.transform, "Body", Vector3.zero,
                new Vector3(1.1f, 2.1f, 0.9f),
                Blockout.Mat("MAT_Locker", new Color(0.12f, 0.16f, 0.14f), 0.5f, 0.4f));
            body.transform.localPosition = new Vector3(0, 1.05f, 0);
            var zone = new GameObject("Zone", typeof(BoxCollider), typeof(HidingZone));
            zone.transform.SetParent(go.transform, false);
            zone.transform.localPosition = new Vector3(0, 1.1f, 0.1f);
            var bc = zone.GetComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1.0f, 2.0f, 0.8f);
            zone.layer = LayerMask.NameToLayer(GameLayers.HidingZoneName);
            return Save("Gameplay", "HidingLocker", go);
        }

        public static GameObject Distraction()
        {
            var go = Blockout.Box(null, "Distraction", Vector3.zero,
                new Vector3(0.4f, 0.5f, 0.2f),
                Blockout.Mat("MAT_Breaker", new Color(0.6f, 0.2f, 0.1f), 0.4f, 0.4f),
                GameLayers.InteractableName);
            var audio = go.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            var d = go.AddComponent<DistractionInteractable>();
            Blockout.Set(d, "audioSource", audio);
            Blockout.Set(d, "label", "Rattle breaker panel");
            return Save("Gameplay", "Distraction", go);
        }

        /// <summary>Throwable bottle — doubles as the scene lure pickup.</summary>
        public static GameObject Lure()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Lure";
            go.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
            go.GetComponent<MeshRenderer>().sharedMaterial =
                Blockout.Mat("MAT_Bottle", new Color(0.25f, 0.5f, 0.35f), 0.1f, 0.75f);
            int l = LayerMask.NameToLayer(GameLayers.InteractableName);
            if (l >= 0) go.layer = l;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            var audio = go.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.maxDistance = 12f;
            var lure = go.AddComponent<ThrowableLure>();
            Blockout.Set(lure, "audioSource", audio);
            return Save("Gameplay", "Lure", go);
        }

        // ---------- Security camera ----------

        public static GameObject SecurityCameraPrefab()
        {
            var go = new GameObject("SecurityCamera");
            int secLayer = LayerMask.NameToLayer(GameLayers.SecurityName);
            go.layer = secLayer;
            var device = go.AddComponent<SecurityCamera>();
            var audio = go.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.loop = true;
            audio.volume = 0.25f;
            audio.maxDistance = 8f;

            var mount = Blockout.Box(go.transform, "Mount", Vector3.zero,
                new Vector3(0.12f, 0.5f, 0.12f),
                Blockout.Mat("MAT_Metal_Dark", new Color(0.16f, 0.18f, 0.22f), 0.6f, 0.5f));
            mount.transform.localPosition = new Vector3(0, 0.25f, 0);

            var head = new GameObject("Head");
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0, -0.15f, 0);
            var housing = Blockout.Box(head.transform, "Housing", Vector3.zero,
                new Vector3(0.22f, 0.18f, 0.45f),
                Blockout.Mat("MAT_Metal_Dark", new Color(0.16f, 0.18f, 0.22f), 0.6f, 0.5f));
            housing.transform.localPosition = new Vector3(0, 0, 0.15f);

            var eye = new GameObject("Eye");
            eye.transform.SetParent(head.transform, false);
            eye.transform.localPosition = new Vector3(0, 0, 0.4f);

            var cone = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer), typeof(CameraConeRenderer));
            cone.transform.SetParent(head.transform, false);
            cone.transform.localPosition = new Vector3(0, 0, 0.4f);
            cone.GetComponent<MeshRenderer>().sharedMaterial = Blockout.VisionConeMat();
            cone.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("StatusLight");
            lightGo.transform.SetParent(head.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 0.12f, 0.3f);
            var sl = lightGo.AddComponent<Light>();
            sl.type = LightType.Point;
            sl.color = new Color(1f, 0.1f, 0.05f);
            sl.range = 3f;
            sl.intensity = 1.5f;
            sl.shadows = LightShadows.None;

            var motor = go.AddComponent<CameraMotor>();
            Blockout.Set(motor, "head", head.transform);
            var sensor = go.AddComponent<CameraSensor>();
            Blockout.Set(sensor, "eye", eye.transform);
            var coneR = cone.GetComponent<CameraConeRenderer>();
            Blockout.Set(coneR, "sensor", sensor);
            Blockout.Set(device, "motor", motor);
            Blockout.Set(device, "sensor", sensor);
            Blockout.Set(device, "cone", coneR);
            Blockout.Set(device, "statusLight", sl);
            Blockout.Set(device, "motorAudio", audio);
            return Save("Security", "SecurityCamera", go);
        }

        // ---------- Guard ----------

        public static GameObject Guard()
        {
            var go = new GameObject("Guard");
            go.layer = LayerMask.NameToLayer(GameLayers.GuardName);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0, 0.95f, 0);
            body.GetComponent<MeshRenderer>().sharedMaterial =
                Blockout.Mat("MAT_Guard", new Color(0.12f, 0.12f, 0.16f), 0.3f, 0.4f);

            var head = new GameObject("Head");
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0, 1.7f, 0);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.3f;

            go.AddComponent<GuardBrain>();
            var vision = go.AddComponent<GuardVision>();
            Blockout.Set(vision, "eye", head.transform);
            go.AddComponent<GuardHearing>();
            go.AddComponent<GuardAnimatorDriver>();

            // Floor-projected vision fan — makes patrol reads possible.
            var coneGo = new GameObject("VisionCone",
                typeof(MeshFilter), typeof(MeshRenderer), typeof(GuardVisionCone));
            coneGo.transform.SetParent(go.transform, false);
            coneGo.transform.localPosition = Vector3.zero;
            var coneMr = coneGo.GetComponent<MeshRenderer>();
            coneMr.sharedMaterial = Blockout.VisionConeMat();
            coneMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Blockout.Set(coneGo.GetComponent<GuardVisionCone>(), "brain",
                go.GetComponent<GuardBrain>());
            return Save("AI", "Guard", go);
        }

        // ---------- UI ----------

        public static GameObject GameUi()
        {
            var go = new GameObject("GameUI");
            go.AddComponent<GameUI>();
            return Save("UI", "GameUI", go);
        }
    }
}
