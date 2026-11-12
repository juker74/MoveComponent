#if UNITY_IOS || UNITY_IPHONE

using AnyThink.Scripts.IntegrationManager.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace AnyThink.Scripts.Editor
{
    [Serializable]
    public class ATSkAdNetworkInfo
    {
        [SerializeField] public string[] ids;
    }

    /// <summary>
    /// Configures iOS deployment settings and scene lifecycle.
    /// CocoaPods owns Pod frameworks and resources in the app target.
    /// </summary>
    public class ATPostProcessBuildiOS
    {
        // Fallback only when Pods is not installed / scan finds nothing.
        // New networks should NOT require editing this list — prefer auto-scan after pod install.
        private static readonly string[] FixedEmbedFrameworksFallback = {
            "AnyThinkiOS/core/AnyThinkSDK.xcframework",
            "TPNiOS/core/AnyThinkSDK.xcframework",
            "KSAdSDK/KSAdSDK.xcframework",
            "StartAppSDK/StartApp.xcframework",
            "BigoADS/BigoADS/BigoADS.xcframework",
            "BigoADS/BigoADS/OMSDK_Bigosg.xcframework",
            "Fyber_Marketplace_SDK/IASDKCore/IASDKCore.xcframework",
            "InMobiSDK/InMobiSDK.xcframework",
            "GDTMobSDK/GDTFramework/GDTMobSDK.xcframework",
            "GDTMobSDK/GDTFramework/Tquic.xcframework",
            "ATOM-Standalone/ATOM.xcframework",
            "SmartdigimktCNSDK/KuYingSDK/SmartdigimktSDK.xcframework",
            "SmartdigimktSDK/SmartDigimktSDK/SmartdigimktSDK.xcframework"
        };

        private const string MinIosDeploymentTarget = "15.0";
        private const string EmbedScriptFileName = "embed_dynamic_pods.py";
        private const string CopyScriptFileName = "copy_dynamic_pods.sh";
        private const string CopyBuildPhaseName = "[AT] Copy Dynamic Pods";
        private const string PodfilePostInstallMarker = "AT_UNITY_POST_INSTALL";
        private const string SceneDelegateClassName = "ATUnitySceneDelegate";
        private const string SceneDelegateMmFile = "ATUnitySceneDelegate.mm";
        private const string SceneDelegateHFile = "ATUnitySceneDelegate.h";

        // EDM4U: GenPodfile=40, InstallPods=50. Patch platform after Podfile is written
        // and before `pod install`, otherwise Facebook/FBAudienceNetwork (iOS 15+) fails.
        private const int BuildOrderPatchPodfilePlatform = 45;

        [PostProcessBuildAttribute(BuildOrderPatchPodfilePlatform)]
        public static void ATPostProcessPatchPodfilePlatform(BuildTarget buildTarget, string buildPath)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }
            EnsurePodfilePlatform(buildPath);
        }

        [PostProcessBuildAttribute(int.MaxValue)]
        public static void ATPostProcessPbxProject(BuildTarget buildTarget, string buildPath)
        {
            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

#if UNITY_2019_3_OR_NEWER
            var unityMainTargetGuid = project.GetUnityMainTargetGuid();
            var unityFrameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
#else
            var unityMainTargetGuid = project.TargetGuidByName(UnityMainTargetName);
            var unityFrameworkTargetGuid = project.TargetGuidByName(UnityMainTargetName);
#endif

            project.SetBuildProperty(unityMainTargetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");
            project.SetBuildProperty(unityMainTargetGuid, "ENABLE_BITCODE", "NO");
            project.SetBuildProperty(unityMainTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", MinIosDeploymentTarget);
            // The app embeds UnityFramework, but must not link the same static Pods again.
            project.SetBuildProperty(unityMainTargetGuid, "OTHER_LDFLAGS", "-ObjC");

            project.SetBuildProperty(unityFrameworkTargetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");
            project.SetBuildProperty(unityFrameworkTargetGuid, "ENABLE_BITCODE", "NO");
            project.SetBuildProperty(unityFrameworkTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", MinIosDeploymentTarget);

            EnsurePodfilePostInstallFix(buildPath);
            EnsureUISceneLifecycleSupport(buildPath, project, unityFrameworkTargetGuid);

            project.WriteToFile(projectPath);
            NormalizeXcodeProjectFile(projectPath);
        }

        [PostProcessBuildAttribute(int.MaxValue)]
        public static void ATPostProcessPlist(BuildTarget buildTarget, string path)
        {
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

#if UNITY_2018_2_OR_NEWER
            ConfigureAdmobAppIdentifier(plist);
#endif
            EnsureUIApplicationSceneManifest(plist);
            plist.WriteToFile(plistPath);
        }

        /// <summary>
        /// New Xcode/iOS SDKs require UIScene lifecycle or the app traps at launch.
        /// Unity official fix is 2022.3.72f1+; this adds a minimal bridge for older editors.
        /// </summary>
        private static void EnsureUISceneLifecycleSupport(string buildPath, PBXProject project, string unityFrameworkTargetGuid)
        {
            var srcDir = FindEditorHelperScript(SceneDelegateMmFile);
            if (string.IsNullOrEmpty(srcDir))
            {
                var candidates = new[]
                {
                    Path.Combine(Application.dataPath, "ThinkupTpnPlugin/Script/Editor/iOS"),
                    Path.Combine(Application.dataPath, "TpnPlugin/Script/Editor/iOS"),
                };
                foreach (var dir in candidates)
                {
                    if (File.Exists(Path.Combine(dir, SceneDelegateMmFile)))
                    {
                        srcDir = dir;
                        break;
                    }
                }
            }
            else
            {
                srcDir = Path.GetDirectoryName(srcDir);
            }

            if (string.IsNullOrEmpty(srcDir))
            {
                ATLog.logError("EnsureUISceneLifecycleSupport() >>> ATUnitySceneDelegate sources not found.");
                return;
            }

            var destDir = Path.Combine(buildPath, "Classes/UI");
            Directory.CreateDirectory(destDir);

            // Remove legacy TopOn-prefixed SceneDelegate if present.
            foreach (var legacy in new[] { "TopOnUnitySceneDelegate.h", "TopOnUnitySceneDelegate.mm" })
            {
                var legacyPath = Path.Combine(destDir, legacy);
                if (File.Exists(legacyPath))
                    File.Delete(legacyPath);
            }

            foreach (var name in new[] { SceneDelegateHFile, SceneDelegateMmFile })
            {
                File.Copy(Path.Combine(srcDir, name), Path.Combine(destDir, name), true);
            }

#if UNITY_2019_3_OR_NEWER
            var mmRel = "Classes/UI/" + SceneDelegateMmFile;
            var guid = project.AddFile(mmRel, mmRel, PBXSourceTree.Source);
            project.AddFileToBuild(unityFrameworkTargetGuid, guid);
#endif

            PatchUnityAppControllerForUIScene(Path.Combine(buildPath, "Classes/UnityAppController.mm"));
            ATLog.log("EnsureUISceneLifecycleSupport() >>> SceneDelegate installed.");
        }

        private static void PatchUnityAppControllerForUIScene(string appControllerPath)
        {
            if (!File.Exists(appControllerPath)) return;
            var text = File.ReadAllText(appControllerPath);

            // Always defer Unity init to SceneDelegate on iOS 13+.
            // Initiating in didFinishLaunching before windowScene is set causes a black screen
            // (Unity window exists but is not attached to the active UIWindowScene).
            const string atMarker = "AT: UIScene lifecycle — defer Unity init to SceneDelegate";
            if (text.Contains(atMarker))
                return;

            const string newDeferBlock =
                "    if (UIApplication.sharedApplication.applicationState == UIApplicationStateBackground)\n" +
                "        return YES;\n" +
                "\n" +
                "    // AT: UIScene lifecycle — defer Unity init to SceneDelegate\n" +
                "    if (@available(iOS 13.0, tvOS 13.0, *))\n" +
                "    {\n" +
                "        // With UIApplicationSceneManifest, window must be created in scene:willConnectToSession:\n" +
                "        return YES;\n" +
                "    }\n" +
                "\n" +
                "    [self initUnityWithApplication: application];\n" +
                "    return YES;\n" +
                "}";

            // Replace prior TopOn defer (only when scenes.count == 0) — that still inited too early.
            const string oldTopOnDefer =
                "    if (UIApplication.sharedApplication.applicationState == UIApplicationStateBackground)\n" +
                "        return YES;\n" +
                "\n" +
                "    // TopOn: with UIScene lifecycle, wait for scene connection if none yet.\n" +
                "    if (@available(iOS 13.0, tvOS 13.0, *))\n" +
                "    {\n" +
                "        if (application.connectedScenes.count == 0)\n" +
                "            return YES;\n" +
                "    }\n" +
                "\n" +
                "    [self initUnityWithApplication: application];\n" +
                "    return YES;\n" +
                "}";

            const string stockBlock =
                "    if (UIApplication.sharedApplication.applicationState == UIApplicationStateBackground)\n" +
                "        return YES;\n" +
                "\n" +
                "    [self initUnityWithApplication: application];\n" +
                "    return YES;\n" +
                "}";

            if (text.Contains(oldTopOnDefer))
            {
                File.WriteAllText(appControllerPath, text.Replace(oldTopOnDefer, newDeferBlock));
                ATLog.log("PatchUnityAppControllerForUIScene() >>> upgraded defer: always SceneDelegate on iOS 13+.");
                return;
            }

            if (text.Contains(stockBlock))
            {
                File.WriteAllText(appControllerPath, text.Replace(stockBlock, newDeferBlock));
                ATLog.log("PatchUnityAppControllerForUIScene() >>> deferred Unity init to SceneDelegate on iOS 13+.");
                return;
            }

            ATLog.log("PatchUnityAppControllerForUIScene() >>> pattern not found, skip.");
        }

        private static void EnsureUIApplicationSceneManifest(PlistDocument plist)
        {
            const string manifestKey = "UIApplicationSceneManifest";
            PlistElementDict manifest;
            if (plist.root.values.ContainsKey(manifestKey))
            {
                manifest = plist.root[manifestKey].AsDict();
            }
            else
            {
                manifest = plist.root.CreateDict(manifestKey);
                manifest.SetBoolean("UIApplicationSupportsMultipleScenes", false);
            }

            PlistElementDict configs;
            if (manifest.values.ContainsKey("UISceneConfigurations"))
                configs = manifest["UISceneConfigurations"].AsDict();
            else
                configs = manifest.CreateDict("UISceneConfigurations");

            PlistElementArray roles;
            if (configs.values.ContainsKey("UIWindowSceneSessionRoleApplication"))
                roles = configs["UIWindowSceneSessionRoleApplication"].AsArray();
            else
                roles = configs.CreateArray("UIWindowSceneSessionRoleApplication");

            PlistElementDict conf;
            if (roles.values.Count > 0)
                conf = roles.values[0].AsDict();
            else
                conf = roles.AddDict();

            conf.SetString("UISceneConfigurationName", "Default Configuration");
            conf.SetString("UISceneDelegateClassName", SceneDelegateClassName);
            ATLog.log("EnsureUIApplicationSceneManifest() >>> UISceneDelegateClassName=" + SceneDelegateClassName);
        }

        private static List<string> CollectDynamicFrameworkPaths(string buildRoot)
        {
            var scanned = ScanPodsForDynamicFrameworks(buildRoot);
            if (scanned.Count > 0)
            {
                ATLog.log("CollectDynamicFrameworkPaths() >>> auto-scanned: " + scanned.Count);
                return scanned;
            }

            ATLog.log("CollectDynamicFrameworkPaths() >>> Pods not ready, using fallback list (existing paths only).");
            var result = new List<string>();
            foreach (var fw in FixedEmbedFrameworksFallback)
            {
                var rel = ("Pods/" + fw).Replace('\\', '/');
                if (Directory.Exists(Path.Combine(buildRoot, rel)))
                {
                    result.Add(rel);
                }
            }
            return result.Distinct().ToList();
        }

        /// <summary>
        /// Walk Pods and collect vendored .xcframework / .framework that are dynamic (@rpath).
        /// Static archives must NOT be embedded.
        /// </summary>
        private static List<string> ScanPodsForDynamicFrameworks(string buildRoot)
        {
            var result = new List<string>();
            var podsRoot = Path.Combine(buildRoot, "Pods");
            if (!Directory.Exists(podsRoot)) return result;

            foreach (var xc in Directory.GetDirectories(podsRoot, "*.xcframework", SearchOption.AllDirectories))
            {
                if (xc.Contains("/Target Support Files/") || xc.Contains(".dSYM")) continue;
                // Incomplete packages (e.g. OMSDK_Appodeal/Dynamic) have no Info.plist and break Embed.
                if (!File.Exists(Path.Combine(xc, "Info.plist"))) continue;
                if (!XcframeworkContainsDynamicBinary(xc)) continue;
                result.Add(MakeBuildRelativePath(buildRoot, xc));
            }

            foreach (var fw in Directory.GetDirectories(podsRoot, "*.framework", SearchOption.AllDirectories))
            {
                if (fw.Contains(".xcframework/") || fw.Contains(".dSYM") || fw.Contains("/Target Support Files/"))
                    continue;
                if (!FrameworkContainsDynamicBinary(fw)) continue;
                result.Add(MakeBuildRelativePath(buildRoot, fw));
            }

            return result.Distinct().ToList();
        }

        private static string MakeBuildRelativePath(string buildRoot, string absolutePath)
        {
            var fullBuild = Path.GetFullPath(buildRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(absolutePath);
            if (fullPath.StartsWith(fullBuild, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(fullBuild.Length).Replace('\\', '/');
            }
            return absolutePath.Replace('\\', '/');
        }

        private static bool XcframeworkContainsDynamicBinary(string xcframeworkPath)
        {
            foreach (var fw in Directory.GetDirectories(xcframeworkPath, "*.framework", SearchOption.AllDirectories))
            {
                if (FrameworkContainsDynamicBinary(fw)) return true;
            }
            return false;
        }

        private static bool FrameworkContainsDynamicBinary(string frameworkPath)
        {
            var binaryName = Path.GetFileNameWithoutExtension(frameworkPath);
            var binaryPath = Path.Combine(frameworkPath, binaryName);
            if (!File.Exists(binaryPath)) return false;
            return IsDynamicSharedLibrary(binaryPath);
        }

        private static bool IsDynamicSharedLibrary(string binaryPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/file",
                    Arguments = "\"" + binaryPath.Replace("\"", "\\\"") + "\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(psi))
                {
                    if (process == null) return false;
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);
                    return output.IndexOf("dynamically linked shared library", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception e)
            {
                ATLog.log("IsDynamicSharedLibrary() failed: " + binaryPath + " err=" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Embed dynamic frameworks with SOURCE_ROOT paths.
        /// Do NOT use PBXProject.AddFile / AddFileToEmbedFrameworks for Pods xcframeworks.
        /// </summary>
        private static void EmbedDynamicFrameworksWithSourceRoot(string buildPath, string projectPath)
        {
#if !UNITY_2019_3_OR_NEWER
            return;
#else
            if (!File.Exists(projectPath)) return;

            var toEmbed = CollectDynamicFrameworkPaths(buildPath)
                .Where(p => Directory.Exists(Path.Combine(buildPath, p)))
                .Distinct()
                .ToList();
            if (toEmbed.Count == 0)
            {
                ATLog.log("EmbedDynamicFrameworksWithSourceRoot() >>> nothing to embed yet; will rely on pod install post_install.");
                return;
            }

            var content = File.ReadAllText(projectPath);
            content = RemoveInvalidXcframeworkEmbedEntries(content, buildPath);

            var buildEntries = new StringBuilder();
            var fileRefEntries = new StringBuilder();
            var embedLines = new List<string>();
            int added = 0;

            foreach (var relPath in toEmbed)
            {
                var normalized = relPath.Replace('\\', '/');
                var name = Path.GetFileName(normalized);
                if (IsPathEmbeddedWithSourceRoot(content, normalized, name)) continue;

                var fileRefId = NewPbxId();
                var buildFileId = NewPbxId();
                fileRefEntries.Append("\t\t").Append(fileRefId)
                    .Append(" /* ").Append(name)
                    .Append(" */ = {isa = PBXFileReference; lastKnownFileType = wrapper.xcframework; name = ")
                    .Append(name).Append("; path = ").Append(normalized)
                    .Append("; sourceTree = SOURCE_ROOT; };\n");
                buildEntries.Append("\t\t").Append(buildFileId)
                    .Append(" /* ").Append(name).Append(" in Embed Frameworks */ = {isa = PBXBuildFile; fileRef = ")
                    .Append(fileRefId).Append(" /* ").Append(name)
                    .Append(" */; settings = {ATTRIBUTES = (CodeSignOnCopy, RemoveHeadersOnCopy, ); }; };\n");
                embedLines.Add("\t\t\t\t" + buildFileId + " /* " + name + " in Embed Frameworks */,");
                added++;
                ATLog.log("EmbedDynamicFrameworksWithSourceRoot() >>> embed: " + normalized);
            }

            if (added > 0)
            {
                if (fileRefEntries.Length > 0)
                {
                    content = content.Replace(
                        "/* End PBXFileReference section */",
                        fileRefEntries + "/* End PBXFileReference section */");
                }
                if (buildEntries.Length > 0)
                {
                    content = content.Replace(
                        "/* End PBXBuildFile section */",
                        buildEntries + "/* End PBXBuildFile section */");
                }

                content = Regex.Replace(
                    content,
                    @"(/\* Embed Frameworks \*/ = \{\s*isa = PBXCopyFilesBuildPhase;[\s\S]*?files = \()([\s\S]*?)(\);\s*name = ""Embed Frameworks"";)",
                    match =>
                    {
                        var existing = match.Groups[2].Value;
                        var injection = string.Join("\n", embedLines);
                        if (!injection.EndsWith("\n")) injection += "\n";
                        return match.Groups[1].Value + "\n" + injection + existing + match.Groups[3].Value;
                    },
                    RegexOptions.Multiline);
            }

            File.WriteAllText(projectPath, content);
            ATLog.log("EmbedDynamicFrameworksWithSourceRoot() >>> added " + added + " frameworks.");
#endif
        }

        private static bool IsPathEmbeddedWithSourceRoot(string content, string normalizedPath, string name)
        {
            var refMatches = Regex.Matches(
                content,
                @"([A-F0-9]{24}) /\* " + Regex.Escape(name) +
                @" \*/ = \{isa = PBXFileReference;[^}]*path = " +
                Regex.Escape(normalizedPath) +
                @";[^}]*sourceTree = SOURCE_ROOT;");
            foreach (Match rm in refMatches)
            {
                if (Regex.IsMatch(content, @"fileRef = " + rm.Groups[1].Value + @"[^\n]*in Embed Frameworks"))
                {
                    return true;
                }
            }
            return false;
        }

        private static string RemoveInvalidXcframeworkEmbedEntries(string content, string buildPath)
        {
            var phaseMatch = Regex.Match(
                content,
                @"/\* Embed Frameworks \*/ = \{\s*isa = PBXCopyFilesBuildPhase;[\s\S]*?files = \(([\s\S]*?)\);\s*name = ""Embed Frameworks"";");
            if (!phaseMatch.Success) return content;

            var lines = phaseMatch.Groups[1].Value.Split('\n');
            var kept = new List<string>();
            foreach (var line in lines)
            {
                var m = Regex.Match(line, @"([A-F0-9]{24}) /\* (.+?) in Embed Frameworks \*/");
                if (!m.Success)
                {
                    kept.Add(line);
                    continue;
                }

                var buildId = m.Groups[1].Value;
                var name = m.Groups[2].Value;
                if (name == "UnityFramework.framework")
                {
                    kept.Add(line);
                    continue;
                }

                var buildMatch = Regex.Match(
                    content,
                    buildId + @" /\* " + Regex.Escape(name) +
                    @" in Embed Frameworks \*/ = \{isa = PBXBuildFile; fileRef = ([A-F0-9]{24})");
                if (!buildMatch.Success) continue;

                var fileRef = buildMatch.Groups[1].Value;
                var refMatch = Regex.Match(
                    content,
                    fileRef + @" /\* [^*]+ \*/ = \{isa = PBXFileReference;([^}]*)\};");
                if (!refMatch.Success) continue;

                var meta = refMatch.Groups[1].Value;
                var pathMatch = Regex.Match(meta, @"path = ([^;]+);");
                var treeMatch = Regex.Match(meta, @"sourceTree = ([^;]+);");
                if (!pathMatch.Success || !treeMatch.Success) continue;

                var rel = pathMatch.Groups[1].Value.Trim().Trim('"');
                var tree = treeMatch.Groups[1].Value.Trim();
                var exists = Directory.Exists(Path.Combine(buildPath, rel)) ||
                             File.Exists(Path.Combine(buildPath, rel));

                // Only SOURCE_ROOT + existing path. <group> under Frameworks => Frameworks/Pods/...
                if (tree == "SOURCE_ROOT" && exists)
                {
                    kept.Add(line);
                }
                else
                {
                    ATLog.log("RemoveInvalidXcframeworkEmbedEntries() >>> drop: " + rel + " tree=" + tree);
                }
            }

            var newFiles = string.Join("\n", kept);
            return content.Substring(0, phaseMatch.Groups[1].Index) + newFiles +
                   content.Substring(phaseMatch.Groups[1].Index + phaseMatch.Groups[1].Length);
        }

        private static string NewPbxId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 24).ToUpperInvariant();
        }

        private static void NormalizeXcodeProjectFile(string projectPath)
        {
            if (!File.Exists(projectPath)) return;

            var content = File.ReadAllText(projectPath);
            var original = content;

            content = Regex.Replace(
                content,
                @"IPHONEOS_DEPLOYMENT_TARGET = ([0-9.]+);",
                match =>
                {
                    float version;
                    if (float.TryParse(match.Groups[1].Value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out version)
                        && version < 15.0f)
                    {
                        return "IPHONEOS_DEPLOYMENT_TARGET = " + MinIosDeploymentTarget + ";";
                    }
                    return match.Value;
                });

            content = Regex.Replace(
                content,
                @"buildPhases = \((.*?)\);",
                match =>
                {
                    var lines = match.Value.Split('\n');
                    var deduped = new List<string>();
                    string prev = null;
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(prev) && line == prev && line.Trim().EndsWith(","))
                            continue;
                        deduped.Add(line);
                        prev = line;
                    }
                    return string.Join("\n", deduped);
                },
                RegexOptions.Singleline);

            if (content != original)
            {
                File.WriteAllText(projectPath, content);
                ATLog.log("NormalizeXcodeProjectFile() >>> deployment target / dedupe buildPhases.");
            }
        }

        private static string BuildPodfilePostInstallHook()
        {
            return "\n# " + PodfilePostInstallMarker + "\n" +
                   "post_install do |installer|\n" +
                   "  installer.pods_project.targets.each do |target|\n" +
                   "    target.build_configurations.each do |config|\n" +
                   "      current = config.build_settings['IPHONEOS_DEPLOYMENT_TARGET']\n" +
                   "      if current.nil? || current.to_f < 15.0\n" +
                   "        config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'\n" +
                   "      end\n" +
                   "    end\n" +
                   "  end\n" +
                   "\n" +
                   "end\n";
        }

        private static string FindEditorHelperScript(string fileName)
        {
            var candidates = new[]
            {
                Path.Combine(Application.dataPath, "ThinkupTpnPlugin/Script/Editor", fileName),
                Path.Combine(Application.dataPath, "ThinkupTpnPlugin/Script/Editor/iOS", fileName),
                Path.Combine(Application.dataPath, "TpnPlugin/Script/Editor", fileName),
                Path.Combine(Application.dataPath, "TpnPlugin/Script/Editor/iOS", fileName),
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            var token = Path.GetFileNameWithoutExtension(fileName);
            foreach (var guid in AssetDatabase.FindAssets(token))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith(fileName, StringComparison.Ordinal))
                {
                    return Path.GetFullPath(assetPath);
                }
            }
            return null;
        }

        private static void InstallDynamicPodsHelperScripts(string buildPath)
        {
            foreach (var fileName in new[] { EmbedScriptFileName, CopyScriptFileName })
            {
                var src = FindEditorHelperScript(fileName);
                if (string.IsNullOrEmpty(src) || !File.Exists(src))
                {
                    ATLog.logError("InstallDynamicPodsHelperScripts() >>> missing " + fileName);
                    continue;
                }
                var dest = Path.Combine(buildPath, fileName);
                File.Copy(src, dest, true);
                ATLog.log("InstallDynamicPodsHelperScripts() >>> " + dest);
            }
        }

        /// <summary>
        /// Embed Frameworks for Pods xcframeworks is unreliable; force-copy at build time.
        /// </summary>
        private static void EnsureCopyDynamicPodsBuildPhase(string projectPath)
        {
            if (!File.Exists(projectPath)) return;
            var content = File.ReadAllText(projectPath);
            // Rename legacy TopOn phase name if present.
            if (content.Contains("[TopOn] Copy Dynamic Pods"))
            {
                content = content.Replace("[TopOn] Copy Dynamic Pods", CopyBuildPhaseName);
                File.WriteAllText(projectPath, content);
                ATLog.log("EnsureCopyDynamicPodsBuildPhase() >>> renamed legacy TopOn phase to AT.");
                return;
            }
            if (content.Contains(CopyBuildPhaseName))
            {
                ATLog.log("EnsureCopyDynamicPodsBuildPhase() >>> already present.");
                return;
            }

            var phaseId = NewPbxId();
            var shellScript =
                "chmod +x \\\"${PROJECT_DIR}/copy_dynamic_pods.sh\\\"\\n" +
                "\\\"${PROJECT_DIR}/copy_dynamic_pods.sh\\\"\\n";

            var phase =
                "\t\t" + phaseId + " /* " + CopyBuildPhaseName + " */ = {\n" +
                "\t\t\tisa = PBXShellScriptBuildPhase;\n" +
                "\t\t\talwaysOutOfDate = 1;\n" +
                "\t\t\tbuildActionMask = 2147483647;\n" +
                "\t\t\tfiles = (\n" +
                "\t\t\t);\n" +
                "\t\t\tinputFileListPaths = (\n" +
                "\t\t\t);\n" +
                "\t\t\tinputPaths = (\n" +
                "\t\t\t);\n" +
                "\t\t\tname = \"" + CopyBuildPhaseName + "\";\n" +
                "\t\t\toutputFileListPaths = (\n" +
                "\t\t\t);\n" +
                "\t\t\toutputPaths = (\n" +
                "\t\t\t);\n" +
                "\t\t\trunOnlyForDeploymentPostprocessing = 0;\n" +
                "\t\t\tshellPath = /bin/sh;\n" +
                "\t\t\tshellScript = \"" + shellScript + "\";\n" +
                "\t\t\tshowEnvVarsInLog = 0;\n" +
                "\t\t};\n";

            if (!content.Contains("/* End PBXShellScriptBuildPhase section */"))
            {
                ATLog.logError("EnsureCopyDynamicPodsBuildPhase() >>> no ShellScript section.");
                return;
            }

            content = content.Replace(
                "/* End PBXShellScriptBuildPhase section */",
                phase + "/* End PBXShellScriptBuildPhase section */");

            // Prefer inserting after Embed Frameworks on Unity-iPhone.
            if (content.Contains("/* Embed Frameworks */,"))
            {
                content = content.Replace(
                    "/* Embed Frameworks */,\n",
                    "/* Embed Frameworks */,\n\t\t\t\t" + phaseId + " /* " + CopyBuildPhaseName + " */,\n");
            }
            else
            {
                ATLog.logError("EnsureCopyDynamicPodsBuildPhase() >>> Embed Frameworks phase not found.");
                return;
            }

            File.WriteAllText(projectPath, content);
            ATLog.log("EnsureCopyDynamicPodsBuildPhase() >>> added " + CopyBuildPhaseName);
        }

        private static void RunEmbedDynamicPodsScript(string buildPath)
        {
            var script = Path.Combine(buildPath, EmbedScriptFileName);
            if (!File.Exists(script) || !Directory.Exists(Path.Combine(buildPath, "Pods")))
            {
                ATLog.log("RunEmbedDynamicPodsScript() >>> skip (script or Pods missing).");
                return;
            }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/python3",
                    Arguments = "\"" + script.Replace("\"", "\\\"") + "\" \"" +
                                buildPath.Replace("\"", "\\\"") + "\"",
                    WorkingDirectory = buildPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(psi))
                {
                    if (process == null) return;
                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    if (!string.IsNullOrEmpty(stdout)) ATLog.log(stdout.Trim());
                    if (!string.IsNullOrEmpty(stderr)) ATLog.log(stderr.Trim());
                }
            }
            catch (Exception e)
            {
                ATLog.logError("RunEmbedDynamicPodsScript() >>> " + e.Message);
            }
        }

        /// <summary>
        /// Force Podfile <c>platform :ios</c> to <see cref="MinIosDeploymentTarget"/>.
        /// Must run before EDM <c>pod install</c> (priority 50). Facebook Adapter 6.22+ /
        /// FBAudienceNetwork require iOS 15.0+.
        /// </summary>
        private static void EnsurePodfilePlatform(string buildPath)
        {
            var podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath))
            {
                ATLog.log("EnsurePodfilePlatform() >>> Podfile not found, skip.");
                return;
            }

            var content = File.ReadAllText(podfilePath);
            var updated = EnsurePodfilePlatformContent(content);
            if (updated == content)
            {
                return;
            }

            File.WriteAllText(podfilePath, updated);
            ATLog.log("EnsurePodfilePlatform() >>> platform :ios, '" + MinIosDeploymentTarget + "'");
        }

        private static string EnsurePodfilePlatformContent(string content)
        {
            var platformLine = "platform :ios, '" + MinIosDeploymentTarget + "'";
            if (Regex.IsMatch(content, @"platform\s+:ios\s*,\s*'[^']+'"))
            {
                return Regex.Replace(content, @"platform\s+:ios\s*,\s*'[^']+'", platformLine);
            }

            // EDM sometimes omits platform; insert after source lines / at top.
            if (Regex.IsMatch(content, @"^source\s+", RegexOptions.Multiline))
            {
                return Regex.Replace(
                    content,
                    @"(source\s+[^\n]+\n(?:source\s+[^\n]+\n)*)",
                    "$1\n" + platformLine + "\n",
                    RegexOptions.Multiline);
            }

            return platformLine + "\n" + content;
        }

        private static void EnsurePodfilePostInstallFix(string buildPath)
        {
            var podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath))
            {
                ATLog.log("EnsurePodfilePostInstallFix() >>> Podfile not found, skip.");
                return;
            }

            var content = EnsurePodfilePlatformContent(File.ReadAllText(podfilePath));

            var hook = BuildPodfilePostInstallHook();
            // Strip legacy TopOn marker block before writing AT hook.
            content = Regex.Replace(
                content,
                @"# TOPON_UNITY_POST_INSTALL[\s\S]*?\nend\n?",
                "");
            if (content.Contains(PodfilePostInstallMarker))
            {
                content = Regex.Replace(
                    content,
                    @"# " + PodfilePostInstallMarker + @"[\s\S]*?\nend\n?",
                    hook.TrimStart() + "\n");
            }
            else if (Regex.IsMatch(content, @"^\s*post_install\s+do\s+\|installer\|", RegexOptions.Multiline))
            {
                content = Regex.Replace(
                    content,
                    @"post_install\s+do\s+\|installer\|[\s\S]*?\nend\n?",
                    hook.TrimStart() + "\n",
                    RegexOptions.Multiline);
            }
            else
            {
                content = content.TrimEnd() + "\n" + hook;
            }

            File.WriteAllText(podfilePath, content);
            ATLog.log("EnsurePodfilePostInstallFix() >>> Podfile post_install updated.");
        }

#if UNITY_2018_2_OR_NEWER
        // Google official sample App ID (demo / local build when Integration Manager is empty).
        private const string AdmobSampleAppIdIos = "ca-app-pub-3940256099942544~1458002511";

        private static void ConfigureAdmobAppIdentifier(PlistDocument plist)
        {
            if (!ATConfig.isNetworkInstalledByName("Admob", ATConfig.OS_IOS))
            {
                ATLog.log("addGoogleApplicationIdIfNeeded() >>> Admob not install.");
                return;
            }

            var appId = ATConfig.getAdmobAppIdByOs(ATConfig.OS_IOS);
            if (string.IsNullOrEmpty(appId) || !appId.StartsWith("ca-app-pub-"))
            {
                UnityEngine.Debug.LogWarning(
                    "[AT] AdMob App ID is not set in Integration Manager; using Google sample App ID for this build. " +
                    "Set a real App ID (ca-app-pub-...) before release.");
                appId = AdmobSampleAppIdIos;
            }

            plist.root.SetString("GADApplicationIdentifier", appId);
        }
#endif
    }
}
#endif
