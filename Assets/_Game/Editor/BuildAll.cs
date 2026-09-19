using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Tools → Escape the Elites → Build All: layers → content import →
    /// prefabs → scenes. One deterministic pipeline for the whole slice.
    /// </summary>
    public static class BuildAll
    {
        /// <summary>
        /// Generation is editor-only: SceneFactory opens scenes with
        /// EditorSceneManager.NewScene, which throws in play mode. A run that
        /// gets that far recreates prefabs with fresh fileIDs and then fails,
        /// leaving the next successful run to renumber every scene that
        /// references them. Refuse up front instead.
        /// </summary>
        public static bool RefuseIfPlaying(string what)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
            Debug.LogError($"[{what}] Refusing to generate while the editor is in play mode — " +
                           "stop play mode first. A partial run renumbers prefab fileIDs.");
            return true;
        }

        [MenuItem("Tools/Escape the Elites/Build All")]
        public static void Run()
        {
            if (RefuseIfPlaying("BuildAll")) return;
            ProjectSetup.EnsureLayers();
            WebDataImporter.ImportAll();
            AudioLibraryBuilder.Build();
            PrefabFactory.BuildAll();
            SceneFactory.BuildAll();
            MaterialLibrary.ApplyAll();
            VolumeProfileBuilder.Author();
            Debug.Log("[BuildAll] Done. Open Bootstrap and press Play.");
        }
    }
}
