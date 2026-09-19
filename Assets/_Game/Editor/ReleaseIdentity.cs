using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Release identity. The project shipped with the URP template's metadata
    /// (company "DefaultCompany", product "My project (7)", and template
    /// bundle identifiers), which makes builds identify themselves as a Unity
    /// template rather than a product.
    ///
    /// Deliberately a menu action rather than part of BuildAll: it rewrites
    /// ProjectSettings.asset, and a generation pass should not silently change
    /// player settings.
    /// </summary>
    public static class ReleaseIdentity
    {
        public const string Company = "Escape the Elites";
        public const string Product = "Escape the Elites";
        public const string BundleId = "com.escapetheelites.game";

        [MenuItem("Tools/Escape the Elites/Apply Release Identity")]
        public static void Apply()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ReleaseIdentity] company='{Company}' product='{Product}' id='{BundleId}'.");
        }

        /// <summary>True when the project still identifies as the template.</summary>
        public static bool IsTemplateIdentity =>
            PlayerSettings.companyName == "DefaultCompany" ||
            PlayerSettings.productName != Product;
    }
}
