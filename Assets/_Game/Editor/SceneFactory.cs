using Escape.AI;
using Escape.Core;
using Escape.Gameplay;
using Escape.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Escape.EditorTools
{
    /// <summary>
    /// Generates the blockout scenes for the vertical slice:
    /// Bootstrap, MainMenu, Dock, ServiceEntrance, MansionOffice,
    /// SecurityWing, BunkerServerRoom, BroadcastTower.
    /// Tools → Escape the Elites → Build Scenes.
    /// </summary>
    public static class SceneFactory
    {
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string PrefabDir = "Assets/_Game/Prefabs";

        private static Material Concrete => Blockout.Mat("MAT_Concrete", new Color(0.22f, 0.23f, 0.26f));
        private static Material ConcreteDark => Blockout.Mat("MAT_Concrete_Dark", new Color(0.1f, 0.11f, 0.13f));
        private static Material Metal => Blockout.Mat("MAT_Metal_Dark", new Color(0.16f, 0.18f, 0.22f), 0.6f, 0.5f);
        private static Material Wood => Blockout.Mat("MAT_Wood", new Color(0.25f, 0.17f, 0.1f));
        private static Material Crate => Blockout.Mat("MAT_Crate", new Color(0.3f, 0.22f, 0.12f));
        private static Material OfficeFloor => Blockout.Mat("MAT_Carpet", new Color(0.2f, 0.12f, 0.1f));
        private static Material Water => Blockout.Mat("MAT_Water", new Color(0.02f, 0.05f, 0.09f), 0.2f, 0.9f);
        private static Material Fence => Blockout.Mat("MAT_Fence", new Color(0.08f, 0.1f, 0.09f), 0.4f);

        [MenuItem("Tools/Escape the Elites/Build Scenes")]
        public static void BuildAll()
        {
            Blockout.EnsureFolder(SceneDir);
            Bootstrap();
            MainMenu();
            Dock();
            ServiceEntrance();
            MansionOffice();
            SecurityWing();
            BunkerServerRoom();
            BroadcastTower();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneFactory] Scenes built.");
        }

        private static int _lureSeq;
        private static string _sceneKey = "scene";

        private static Scene NewScene()
        {
            _lureSeq = 0;
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void Save(Scene scene, string name)
        {
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/{name}.unity");
        }

        private static GameObject Spawn(string prefabName, Vector3 pos, Vector3 euler = default)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabDir}/{(prefabName == "Guard" ? "AI" : prefabName == "SecurityCamera" ? "Security" : prefabName == "GameUI" ? "UI" : "Gameplay")}/{prefabName}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            return go;
        }

        private static Transform EnvRoot(Scene scene)
        {
            var go = new GameObject("Environment");
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        private static void AddSceneBootstrap(Scene scene, string sceneId,
            string firstObjective = "", string completesObjective = "")
        {
            var go = new GameObject("SceneBootstrap");
            SceneManager.MoveGameObjectToScene(go, scene);
            var sb = go.AddComponent<SceneBootstrap>();
            Blockout.Set(sb, "sceneId", sceneId);
            Blockout.Set(sb, "firstObjectiveId", firstObjective);
            Blockout.Set(sb, "completesObjectiveId", completesObjective);
            go.AddComponent<CaughtSequence>();
        }

        private static void AddSpawn(Transform parent, string id, Vector3 pos, float yaw)
        {
            var go = new GameObject("Spawn_" + id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var sp = go.AddComponent<PlayerSpawnPoint>();
            sp.SpawnId = id;
        }

        private static void AddDocument(Transform parent, string docId, Vector3 pos)
        {
            var go = Spawn("DocumentPickup", pos);
            go.transform.SetParent(parent, true);
            var d = go.GetComponent<DocumentInteractable>();
            Blockout.Set(d, "document", Blockout.LoadDefinition<Escape.Data.DocumentDefinition>("DOC", docId));
        }

        private static void AddEvidence(Transform parent, string evId, Vector3 pos, string label = "")
        {
            var go = Spawn("EvidencePickup", pos);
            go.transform.SetParent(parent, true);
            var e = go.GetComponent<EvidenceInteractable>();
            Blockout.Set(e, "evidence", Blockout.LoadDefinition<Escape.Data.EvidenceDefinition>("EVD", evId));
            if (!string.IsNullOrEmpty(label)) Blockout.Set(e, "promptLabel", label);
        }

        private static void AddTerminal(Transform parent, string termId, Vector3 pos, float yaw)
        {
            var go = Spawn("Terminal", pos, new Vector3(0, yaw, 0));
            go.transform.SetParent(parent, true);
            var t = go.GetComponent<TerminalInteractable>();
            Blockout.Set(t, "terminal", Blockout.LoadDefinition<Escape.Data.TerminalDefinition>("TRM", termId));
        }

        private static void AddCamera(Transform parent, string camId, Vector3 pos, float yaw)
        {
            var go = Spawn("SecurityCamera", pos, new Vector3(0, yaw, 0));
            go.transform.SetParent(parent, true);
            Blockout.Set(go.GetComponent<SecurityCamera>(), "id", camId);
            Blockout.Set(go.GetComponent<CameraSensor>(), "sourceId", camId);
        }

        private static void AddDoor(Transform parent, string doorId, Vector3 pos, float yaw,
            DoorRequirementKind req, string reqId, string lockedText)
        {
            var go = Spawn("Door", pos, new Vector3(0, yaw, 0));
            go.transform.SetParent(parent, true);
            var d = go.GetComponent<DoorController>();
            Blockout.Set(d, "id", doorId);
            Blockout.Set(d, "requirement", req);
            Blockout.Set(d, "requirementId", reqId);
            Blockout.Set(d, "lockedText", lockedText);
        }

        private static void AddTransition(Transform parent, string targetScene, string spawnId,
            Vector3 pos, float yaw, string prompt, string completesObjective = "",
            string requiredObjective = "", string blockedText = "")
        {
            var go = Blockout.Box(parent, "Transition_" + targetScene, pos,
                new Vector3(1.6f, 2.4f, 0.4f),
                Blockout.Mat("MAT_Transition", new Color(0.05f, 0.2f, 0.12f), 0f, 0.5f, true),
                GameLayers.InteractableName);
            var t = go.AddComponent<SceneTransitionInteractable>();
            Blockout.Set(t, "targetSceneId", targetScene);
            Blockout.Set(t, "targetSpawnId", spawnId);
            Blockout.Set(t, "prompt", prompt);
            Blockout.Set(t, "completesObjectiveId", completesObjective);
            Blockout.Set(t, "requiredObjectiveId", requiredObjective);
            if (!string.IsNullOrEmpty(blockedText)) Blockout.Set(t, "blockedText", blockedText);
        }

        private static void AddLure(Transform parent, Vector3 pos)
        {
            var go = Spawn("Lure", pos);
            go.transform.SetParent(parent, true);
            Blockout.Set(go.GetComponent<ThrowableLure>(), "lureId",
                $"lure_{_sceneKey}_{_lureSeq++}");
        }

        private static void AddLocker(Transform parent, Vector3 pos, float yaw)
        {
            var go = Spawn("HidingLocker", pos, new Vector3(0, yaw, 0));
            go.transform.SetParent(parent, true);
        }

        private static void AddDistraction(Transform parent, Vector3 pos, float yaw, string label)
        {
            var go = Spawn("Distraction", pos, new Vector3(0, yaw, 0));
            go.transform.SetParent(parent, true);
            Blockout.Set(go.GetComponent<DistractionInteractable>(), "label", label);
        }

        private static void AddLightingZone(Transform parent, Vector3 pos, Vector3 size,
            PlayerVisibility.Exposure exposure)
        {
            var go = new GameObject("LightZone_" + exposure);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size;
            var zone = go.AddComponent<LightingZone>();
            zone.ExposureLevel = exposure;
            int l = LayerMask.NameToLayer(GameLayers.LightingZoneName);
            if (l >= 0) go.layer = l;
        }

        private static void AddLamp(Transform parent, Vector3 pos, Color color, float range,
            float intensity = 2.5f, LightType type = LightType.Point, bool shadows = false)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            if (type == LightType.Spot) l.spotAngle = 60f;
            // Most lamps are shadowless fill — only key lights cast shadows,
            // otherwise the punctual-light atlas overflows and logs warnings.
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static void AddGuard(Transform parent, Vector3 pos, params (Vector3 p, float wait)[] waypoints)
        {
            var go = Spawn("Guard", pos);
            go.transform.SetParent(parent, true);
            var routeGo = new GameObject("PatrolRoute");
            routeGo.transform.SetParent(parent, false);
            var route = routeGo.AddComponent<PatrolRoute>();
            var wps = new PatrolRoute.Waypoint[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                var wp = new GameObject($"WP{i}");
                wp.transform.SetParent(routeGo.transform, false);
                wp.transform.position = waypoints[i].p;
                wps[i] = new PatrolRoute.Waypoint { Point = wp.transform, WaitSeconds = waypoints[i].wait };
            }
            route.Waypoints = wps;
            Blockout.Set(go.GetComponent<GuardBrain>(), "route", route);
            EditorUtility.SetDirty(go);
        }

        private static void BakeNav(Transform envRoot, Scene scene, string sceneAssetName)
        {
            var surface = envRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            int geo = LayerMask.NameToLayer(GameLayers.WorldGeometryName);
            surface.layerMask = geo >= 0 ? (LayerMask)(1 << geo) : (LayerMask)(-1);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // Persist the baked data as an asset. Left scene-embedded, the
            // NavMeshData object forces the whole .unity file into binary
            // serialization — breaking text serialization and diffability.
            var data = surface.navMeshData;
            if (data != null && !EditorUtility.IsPersistent(data))
            {
                Blockout.EnsureFolder($"{SceneDir}/NavMesh");
                AssetDatabase.CreateAsset(data, $"{SceneDir}/NavMesh/{sceneAssetName}.asset");
            }
        }

        // ==================== SCENES ====================

        private static void Bootstrap()
        {
            var scene = NewScene();
            var root = new GameObject("GameRoot");
            root.AddComponent<GameRoot>();
            Save(scene, "Bootstrap");
        }

        private static void MainMenu()
        {
            var scene = NewScene();
            var cam = new GameObject("MenuCamera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>().backgroundColor = new Color(0.01f, 0.02f, 0.02f);
            var go = new GameObject("MainMenu");
            go.AddComponent<MainMenuUI>();
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.01f, 0.02f, 0.02f);
            Save(scene, "MainMenu");
        }

        private static void Dock()
        {
            var scene = NewScene();
            _sceneKey = "dock";
            var env = EnvRoot(scene);
            var geo = GameLayers.WorldGeometryName;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.04f, 0.07f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.08f, 0.12f);

            var moon = new GameObject("Moon");
            moon.transform.SetParent(env, false);
            var dl = moon.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(0.5f, 0.65f, 0.85f);
            dl.intensity = 0.35f;
            moon.transform.rotation = Quaternion.Euler(40f, -30f, 0);

            // Pier + water
            Blockout.Box(env, "Pier", new Vector3(0, -0.5f, 0), new Vector3(36, 1, 18), Concrete, geo);
            Blockout.Box(env, "Water", new Vector3(0, -1.3f, 0), new Vector3(200, 0.4f, 200), Water, geo, false);

            // Perimeter fence (gap at gate z=+8)
            Blockout.Box(env, "FenceW", new Vector3(-17.5f, 1.5f, 0), new Vector3(0.3f, 3f, 18), Fence, geo);
            Blockout.Box(env, "FenceE", new Vector3(17.5f, 1.5f, 0), new Vector3(0.3f, 3f, 18), Fence, geo);
            Blockout.Box(env, "FenceN1", new Vector3(-9.5f, 1.5f, 8.5f), new Vector3(16, 3f, 0.3f), Fence, geo);
            Blockout.Box(env, "FenceN2", new Vector3(9.5f, 1.5f, 8.5f), new Vector3(16, 3f, 0.3f), Fence, geo);
            Blockout.Box(env, "GateLintel", new Vector3(0, 2.8f, 8.5f), new Vector3(3.4f, 0.4f, 0.3f), Metal, geo);
            Blockout.Box(env, "FenceS", new Vector3(0, 1.5f, -8.5f), new Vector3(36, 3f, 0.3f), Fence, geo);

            // Crates — cover between the lamps
            Blockout.Box(env, "CrateA", new Vector3(-5, 0.75f, -2), new Vector3(1.5f, 1.5f, 1.5f), Crate, geo);
            Blockout.Box(env, "CrateB", new Vector3(-3.6f, 0.75f, -0.8f), new Vector3(1.5f, 1.5f, 1.5f), Crate, geo);
            Blockout.Box(env, "CrateC", new Vector3(5, 0.75f, 3), new Vector3(1.5f, 1.5f, 1.5f), Crate, geo);
            Blockout.Box(env, "CrateD", new Vector3(6.4f, 0.5f, 2.2f), new Vector3(1.3f, 1f, 1.3f), Crate, geo);
            Blockout.Box(env, "CrateE", new Vector3(2, 0.6f, 5.5f), new Vector3(1.2f, 1.2f, 1.2f), Crate, geo);

            // Lamp posts with bright pools; the gaps stay dark
            foreach (var (x, z) in new[] { (-8f, -4f), (0f, 4f), (8f, -1f) })
            {
                Blockout.Box(env, "LampPost", new Vector3(x, 2, z), new Vector3(0.18f, 4, 0.18f), Metal, geo);
                AddLamp(env, new Vector3(x, 3.8f, z), new Color(0.6f, 0.75f, 1f), 11f, 3f, shadows: true);
                AddLightingZone(env, new Vector3(x, 1.5f, z), new Vector3(7, 3, 7), PlayerVisibility.Exposure.Bright);
            }
            AddLightingZone(env, new Vector3(-13, 1.5f, 0), new Vector3(9, 3, 18), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(13, 1.5f, 0), new Vector3(9, 3, 18), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(0, 1.5f, -6.5f), new Vector3(36, 3, 4), PlayerVisibility.Exposure.Dark);

            // First document + the dock camera that teaches LOS
            AddDocument(env, "doc_security_memo", new Vector3(-5, 1.55f, -2));
            AddCamera(env, "dock_cam_01", new Vector3(14, 3.2f, 7.8f), 235f);
            AddEvidence(env, "broadcast_key_001", new Vector3(6.4f, 1.15f, 2.2f), "Take Broadcast Key");

            // Bottles on the crates — teaches the throw verb early
            AddLure(env, new Vector3(-3.6f, 1.6f, -0.8f));
            AddLure(env, new Vector3(2, 1.35f, 5.5f));

            // Player + spawn + gate transition
            Spawn("Player", new Vector3(0, 0.1f, -6));
            AddSpawn(env, "default", new Vector3(0, 0.1f, -6), 0f);
            AddTransition(env, Escape.Data.SceneId.ServiceEntrance, "default",
                new Vector3(0, 1.2f, 8.3f), 0, "[E] Slip through the gate",
                completesObjective: "reach_compound");

            Spawn("GameUI", Vector3.zero);
            AddSceneBootstrap(scene, Escape.Data.SceneId.Dock, firstObjective: "reach_compound");
            Save(scene, "Dock");
        }

        private static void ServiceEntrance()
        {
            var scene = NewScene();
            _sceneKey = "service_entrance";
            var env = EnvRoot(scene);
            var geo = GameLayers.WorldGeometryName;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.04f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.07f);

            // Corridor z:0..24 (x -4..4), inner room z:26..34
            Blockout.Box(env, "Floor", new Vector3(0, -0.5f, 15), new Vector3(9, 1, 38), ConcreteDark, geo);
            Blockout.Box(env, "Ceiling", new Vector3(0, 3.4f, 15), new Vector3(9, 0.4f, 38), Concrete, geo, false);
            Blockout.Box(env, "WallW", new Vector3(-4.5f, 1.5f, 15), new Vector3(0.4f, 4, 38), Concrete, geo);
            Blockout.Box(env, "WallE", new Vector3(4.5f, 1.5f, 15), new Vector3(0.4f, 4, 38), Concrete, geo);
            Blockout.Box(env, "WallS", new Vector3(0, 1.5f, -3.5f), new Vector3(9, 4, 0.4f), Concrete, geo);
            // End wall with doorway at x≈0 (door spans x -0.7..0.7)
            Blockout.Box(env, "WallN1", new Vector3(-2.9f, 1.5f, 25.2f), new Vector3(3.2f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "WallN2", new Vector3(2.9f, 1.5f, 25.2f), new Vector3(3.2f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "WallNTop", new Vector3(0, 3.3f, 25.2f), new Vector3(2.6f, 1.4f, 0.4f), Concrete, geo);
            // Inner room walls
            Blockout.Box(env, "RoomW", new Vector3(-4.5f, 1.5f, 30), new Vector3(0.4f, 4, 10), Concrete, geo);
            Blockout.Box(env, "RoomE", new Vector3(4.5f, 1.5f, 30), new Vector3(0.4f, 4, 10), Concrete, geo);
            Blockout.Box(env, "RoomN", new Vector3(0, 1.5f, 34.5f), new Vector3(9, 4, 0.4f), Concrete, geo);

            // Entry vestibule: shields the spawn point from corridor sightlines.
            // Player enters a walled pocket (x -4.5..-1.5, z -3.5..0.5) and must
            // round the corner at z>0.5 where the guard patrol begins.
            Blockout.Box(env, "EntryScreenN", new Vector3(1.5f, 1.5f, 0.5f), new Vector3(6f, 3f, 0.4f), Concrete, geo);
            Blockout.Box(env, "EntryScreenW", new Vector3(-1.5f, 1.5f, -1.5f), new Vector3(0.4f, 3f, 4f), Concrete, geo);

            // Cover crates
            Blockout.Box(env, "CrateA", new Vector3(-2.5f, 0.75f, 6), new Vector3(1.5f, 1.5f, 1.5f), Crate, geo);
            Blockout.Box(env, "CrateB", new Vector3(-1f, 0.75f, 6.8f), new Vector3(1.5f, 1.5f, 1.5f), Crate, geo);
            Blockout.Box(env, "CrateC", new Vector3(2.5f, 0.75f, 14), new Vector3(1.5f, 1.5f, 1.5f), Crate, geo);
            Blockout.Box(env, "CrateD", new Vector3(-2.8f, 0.6f, 18f), new Vector3(1.2f, 1.2f, 1.2f), Crate, geo);
            Blockout.Box(env, "Shelf", new Vector3(-4f, 1f, 20), new Vector3(0.8f, 2f, 3f), Wood, geo);

            // Lockers (hiding)
            foreach (var z in new[] { 9f, 17f })
                AddLocker(env, new Vector3(-3.8f, 0, z), 90f);

            // A bottle by the crates and another in the back room
            AddLure(env, new Vector3(-1f, 1.6f, 6.8f));
            AddLure(env, new Vector3(2.5f, 1.05f, 31f));

            // Ceiling lights: dim corridor, red near door
            AddLamp(env, new Vector3(0, 3f, 4), new Color(0.5f, 0.7f, 0.6f), 10f, 1.6f);
            AddLamp(env, new Vector3(0, 3f, 14), new Color(0.5f, 0.7f, 0.6f), 10f, 1.6f);
            AddLamp(env, new Vector3(0, 3f, 24), new Color(1f, 0.25f, 0.15f), 9f, 2.2f, shadows: true);
            AddLightingZone(env, new Vector3(0, 1.5f, 14), new Vector3(9, 3, 22), PlayerVisibility.Exposure.Dim);
            AddLightingZone(env, new Vector3(-3.5f, 1.5f, 13), new Vector3(2.5f, 3, 18), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(0, 1.5f, 30), new Vector3(9, 3, 9), PlayerVisibility.Exposure.Dim);

            // Camera covering mid-corridor
            AddCamera(env, "service_cam_01", new Vector3(4.1f, 2.9f, 12), 250f);

            // Guard patrols the corridor
            AddGuard(env, new Vector3(0, 0.1f, 4),
                (new Vector3(0, 0.1f, 4), 2f),
                (new Vector3(0, 0.1f, 14), 1f),
                (new Vector3(0, 0.1f, 21), 3f));

            // Keycard on the shelf, terminal beside the door, breaker near entry
            AddEvidence(env, "keycard_staff", new Vector3(-4f, 1.15f, 20), "Take Staff Keycard");
            AddTerminal(env, "service_terminal", new Vector3(3.6f, 0, 24.3f), -90f);
            AddDistraction(env, new Vector3(3.9f, 1f, 2), -90f, "Rattle breaker panel");

            // The locked interior door + room behind with exit
            AddDoor(env, "service_security_door", new Vector3(0, 0, 25.2f), 0,
                DoorRequirementKind.RemoteOnly, "", "Locked — badge release required");
            Blockout.Box(env, "RoomDesk", new Vector3(2.5f, 0.5f, 31), new Vector3(2, 1, 0.9f), Wood, geo);
            AddDocument(env, "doc_ledger", new Vector3(-3.8f, 0.4f, 33f));
            AddTransition(env, Escape.Data.SceneId.MansionOffice, "default",
                new Vector3(0, 1.2f, 34f), 0, "[E] Enter the office wing");

            Spawn("Player", new Vector3(-3.5f, 0.1f, -2.2f));
            AddSpawn(env, "default", new Vector3(-3.5f, 0.1f, -2.2f), 40f);
            Spawn("GameUI", Vector3.zero);
            AddSceneBootstrap(scene, Escape.Data.SceneId.ServiceEntrance,
                completesObjective: "infiltrate_service");
            BakeNav(env, scene, "ServiceEntrance");
            Save(scene, "ServiceEntrance");
        }

        private static void MansionOffice()
        {
            var scene = NewScene();
            _sceneKey = "mansion_office";
            var env = EnvRoot(scene);
            var geo = GameLayers.WorldGeometryName;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.04f, 0.03f, 0.02f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.07f, 0.05f);

            // Room x:-9..9 z:-6..8, entry hall z:-8..-6
            Blockout.Box(env, "Floor", new Vector3(0, -0.5f, 0), new Vector3(19, 1, 17), OfficeFloor, geo);
            Blockout.Box(env, "Ceiling", new Vector3(0, 3.4f, 0), new Vector3(19, 0.4f, 17), Wood, geo, false);
            Blockout.Box(env, "WallW", new Vector3(-9.5f, 1.5f, 0), new Vector3(0.4f, 4, 17), Wood, geo);
            // East wall has a doorway to the security wing at z≈4.5
            Blockout.Box(env, "WallE1", new Vector3(9.5f, 1.5f, -2.5f), new Vector3(0.4f, 4, 12), Wood, geo);
            Blockout.Box(env, "WallE2", new Vector3(9.5f, 1.5f, 7f), new Vector3(0.4f, 4, 3), Wood, geo);
            Blockout.Box(env, "LintelE", new Vector3(9.5f, 3.3f, 4.5f), new Vector3(0.4f, 1.4f, 2), Wood, geo);
            Blockout.Box(env, "WallN", new Vector3(0, 1.5f, 8.5f), new Vector3(19, 4, 0.4f), Wood, geo);
            Blockout.Box(env, "WallS1", new Vector3(-5.5f, 1.5f, -6.5f), new Vector3(8, 4, 0.4f), Wood, geo);
            Blockout.Box(env, "WallS2", new Vector3(5.5f, 1.5f, -6.5f), new Vector3(8, 4, 0.4f), Wood, geo);
            // Entry hall walls (arrival corridor from service wing)
            Blockout.Box(env, "HallW", new Vector3(-1.7f, 1.5f, -8), new Vector3(0.3f, 4, 3.4f), Wood, geo);
            Blockout.Box(env, "HallE", new Vector3(1.7f, 1.5f, -8), new Vector3(0.3f, 4, 3.4f), Wood, geo);

            // Desk + furniture
            Blockout.Box(env, "Desk", new Vector3(0, 0.45f, 3), new Vector3(2.4f, 0.9f, 1.1f), Wood, geo);
            Blockout.Box(env, "DeskTop", new Vector3(0, 0.93f, 3), new Vector3(2.6f, 0.06f, 1.3f),
                Blockout.Mat("MAT_DeskTop", new Color(0.35f, 0.22f, 0.12f), 0f, 0.6f), geo);
            Blockout.Box(env, "ShelfW", new Vector3(-9f, 1.2f, 2), new Vector3(0.8f, 2.4f, 4), Wood, geo);
            Blockout.Box(env, "Cabinet", new Vector3(-8.5f, 1f, -4.5f), new Vector3(1.6f, 2f, 1f), Metal, geo);
            Blockout.Box(env, "Printer", new Vector3(8f, 0.9f, -3), new Vector3(1f, 0.6f, 0.8f), Metal, geo);
            Blockout.Box(env, "TableS", new Vector3(8f, 0.45f, -3), new Vector3(1.4f, 0.9f, 1f), Wood, geo);
            Blockout.Box(env, "Safe", new Vector3(8.5f, 0.6f, 5), new Vector3(1.1f, 1.2f, 1f), Metal, geo);
            Blockout.Box(env, "Chair", new Vector3(0, 0.5f, 1.6f), new Vector3(0.7f, 1f, 0.7f), Crate, geo);

            // Warm interior lights
            AddLamp(env, new Vector3(0, 2.9f, 3), new Color(1f, 0.75f, 0.45f), 9f, 2.4f, shadows: true);
            AddLamp(env, new Vector3(-6, 2.9f, 0), new Color(1f, 0.8f, 0.5f), 8f, 1.6f);
            AddLamp(env, new Vector3(6, 2.9f, 0), new Color(1f, 0.8f, 0.5f), 8f, 1.6f);
            AddLamp(env, new Vector3(0, 2.5f, -8), new Color(0.5f, 0.6f, 0.7f), 7f, 1.2f);
            AddLightingZone(env, new Vector3(0, 1.5f, 3), new Vector3(8, 3, 7), PlayerVisibility.Exposure.Bright);
            AddLightingZone(env, new Vector3(0, 1.5f, 0), new Vector3(19, 3, 17), PlayerVisibility.Exposure.Normal);
            AddLightingZone(env, new Vector3(-8, 1.5f, -4.5f), new Vector3(3, 3, 4), PlayerVisibility.Exposure.Dark);

            // The paperwork — guest log on the desk, manifest on the shelf,
            // statement + access log nearby, code note under the desk,
            // ledger hidden behind the cabinet.
            AddDocument(env, "doc_guest_log", new Vector3(-0.5f, 1f, 3));
            AddDocument(env, "doc_public_statement", new Vector3(0.5f, 1f, 3.1f));
            AddDocument(env, "doc_archive_code", new Vector3(0, 0.55f, 2.4f));
            AddDocument(env, "doc_manifest", new Vector3(-9f, 1.5f, 2));
            AddDocument(env, "doc_access_log", new Vector3(8f, 1.25f, -3));

            // A decanter on the shelf and a loose grate by the entry hall
            AddLure(env, new Vector3(-9f, 1.9f, 3.2f));
            AddDistraction(env, new Vector3(-1.55f, 0.5f, -7f), 90f, "Kick loose vent grate");

            AddTerminal(env, "office_terminal", new Vector3(1.4f, 0.95f, 3), 180f);

            // Security wing doorway — sealed until the archive reveals the relay route.
            AddTransition(env, Escape.Data.SceneId.SecurityWing, "default",
                new Vector3(9.2f, 1.2f, 4.5f), 90f, "[E] Enter the security wing",
                requiredObjective: "download_archive",
                blockedText: "Sealed — the relay route isn't confirmed yet");

            // One guard drifts through the main room, keeping pressure on.
            // Patrol stays out of the entry hall (z < -6) so the spawn is safe.
            AddGuard(env, new Vector3(0, 0.1f, -1),
                (new Vector3(0, 0.1f, -1), 4f),
                (new Vector3(4, 0.1f, -4), 2f),
                (new Vector3(-4, 0.1f, 1), 3f));

            Spawn("Player", new Vector3(0, 0.1f, -8.5f));
            AddSpawn(env, "default", new Vector3(0, 0.1f, -8.5f), 0f);
            Spawn("GameUI", Vector3.zero);
            AddSceneBootstrap(scene, Escape.Data.SceneId.MansionOffice);
            BakeNav(env, scene, "MansionOffice");
            Save(scene, "MansionOffice");
        }

        private static void SecurityWing()
        {
            var scene = NewScene();
            _sceneKey = "security_wing";
            var env = EnvRoot(scene);
            var geo = GameLayers.WorldGeometryName;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.04f, 0.05f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.07f, 0.09f);

            var monitor = Blockout.Mat("MAT_Monitor", new Color(0.3f, 0.7f, 0.8f), 0f, 0.8f, true);

            // Hub floor x -9.5..9.5, z -4.5..14.5; bunker alcove z 14.5..18.5
            Blockout.Box(env, "Floor", new Vector3(0, -0.5f, 5), new Vector3(19, 1, 19), ConcreteDark, geo);
            Blockout.Box(env, "Ceiling", new Vector3(0, 3.4f, 5), new Vector3(19, 0.4f, 19), Concrete, geo, false);
            Blockout.Box(env, "WallW", new Vector3(-9.5f, 1.5f, 5), new Vector3(0.4f, 4, 19), Concrete, geo);
            Blockout.Box(env, "WallE", new Vector3(9.5f, 1.5f, 5), new Vector3(0.4f, 4, 19), Concrete, geo);
            // South wall with entry door at x≈-6.5 (gap x -7.4..-5.6)
            Blockout.Box(env, "WallS_W", new Vector3(-8.45f, 1.5f, -4.5f), new Vector3(2.1f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "WallS_E", new Vector3(1.95f, 1.5f, -4.5f), new Vector3(15.1f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "LintelS", new Vector3(-6.5f, 3.3f, -4.5f), new Vector3(1.8f, 1.4f, 0.4f), Concrete, geo);
            // North wall with sealed bunker door at x=0 (gap x -0.9..0.9)
            Blockout.Box(env, "WallN_W", new Vector3(-5.2f, 1.5f, 14.5f), new Vector3(8.6f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "WallN_E", new Vector3(5.2f, 1.5f, 14.5f), new Vector3(8.6f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "LintelN", new Vector3(0, 3.3f, 14.5f), new Vector3(1.8f, 1.4f, 0.4f), Concrete, geo);

            // Entry pocket: screen wall shields the spawn from the main room.
            Blockout.Box(env, "EntryScreen", new Vector3(-6.75f, 1.5f, -1.5f), new Vector3(5.5f, 3, 0.4f), Concrete, geo);

            // Monitor console rows — cover + the hub's reason to exist
            foreach (var z in new[] { 5f, 10f })
            {
                foreach (var x in new[] { -5f, -2.5f, 0f, 2.5f, 5f })
                {
                    Blockout.Box(env, "Console", new Vector3(x, 0.45f, z), new Vector3(1.8f, 0.9f, 0.8f), Metal, geo);
                    Blockout.Box(env, "Screen", new Vector3(x, 1.25f, z - 0.2f), new Vector3(0.9f, 0.6f, 0.08f), monitor, geo, false);
                }
            }
            // Hub desk under the north wall + the terminal that runs the wing
            Blockout.Box(env, "HubDesk", new Vector3(-2.5f, 0.45f, 13.4f), new Vector3(3f, 0.9f, 0.9f), Metal, geo);
            AddTerminal(env, "security_hub_terminal", new Vector3(-2.5f, 1.05f, 13.4f), 180f);

            // Bunker door + stairwell alcove behind the north wall
            AddDoor(env, "wing_bunker_door", new Vector3(0, 0, 14.5f), 0,
                DoorRequirementKind.RemoteOnly, "", "Sealed — release from the hub console");
            Blockout.Box(env, "AlcoveFloor", new Vector3(0, -0.5f, 16.5f), new Vector3(4.4f, 1, 4.4f), Metal, geo);
            Blockout.Box(env, "AlcoveW", new Vector3(-2f, 1.5f, 16.5f), new Vector3(0.4f, 4, 4.4f), Concrete, geo);
            Blockout.Box(env, "AlcoveE", new Vector3(2f, 1.5f, 16.5f), new Vector3(0.4f, 4, 4.4f), Concrete, geo);
            Blockout.Box(env, "AlcoveN", new Vector3(0, 1.5f, 18.5f), new Vector3(4.4f, 4, 0.4f), Concrete, geo);
            // Descending stair dressing inside the alcove
            for (int i = 0; i < 3; i++)
                Blockout.Box(env, "StepDown" + i, new Vector3(-1.2f, -0.15f - 0.3f * i, 15.6f + 0.6f * i),
                    new Vector3(1.2f, 0.3f, 0.6f), ConcreteDark, geo);
            AddTransition(env, Escape.Data.SceneId.BunkerServerRoom, "default",
                new Vector3(0, 1.2f, 17.8f), 0, "[E] Descend to the server bunker");

            // Cold monitor glow + a red threat lamp over the bunker door
            AddLamp(env, new Vector3(0, 2.8f, 5), new Color(0.4f, 0.8f, 0.9f), 11f, 1.7f);
            AddLamp(env, new Vector3(0, 2.8f, 10), new Color(0.4f, 0.8f, 0.9f), 11f, 1.7f);
            AddLamp(env, new Vector3(0, 2.8f, 13.5f), new Color(1f, 0.3f, 0.2f), 7f, 2f, shadows: true);
            AddLamp(env, new Vector3(-6.5f, 2.9f, -3f), new Color(0.5f, 0.65f, 0.7f), 6f, 1.1f);
            AddLightingZone(env, new Vector3(0, 1.5f, 7), new Vector3(19, 3, 17), PlayerVisibility.Exposure.Dim);
            AddLightingZone(env, new Vector3(-6.5f, 1.5f, -3f), new Vector3(6, 3, 3), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(-8.2f, 1.5f, 7f), new Vector3(2.6f, 3, 15), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(8.2f, 1.5f, 7f), new Vector3(2.6f, 3, 15), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(-2.5f, 1.5f, 13.4f), new Vector3(5, 3, 2.5f), PlayerVisibility.Exposure.Bright);

            // Two cameras sweep the aisles; both are cut at the hub terminal
            AddCamera(env, "wing_cam_01", new Vector3(-9.1f, 2.9f, 7), 140f);
            AddCamera(env, "wing_cam_02", new Vector3(9.1f, 2.9f, 2), 215f);

            // Stealth kit — lockers in the dark aisles, a bottle on a
            // console, a breaker to rattle near the spawn
            AddLocker(env, new Vector3(-8.4f, 0, 2.5f), 90f);
            AddLocker(env, new Vector3(8.4f, 0, 8f), -90f);
            AddLure(env, new Vector3(0, 1.0f, 5f));
            AddDistraction(env, new Vector3(9.05f, 1.2f, -3.5f), -90f, "Trip breaker panel");

            AddGuard(env, new Vector3(-7f, 0.1f, 1f),
                (new Vector3(-7f, 0.1f, 1f), 2f),
                (new Vector3(-7f, 0.1f, 9f), 1f),
                (new Vector3(-3f, 0.1f, 11.5f), 3f));
            AddGuard(env, new Vector3(7f, 0.1f, 11f),
                (new Vector3(7f, 0.1f, 11f), 3f),
                (new Vector3(7f, 0.1f, 1f), 1f),
                (new Vector3(3f, 0.1f, 4f), 2f));

            Spawn("Player", new Vector3(-6.5f, 0.1f, -3f));
            AddSpawn(env, "default", new Vector3(-6.5f, 0.1f, -3f), 30f);
            Spawn("GameUI", Vector3.zero);
            AddSceneBootstrap(scene, Escape.Data.SceneId.SecurityWing);
            BakeNav(env, scene, "SecurityWing");
            Save(scene, "SecurityWing");
        }

        private static void BunkerServerRoom()
        {
            var scene = NewScene();
            _sceneKey = "bunker_server_room";
            var env = EnvRoot(scene);
            var geo = GameLayers.WorldGeometryName;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.04f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.06f, 0.06f);

            var rackLed = Blockout.Mat("MAT_RackLed", new Color(0.2f, 0.9f, 0.4f), 0f, 0.7f, true);

            // Vault x -8.5..8.5, z -5.5..15.5; exit alcove z 15.5..19
            Blockout.Box(env, "Floor", new Vector3(0, -0.5f, 5), new Vector3(17, 1, 21), ConcreteDark, geo);
            Blockout.Box(env, "Ceiling", new Vector3(0, 2.9f, 5), new Vector3(17, 0.4f, 21), Concrete, geo, false);
            Blockout.Box(env, "WallW", new Vector3(-8.5f, 1.5f, 5), new Vector3(0.4f, 4, 21), Concrete, geo);
            Blockout.Box(env, "WallE", new Vector3(8.5f, 1.5f, 5), new Vector3(0.4f, 4, 21), Concrete, geo);
            Blockout.Box(env, "WallS", new Vector3(0, 1.5f, -5.5f), new Vector3(17, 4, 0.4f), Concrete, geo);
            // North wall with exit doorway at x=0
            Blockout.Box(env, "WallN_W", new Vector3(-4.7f, 1.5f, 15.5f), new Vector3(7.6f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "WallN_E", new Vector3(4.7f, 1.5f, 15.5f), new Vector3(7.6f, 4, 0.4f), Concrete, geo);
            Blockout.Box(env, "LintelN", new Vector3(0, 3.3f, 15.5f), new Vector3(1.8f, 1.4f, 0.4f), Concrete, geo);

            // Server rack rows at x -4/0/4 with a cross-aisle gap at z 5.5..7
            foreach (var x in new[] { -4f, 0f, 4f })
            {
                Blockout.Box(env, "Rack", new Vector3(x, 1.15f, 2.25f), new Vector3(1.2f, 2.3f, 6.5f), Metal, geo);
                Blockout.Box(env, "Rack", new Vector3(x, 1.15f, 10.25f), new Vector3(1.2f, 2.3f, 6.5f), Metal, geo);
                // LED strips down each rack face
                Blockout.Box(env, "Led", new Vector3(x + 0.62f, 1.2f, 2.25f), new Vector3(0.04f, 1.8f, 0.15f), rackLed, geo, false);
                Blockout.Box(env, "Led", new Vector3(x - 0.62f, 1.2f, 10.25f), new Vector3(0.04f, 1.8f, 0.15f), rackLed, geo, false);
            }
            // Stub rack shields the spawn from the vault interior
            Blockout.Box(env, "RackStub", new Vector3(0, 1.15f, -1.5f), new Vector3(1.2f, 2.3f, 1f), Metal, geo);

            // Exit alcove — stairwell up to the tower
            Blockout.Box(env, "UpAlcoveFloor", new Vector3(0, -0.5f, 17.25f), new Vector3(4.4f, 1, 3.9f), Metal, geo);
            Blockout.Box(env, "UpAlcoveW", new Vector3(-2f, 1.5f, 17.25f), new Vector3(0.4f, 4, 3.9f), Concrete, geo);
            Blockout.Box(env, "UpAlcoveE", new Vector3(2f, 1.5f, 17.25f), new Vector3(0.4f, 4, 3.9f), Concrete, geo);
            Blockout.Box(env, "UpAlcoveN", new Vector3(0, 1.5f, 19f), new Vector3(4.4f, 4, 0.4f), Concrete, geo);
            AddTransition(env, Escape.Data.SceneId.BroadcastTower, "default",
                new Vector3(0, 1.2f, 18.3f), 0, "[E] Climb to the broadcast tower");

            // Rack LEDs give the vault its glow; the exit lamp pulls the eye north
            AddLamp(env, new Vector3(-4, 2.5f, 6.25f), new Color(0.3f, 0.9f, 0.5f), 6f, 1f);
            AddLamp(env, new Vector3(4, 2.5f, 6.25f), new Color(0.3f, 0.9f, 0.5f), 6f, 1f);
            AddLamp(env, new Vector3(0, 2.5f, 1f), new Color(0.3f, 0.7f, 0.9f), 6f, 1f);
            AddLamp(env, new Vector3(0, 2.6f, 15f), new Color(1f, 0.35f, 0.2f), 8f, 1.8f, shadows: true);
            AddLightingZone(env, new Vector3(0, 1.5f, 5), new Vector3(17, 3, 21), PlayerVisibility.Exposure.Dim);
            AddLightingZone(env, new Vector3(-6, 1.5f, 5), new Vector3(2.8f, 3, 21), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(6, 1.5f, 5), new Vector3(2.8f, 3, 21), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(0, 1.5f, -3.5f), new Vector3(8, 3, 4), PlayerVisibility.Exposure.Dark);

            AddCamera(env, "bunker_cam_01", new Vector3(8.1f, 2.7f, 6.25f), 200f);

            AddGuard(env, new Vector3(-6f, 0.1f, 1f),
                (new Vector3(-6f, 0.1f, 1f), 2f),
                (new Vector3(-6f, 0.1f, 13.5f), 1f),
                (new Vector3(6f, 0.1f, 13.5f), 2f),
                (new Vector3(6f, 0.1f, 1f), 3f));

            // Bottles in the cross-aisle, a noisy fan on the west wall,
            // a locker tucked in the east dark strip
            AddLure(env, new Vector3(-2f, 0.1f, 6.2f));
            AddLure(env, new Vector3(2f, 0.1f, 13.8f));
            AddDistraction(env, new Vector3(-8.15f, 1.2f, 8f), 90f, "Jostle server fan");
            AddLocker(env, new Vector3(7.9f, 0, 13f), -90f);

            Spawn("Player", new Vector3(0, 0.1f, -3.5f));
            AddSpawn(env, "default", new Vector3(0, 0.1f, -3.5f), 0f);
            Spawn("GameUI", Vector3.zero);
            AddSceneBootstrap(scene, Escape.Data.SceneId.BunkerServerRoom,
                completesObjective: "reach_bunker");
            BakeNav(env, scene, "BunkerServerRoom");
            Save(scene, "BunkerServerRoom");
        }

        private static void BroadcastTower()
        {
            var scene = NewScene();
            _sceneKey = "broadcast_tower";
            var env = EnvRoot(scene);
            var geo = GameLayers.WorldGeometryName;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.04f, 0.07f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.04f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.12f);

            var moon = new GameObject("Moon");
            moon.transform.SetParent(env, false);
            var dl = moon.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(0.5f, 0.65f, 0.85f);
            dl.intensity = 0.3f;
            moon.transform.rotation = Quaternion.Euler(35f, -140f, 0);

            // Rooftop gantry x -6..6, z -5.5..9.5, open to the night sky
            Blockout.Box(env, "Deck", new Vector3(0, -0.5f, 2), new Vector3(12, 1, 15), ConcreteDark, geo);
            // Parapets — keep the player on the roof
            Blockout.Box(env, "ParapetS", new Vector3(0, 0.55f, -5.5f), new Vector3(12, 1.1f, 0.3f), Concrete, geo);
            Blockout.Box(env, "ParapetN", new Vector3(0, 0.55f, 9.5f), new Vector3(12, 1.1f, 0.3f), Concrete, geo);
            Blockout.Box(env, "ParapetW", new Vector3(-6f, 0.55f, 2), new Vector3(0.3f, 1.1f, 15), Concrete, geo);
            Blockout.Box(env, "ParapetE", new Vector3(6f, 0.55f, 2), new Vector3(0.3f, 1.1f, 15), Concrete, geo);

            // Mast core — the tower itself, also the big sight blocker
            Blockout.Box(env, "MastCore", new Vector3(4.5f, 4f, 7.5f), new Vector3(1.4f, 8, 1.4f), Metal, geo);
            Blockout.Box(env, "Crossarm", new Vector3(4.5f, 7.2f, 7.5f), new Vector3(5f, 0.15f, 0.15f), Metal, geo, false);
            AddLamp(env, new Vector3(4.5f, 8.2f, 7.5f), new Color(1f, 0.2f, 0.15f), 12f, 2.5f, shadows: true);

            // Stair run A: up the west wall to landing A (top y 2.4)
            for (int i = 0; i < 8; i++)
                Blockout.Box(env, "StepA" + i, new Vector3(-4.5f, 0.15f + 0.3f * i, -3.5f + 0.55f * i),
                    new Vector3(2f, 0.3f, 0.55f), Metal, geo);
            Blockout.Box(env, "LandingA", new Vector3(-3f, 2.25f, 2f), new Vector3(6f, 0.3f, 4.5f), Metal, geo);
            // Stair run B: switchback east along the north edge to the relay deck (top y 4.8)
            for (int i = 0; i < 8; i++)
                Blockout.Box(env, "StepB" + i, new Vector3(-0.5f + 0.55f * i, 2.55f + 0.3f * i, 3.5f),
                    new Vector3(0.55f, 0.3f, 2f), Metal, geo);
            Blockout.Box(env, "RelayDeck", new Vector3(4f, 4.65f, 5f), new Vector3(4.5f, 0.3f, 7f), Metal, geo);

            // Rails on the landing and deck so a sprint doesn't end in a fall
            Blockout.Box(env, "RailA_S", new Vector3(-3.5f, 3.3f, -0.25f), new Vector3(5f, 1.5f, 0.08f), Metal, geo);
            Blockout.Box(env, "RailA_W", new Vector3(-6f, 3.3f, 2f), new Vector3(0.08f, 1.5f, 4.5f), Metal, geo);
            Blockout.Box(env, "RailD_N", new Vector3(4f, 5.7f, 8.5f), new Vector3(4.5f, 1.8f, 0.08f), Metal, geo);
            Blockout.Box(env, "RailD_E", new Vector3(6.25f, 5.7f, 5f), new Vector3(0.08f, 1.8f, 7f), Metal, geo);
            Blockout.Box(env, "RailD_S", new Vector3(4f, 5.7f, 1.5f), new Vector3(4.5f, 1.8f, 0.08f), Metal, geo);

            // Cover on the ground floor
            Blockout.Box(env, "VentA", new Vector3(-2f, 0.75f, -4f), new Vector3(1.5f, 1.5f, 1.2f), Metal, geo);
            Blockout.Box(env, "VentB", new Vector3(2.5f, 0.9f, 1f), new Vector3(1.8f, 1.8f, 1.4f), Metal, geo);
            Blockout.Box(env, "CrateT", new Vector3(-4.5f, 0.6f, 6.5f), new Vector3(1.2f, 1.2f, 1.2f), Crate, geo);

            // The finale — relay console on the top deck
            var console = Spawn("BroadcastConsole", new Vector3(4f, 4.8f, 6.5f), new Vector3(0, -90, 0));
            console.transform.SetParent(env, true);

            // Ground guard + the tower camera the hub terminal can blind
            AddGuard(env, new Vector3(0, 0.1f, -3f),
                (new Vector3(0, 0.1f, -3f), 2f),
                (new Vector3(3f, 0.1f, 0f), 1f),
                (new Vector3(-2f, 0.1f, 3f), 2f),
                (new Vector3(3f, 0.1f, 6.5f), 3f));
            AddCamera(env, "tower_cam_01", new Vector3(5.5f, 3f, 8.5f), 210f);

            // Last bottle on the crate, a locker by the spawn corner,
            // a cable tray to kick near the mast approach
            AddLure(env, new Vector3(-4.5f, 1.3f, 6.5f));
            AddLocker(env, new Vector3(-5.3f, 0, -4.4f), 90f);
            AddDistraction(env, new Vector3(3.4f, 0.9f, 1f), -90f, "Kick cable tray");

            AddLamp(env, new Vector3(0, 4f, -2f), new Color(0.5f, 0.65f, 0.85f), 9f, 1.3f);
            AddLamp(env, new Vector3(4f, 6.2f, 5f), new Color(0.8f, 0.85f, 1f), 8f, 2f, shadows: true);
            AddLightingZone(env, new Vector3(0, 1.5f, 2), new Vector3(12, 3, 15), PlayerVisibility.Exposure.Dim);
            AddLightingZone(env, new Vector3(-5f, 1.5f, -2f), new Vector3(3, 3, 6), PlayerVisibility.Exposure.Dark);
            AddLightingZone(env, new Vector3(4f, 6f, 5f), new Vector3(4.5f, 3, 7f), PlayerVisibility.Exposure.Bright);

            // Spawn tucked behind stair run A — the landing slab breaks the
            // tower camera's sightline to this corner.
            Spawn("Player", new Vector3(-5.2f, 0.1f, -1.5f));
            AddSpawn(env, "default", new Vector3(-5.2f, 0.1f, -1.5f), 60f);
            Spawn("GameUI", Vector3.zero);
            AddSceneBootstrap(scene, Escape.Data.SceneId.BroadcastTower,
                completesObjective: "reach_tower");
            BakeNav(env, scene, "BroadcastTower");
            Save(scene, "BroadcastTower");
        }

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{SceneDir}/Bootstrap.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/MainMenu.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/Dock.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/ServiceEntrance.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/MansionOffice.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/SecurityWing.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/BunkerServerRoom.unity", true),
                new EditorBuildSettingsScene($"{SceneDir}/BroadcastTower.unity", true),
            };
        }
    }
}
