using GameFrameX.Ad.Runtime;
using UnityEngine;

namespace GameFrameX.Ad.Samples
{
    // Account credentials live on ADComponent's TopOn helper; placement IDs belong to the host.
    public sealed class TopOnExample : MonoBehaviour
    {
        public ADComponent Ads;
        public string RewardPlacementId = "";
        public string InterstitialPlacementId = "";
        public string SplashPlacementId = "";
        private string status = "Configure TopOn credentials and placement IDs before loading ads.";

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 620, 330), GUI.skin.box);
            GUILayout.Label("ADComponent / TopOn Example");
            GUILayout.Label("Editor uses SDK simulation; verify real ads on a configured device.");
            Draw("Reward", ADFormat.reward, ref RewardPlacementId);
            Draw("Interstitial", ADFormat.interstitial, ref InterstitialPlacementId);
            Draw("Splash", ADFormat.splash, ref SplashPlacementId);
            GUILayout.Label(status);
            GUILayout.EndArea();
        }

        private void Draw(string label, ADFormat format, ref string placement)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90));
            placement = GUILayout.TextField(placement);
            var id = placement;
            GUI.enabled = Ads != null && !string.IsNullOrWhiteSpace(placement);
            if (GUILayout.Button("Load", GUILayout.Width(60)))
                Submit(() => Ads.AddADRequest("topon", id, format, "example", 0, 60f, null));
            if (GUILayout.Button("Play", GUILayout.Width(60)))
                Submit(() => Ads.PlayAD("topon", id, format, "example", "example", 0, null));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void Submit(System.Func<int> submit)
        {
            try { status = "Queued task: " + submit(); }
            catch (System.Exception error) { status = error.Message; }
        }
    }
}
