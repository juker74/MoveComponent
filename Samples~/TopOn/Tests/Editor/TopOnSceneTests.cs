using System.Collections;
using System.Linq;
using GameFrameX.Ad.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameFrameX.Ad.Samples.Tests
{
    public sealed class TopOnSceneTests
    {
        [UnityTest]
        public IEnumerator ExampleSceneInitializesAllThreeFormats()
        {
            var scenePath = AssetDatabase.FindAssets("TopOnExample t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.StartsWith("Assets/"));
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            var ads = Object.FindObjectOfType<ADComponent>();
            Assert.That(ads, Is.Not.Null);
            Assert.That(Object.FindObjectOfType<TopOnExample>().Ads, Is.SameAs(ads));
            foreach (var format in new[] {ADFormat.reward, ADFormat.interstitial, ADFormat.splash})
            {
                Assert.That(ads.GetTaskPoolInfo("topon", format, out var free, out var working, out var waiting), Is.True);
                Assert.That(free, Is.GreaterThan(0));
                Assert.That(working + waiting, Is.Zero);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
