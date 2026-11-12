using System;
using System.Collections.Generic;
using System.Reflection;
using AnyThinkAds.Api;
using GameFrameX.Ad;
using GameFrameX.Ad.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GameFrameX.Ad.Samples.Tests
{
    public sealed class TopOnAdvertisingTests
    {
        private GameObject root;
        private TestPlatform platform;
        private TestAdLoader loader;
        private TestAdPlayer player;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Ad module tests");
            platform = new TestPlatform();
            loader = root.AddComponent<TestAdLoader>();
            player = root.AddComponent<TestAdPlayer>();
            platform.Init(null);
            platform.AddADRequestAgentHelper(loader);
            platform.AddADPlayAgentHelper(player);
        }

        [TearDown]
        public void TearDown()
        {
            platform.Shutdown();
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void PlatformInitializesImmediatelyWithoutConsentCallback()
        {
            Assert.That(platform.Initializations, Is.EqualTo(1));
            Assert.DoesNotThrow(() => new TopOnADHelper().Init(null), "Editor skips native SDK initialization.");
            Assert.That(typeof(TopOnADHelper).GetField("m_PlatformConfigs", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(typeof(TopOnADHelper).GetField("m_AppId", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(TopOnADHelper).GetField("m_AppKey", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        }

        [Test]
        public void TopOnCredentialsResolveFromPlatformConfigurationCollection()
        {
            var helperType = typeof(TopOnADHelper);
            var configsField = helperType.GetField("m_PlatformConfigs", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(configsField, Is.Not.Null);
            var configType = configsField.FieldType.GetGenericArguments()[0];
            var configs = (System.Collections.IList)configsField.GetValue(new TopOnADHelper());
            var android = Activator.CreateInstance(configType);
            configType.GetField("Platform").SetValue(android, RuntimePlatform.Android);
            configType.GetField("AppId").SetValue(android, "android-app");
            configType.GetField("AppKey").SetValue(android, "android-key");
            configs.Add(android);

            var helper = new TopOnADHelper();
            configsField.SetValue(helper, configs);
            var method = helperType.GetMethod("TryGetPlatformCredentials", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var args = new object[] { RuntimePlatform.Android, null, null };
            Assert.That(method.Invoke(helper, args), Is.True);
            Assert.That(args[1], Is.EqualTo("android-app"));
            Assert.That(args[2], Is.EqualTo("android-key"));
        }

        [Test]
        public void TopOnSdkInitializationStateFollowsSdkCallbacks()
        {
            var listenerType = typeof(TopOnADHelper).GetNestedType("SdkInitListener",
                BindingFlags.NonPublic);
            Assert.That(listenerType, Is.Not.Null);
            var listenerConstructor = listenerType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(TopOnADHelper) }, null);
            Assert.That(listenerConstructor, Is.Not.Null);

            var helper = new TopOnADHelper();
            var listener = listenerConstructor.Invoke(new object[] { helper });
            listenerType.GetMethod("initSuccess").Invoke(listener, null);
            Assert.That(typeof(TopOnADHelper).GetField("initialized", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(helper), Is.True);

            helper = new TopOnADHelper();
            listener = listenerConstructor.Invoke(new object[] { helper });
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                "[TopOnADHelper] SDK initialization failed: failed");
            listenerType.GetMethod("initFail").Invoke(listener, new object[] { "failed" });
            Assert.That(typeof(TopOnADHelper).GetField("initialized", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(helper), Is.False);
        }



        [Test]
        public void ComponentCleanupDoesNotRemoveReplacementOnDeferredDestroy()
        {
            var owner = new GameObject("Failed ad initialization");
            owner.SetActive(false);
            try
            {
                var component = owner.AddComponent<ADComponent>();
                var manager = new GameFrameX.Ad.ADManager();
                typeof(ADComponent).GetField("m_ADManager", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(component, manager);
                component.RegisterHelper(new TestPlatform());
                typeof(ADComponent).GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(component, null);

                manager.AddADHelper(new TestPlatform());
                typeof(ADComponent).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(component, null);
                Assert.That(manager.RemoveADHelper("test"), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test]
        public void ComponentIsTheOnlyConfiguredPlaybackEntry()
        {
            Assert.That(typeof(ADComponent).GetMethod("EnsureInitialized"), Is.Null);
            Assert.That(typeof(ADComponent).Assembly.GetType("GameFrameX.Ad.Runtime.TopOnSettings"), Is.Null);
            Assert.That(typeof(ADComponent).GetMethod("InitializeTopOn", BindingFlags.NonPublic | BindingFlags.Instance), Is.Null);
            Assert.That(typeof(ADComponent).Assembly.GetType("GameFrameX.Ad.Runtime.TopOnAdvertising"), Is.Null);
        }

        [Test]
        public void ComponentStartInitializesConfiguredHelperAndAgentsOnlyOnce()
        {
            var owner = new GameObject("Configured lifecycle");
            owner.SetActive(false);
            var component = owner.AddComponent<ADComponent>();
            var helper = new TestPlatform();
            var manager = new GameFrameX.Ad.ADManager();
            var events = new GameFrameX.Event.Runtime.EventManager();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                typeof(ADComponent).GetField("m_ADManager", flags).SetValue(component, manager);
                typeof(ADComponent).GetField("m_EventComponent", flags).SetValue(component, events);
                typeof(ADComponent).GetField("m_ADHelperConfigs", flags).SetValue(component,
                    new List<ADHelperConfigItem> { new ADHelperConfigItem {
                        Helper = helper,
                        AgentConfigs = new List<ADRequestAgentConfigItem> {
                            new ADRequestAgentConfigItem { CustomAgentHelper = loader } },
                        PlayAgentConfigs = new List<ADPlayAgentConfigItem> {
                            new ADPlayAgentConfigItem { CustomAgentHelper = player } } } });
                component.Timeout = 17;
                var start = typeof(ADComponent).GetMethod("Start", flags);
                start.Invoke(component, null);
                start.Invoke(component, null);
                Assert.That(helper.Initializations, Is.EqualTo(1));
                Assert.That(helper.LoadTimeOut, Is.EqualTo(17));
                Assert.That(helper.GetTaskPoolInfo(ADFormat.reward, out _, out _, out _), Is.True);
                helper.AddADRequest("configured", ADFormat.reward, "test", 0, 10, null);
                helper.PlayAD("configured", ADFormat.reward, "scene", "test", 0, null);
                helper.Update(.02f, .02f);
                Assert.That(loader.Requests, Is.EqualTo(new[] { "configured" }));
                Assert.That(player.Plays, Is.EqualTo(new[] { "configured" }));
            }
            finally
            {
                typeof(ADComponent).GetMethod("Shutdown", flags).Invoke(component, null);
                UnityEngine.Object.DestroyImmediate(owner);
                typeof(GameFrameX.Event.Runtime.EventManager).GetMethod("Shutdown", flags).Invoke(events, null);
            }
        }



        [Test]
        public void ComponentAwakeRegistersManagerAndRemainsEnabled()
        {
            var owner = new GameObject("Ad lifecycle test");
            owner.SetActive(false);
            var component = owner.AddComponent<ADComponent>();
            var registry = (GameFrameX.Runtime.GameFrameworkLinkedList<GameFrameX.Runtime.GameFrameworkComponent>)typeof(GameFrameX.Runtime.GameEntry)
                .GetField("GameFrameworkComponents", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            try
            {
                GameFrameX.Runtime.GameFrameworkEntry.RegisterModule(typeof(GameFrameX.Event.Runtime.IEventManager),
                    typeof(GameFrameX.Event.Runtime.EventManager));
                typeof(ADComponent).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(component, null);
                Assert.That(component.enabled, Is.True);
                Assert.That(typeof(ADComponent).GetField("m_ADManager", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(component), Is.Not.Null);
                Assert.That(GameFrameX.Runtime.GameEntry.GetComponent<ADComponent>(), Is.SameAs(component));
            }
            finally
            {
                registry.Remove(component);
                typeof(ADComponent).GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(component, null);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [TestCase(true, ADFormat.reward)]
        [TestCase(false, ADFormat.reward)]
        [TestCase(true, ADFormat.interstitial)]
        [TestCase(false, ADFormat.interstitial)]
        public void ComponentForwardsExplicitPlaybackWithoutAutomaticRefill(bool success, ADFormat format)
        {
            platform.Shutdown();
            loader.Format = format;
            player.Format = format;
            platform = new TestPlatform();
            platform.Init(null);
            platform.AddADRequestAgentHelper(loader);
            platform.AddADPlayAgentHelper(player);
            WithComponent((component, events) =>
            {
                var results = new List<bool>();
                var request = new object();
                events.Subscribe(GameFrameX.Ad.Runtime.ADPlaySuccessEventArgs.EventId, (_, e) =>
                {
                    Assert.That(((GameFrameX.Ad.Runtime.ADPlaySuccessEventArgs)e).UserData, Is.SameAs(request));
                    results.Add(true);
                });
                events.Subscribe(GameFrameX.Ad.Runtime.ADPlayFailureEventArgs.EventId, (_, e) =>
                {
                    Assert.That(((GameFrameX.Ad.Runtime.ADPlayFailureEventArgs)e).UserData, Is.SameAs(request));
                    results.Add(false);
                });
                component.PlayAD("test", "one", format, "scene", "movie", 0, request);
                Tick();
                Assert.That(player.Plays, Is.EqualTo(new[] { "one" }));
                if (success) player.Close("one"); else player.Fail("one");
                player.Close("one");
                Assert.That(results, Is.Empty, "Framework events remain queued.");
                DispatchEvents(events);
                Tick();
                Assert.That(results, Is.EqualTo(new[] { success }));
                Assert.That(loader.Requests, Is.Empty);
            });
        }

        [Test]
        public void ComponentRejectsEmptyPositionThroughOriginalInterface()
        {
            WithComponent((component, events) =>
            {
                Assert.Throws<GameFrameX.Runtime.GameFrameworkException>(() =>
                    component.PlayAD("test", "", ADFormat.reward, "", "movie", 0, null));
                Assert.That(player.Plays, Is.Empty);
                Assert.That(loader.Requests, Is.Empty);
            });
        }



        private void WithComponent(Action<ADComponent, GameFrameX.Event.Runtime.EventManager> test)
        {
            var owner = new GameObject("Configured ad component");
            owner.SetActive(false);
            var component = owner.AddComponent<ADComponent>();
            var manager = new GameFrameX.Ad.ADManager();
            var events = new GameFrameX.Event.Runtime.EventManager();
            try
            {
                typeof(ADComponent).GetField("m_ADManager", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(component, manager);
                typeof(ADComponent).GetField("m_EventComponent", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(component, events);
                foreach (var name in new[] { "ADPlaySuccess", "ADPlayFailure" })
                {
                    var info = typeof(GameFrameX.Ad.ADManager).GetEvent(name);
                    var method = typeof(ADComponent).GetMethod("On" + name, BindingFlags.Instance | BindingFlags.NonPublic);
                    info.AddEventHandler(manager, Delegate.CreateDelegate(info.EventHandlerType, component, method));
                }
                component.RegisterHelper(platform);
                test(component, events);
            }
            finally
            {
                typeof(ADComponent).GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(component, null);
                UnityEngine.Object.DestroyImmediate(owner);
                typeof(GameFrameX.Event.Runtime.EventManager).GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(events, null);
            }
        }

        private static void DispatchEvents(GameFrameX.Event.Runtime.EventManager events) =>
            typeof(GameFrameX.Event.Runtime.EventManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(events, new object[] { .02f, .02f });

        [Test]
        public void PendingLoadsAreDeduplicated()
        {
            var first = Load("one");
            Assert.That(Load("one"), Is.EqualTo(first));
            Tick();
            Assert.That(loader.Requests, Is.EqualTo(new[] { "one" }));
        }

        [Test]
        public void LoadsAndPlaysUseIndependentPools()
        {
            Load("one"); Play("two"); Tick();
            Assert.That(loader.Requests, Has.Count.EqualTo(1));
            Assert.That(player.Plays, Is.EqualTo(new[] { "two" }));
        }

        [Test]
        public void SharedHelperCallbacksReachAllAgentsBeforeReferenceRelease()
        {
            var completed = new List<string>();
            platform.ADRequestSuccess += (_, e) => completed.Add(e.PlacementID);
            Load("one"); Load("two"); Load("three"); Tick();
            Assert.That(loader.Requests, Is.EqualTo(new[] { "one", "two", "three" }));
            loader.Complete("three"); loader.Complete("two"); loader.Complete("one");
            Assert.That(completed, Is.EqualTo(new[] { "three", "two", "one" }));
        }

        [Test]
        public void DuplicateCloseOnlyCompletesOnce()
        {
            var count = 0;
            platform.ADPlaySuccess += (_, e) => count++;
            Play("one"); Tick(); player.Close("one"); player.Close("one");
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void ShowingAdDoesNotTimeoutWhileWaitingForClose()
        {
            var success = 0; var failure = 0;
            platform.ADPlaySuccess += (_, e) => success++;
            platform.ADPlayFailure += (_, e) => failure++;
            Play("one"); Tick(); player.Show("one"); Tick(1000);
            Assert.That(success + failure, Is.Zero);
            player.Close("one");
            Assert.That(success, Is.EqualTo(1));
        }

        [Test]
        public void PlaybackFailureDoesNotBecomeSuccessOnLateClose()
        {
            var success = 0; var failure = 0;
            platform.ADPlaySuccess += (_, e) => success++;
            platform.ADPlayFailure += (_, e) => failure++;
            Play("one"); Tick(); player.Fail("one"); player.Close("one");
            Assert.That(failure, Is.EqualTo(1));
            Assert.That(success, Is.Zero);
        }

        [Test]
        public void CancelledWaitingTasksNeverReachSdk()
        {
            Assert.That(platform.RemoveADRequest(Load("one")), Is.True);
            Assert.That(platform.RemoveADPlayRequest(Play("one")), Is.True);
            Tick();
            Assert.That(loader.Requests, Is.Empty);
            Assert.That(player.Plays, Is.Empty);
        }

        [Test]
        public void NativeLoadExceptionReportsFailureAndPoolContinues()
        {
            var failures = 0;
            platform.ADRequestFailure += (_, e) => failures++;
            loader.ThrowOnRequest = true;
            Load("one");
            Assert.DoesNotThrow(() => Tick());
            Assert.That(failures, Is.EqualTo(1));
            loader.ThrowOnRequest = false;
            Load("two"); Tick();
            Assert.That(loader.Requests, Does.Contain("two"));
        }

        [Test]
        public void NativePlayExceptionReportsFailureAndPoolContinues()
        {
            var failures = 0;
            platform.ADPlayFailure += (_, e) => failures++;
            player.ThrowOnPlay = true;
            Play("one");
            Assert.DoesNotThrow(() => Tick());
            Assert.That(failures, Is.EqualTo(1));
            player.ThrowOnPlay = false;
            Play("two"); Tick();
            Assert.That(player.Plays, Does.Contain("two"));
        }

        [Test]
        public void ExpiredLoadedPlacementIsForceReloaded()
        {
            Load("one"); Tick(); loader.Complete("one"); Tick(2);
            Assert.That(loader.Requests, Has.Count.EqualTo(2));
            Assert.That(loader.LastForceLoad, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NativeRewardCloseOnlyCompletesWhenRewarded(bool rewarded)
        {
            var native = root.AddComponent<TopOnPlayRewardPlayAgent>();
            native.Initialize();
            try
            {
                var tracking = (Dictionary<string, string>)typeof(TopOnPlayRewardPlayAgent)
                    .GetField("m_PosIds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(native);
                tracking.Add("one", "scene");
                var completions = 0;
                var failures = 0;
                native.ADPlayAgentHelperComplete += (_, e) => { Assert.That(e.PosID, Is.EqualTo("scene")); completions++; };
                native.ADPlayAgentHelperError += (_, e) =>
                {
                    Assert.That(e.PosID, Is.EqualTo("scene"));
                    Assert.That(e.ErrorCode, Is.EqualTo("AD_REWARD_NOT_EARNED"));
                    failures++;
                };
                var args = new ATAdRewardEventArgs("one", "{}", rewarded);
                var close = typeof(TopOnPlayRewardPlayAgent).GetMethod("OnRewardClose", BindingFlags.Instance | BindingFlags.NonPublic);
                close.Invoke(native, new object[] { null, args });
                close.Invoke(native, new object[] { null, args });
                Assert.That(completions + failures, Is.Zero);
                native.DispatchCallbacks();
                Assert.That(completions, Is.EqualTo(rewarded ? 1 : 0));
                Assert.That(failures, Is.EqualTo(rewarded ? 0 : 1));
                Assert.That(tracking, Is.Empty);
            }
            finally { native.Shutdown(); }
        }

        [Test]
        public void NativeSplashCloseDispatchesOnce()
        {
            var native = root.AddComponent<TopOnPlaySplashPlayAgent>();
            native.Initialize();
            try
            {
                var tracking = (Dictionary<string, string>)typeof(TopOnPlaySplashPlayAgent)
                    .GetField("m_PosIds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(native);
                tracking.Add("one", "scene");
                var count = 0;
                native.ADPlayAgentHelperComplete += (_, e) =>
                {
                    Assert.That(e.PosID, Is.EqualTo("scene"));
                    count++;
                };
                var args = new ATAdEventArgs("one", "{}");
                var close = typeof(TopOnPlaySplashPlayAgent)
                    .GetMethod("OnSplashAdClose", BindingFlags.Instance | BindingFlags.NonPublic);
                close.Invoke(native, new object[] { null, args });
                close.Invoke(native, new object[] { null, args });
                Assert.That(count, Is.Zero);
                native.DispatchCallbacks();
                Assert.That(count, Is.EqualTo(1));
                Assert.That(tracking, Is.Empty);
            }
            finally { native.Shutdown(); }
        }

        [Test]
        public void ShutdownDiscardsQueuedNativeCallbacks()
        {
            var count = 0;
            loader.ADRequestAgentHelperComplete += (_, e) => count++;
            loader.EnqueueComplete("one"); loader.Shutdown(); loader.DispatchCallbacks();
            Assert.That(count, Is.Zero);
        }

        private int Load(string id) => platform.AddADRequest(id, ADFormat.reward, "test", 0, 1, null);
        private int Play(string id) => platform.PlayAD(id, ADFormat.reward, "scene", "test", 0, null);
        private void Tick(float delta = .02f) => platform.Update(delta, delta);

        private sealed class TestPlatform : ADHelperBase
        {
            public int Initializations;
            public string PlatformName = "test";
            public override string Name => PlatformName;
            public override void Init(object data) { Initializations++; }
        }
    }

    public sealed class TestAdLoader : ADRequestAgentHelperBase
    {
        public readonly List<string> Requests = new List<string>();
        public bool LastForceLoad;
        public bool ThrowOnRequest;
        public ADFormat Format = ADFormat.reward;
        public override ADFormat SupportedFormat => Format;
        public override void Request(string id, bool force, object data)
        {
            if (ThrowOnRequest) throw new InvalidOperationException("Native load failed");
            Requests.Add(id); LastForceLoad = force;
        }
        public void Complete(string id) => Publish(m_ADRequestAgentHelperComplete, ADRequestAgentHelperCompleteEventArgs.Create(id, null));
        public void EnqueueComplete(string id) => Dispatch(() => Complete(id));
        public override string CheckAdStatus(string id) => "ready";
        public override double GetAdValue(string id) => 1;
        public override bool IsAdReady(string id) => true;
        public override void Reset() { }
    }

    public sealed class TestAdPlayer : ADPlayAgentHelperBase
    {
        public readonly List<string> Plays = new List<string>();
        public bool ThrowOnPlay;
        public ADFormat Format = ADFormat.reward;
        public override ADFormat SupportedFormat => Format;
        public override void Play(string id, string scenario, object data)
        {
            if (ThrowOnPlay) throw new InvalidOperationException("Native show failed");
            Plays.Add(id);
        }
        public void Close(string id) => Publish(m_ADPlayAgentHelperCompleteEvent, ADPlayAgentHelperCompleteEventArgs.Create(id, "scene"));
        public void Show(string id) => Publish(m_ADPlayAgentHelperShowEvent, ADPlayAgentHelperShowEventArgs.Create(id, "scene"));
        public void Fail(string id) => Publish(m_ADPlayAgentHelperErrorEvent, ADPlayAgentHelperErrorEventArgs.Create(id, "scene", "failed", "test"));
        public override string CheckAdStatus(string id) => "ready";
        public override void Reset() { }
    }
}
