using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.Ad.Samples.Editor
{
    public static class TopOnSdkInstaller
    {
        // The vendor build processors and SDK Manager expect this canonical Assets path.
        [MenuItem("Tools/GameFrameX/AD/Install Sample TopOn SDK")]
        public static void Install()
        {
            const string destination = "Assets/ThinkupTpnPlugin";
            var source = AssetDatabase.FindAssets("ATConfig t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("/ThinkupTpnPlugin/Script/IntegrationManager/Editor/ATConfig.cs"));
            if (source == null) throw new System.InvalidOperationException("Import TopOn Example from Package Manager first.");
            source = source.Substring(0, source.IndexOf("/ThinkupTpnPlugin/", System.StringComparison.Ordinal)) + "/ThinkupTpnPlugin";
            if (source == destination) return;
            if (AssetDatabase.IsValidFolder(destination))
                throw new System.InvalidOperationException("An SDK already exists at " + destination + ". Resolve the duplicate before moving the sample SDK.");
            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
            AssetDatabase.Refresh();
            Debug.Log("TopOn sample SDK installed at " + destination);
        }
    }
}
