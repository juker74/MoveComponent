using UnityEditor;
using UnityEngine;

namespace AnyThink.Scripts.IntegrationManager.Editor
{
    public class AnyThinkMenuItems : MonoBehaviour
    {
        [MenuItem("ThinkupTpnPlugin/SDK Manager %#t")]
        private static void IntegrationManager()
        {
            ATIntegrationManagerWindow.ShowManager();
        }

        [MenuItem("ThinkupTpnPlugin/Documentation")]
        public static void Documentation()
        {
            Application.OpenURL("https://help.toponad.com/cn/docs/SDK-dao-ru-shuo-ming");
        }
    }
}
