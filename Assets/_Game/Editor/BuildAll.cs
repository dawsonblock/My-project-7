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
        [MenuItem("Tools/Escape the Elites/Build All")]
        public static void Run()
        {
            ProjectSetup.EnsureLayers();
            WebDataImporter.ImportAll();
            AudioLibraryBuilder.Build();
            PrefabFactory.BuildAll();
            SceneFactory.BuildAll();
            MaterialLibrary.ApplyAll();
            Debug.Log("[BuildAll] Done. Open Bootstrap and press Play.");
        }
    }
}
