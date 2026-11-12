#if UNITY_ANDROID && UNITY_2018_2_OR_NEWER
using AnyThink.Scripts.IntegrationManager.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;

namespace AnyThink.Scripts.Editor
{
    public class ATPostProcessBuildAndroid: IPostGenerateGradleAndroidProject
    {
#if UNITY_2019_3_OR_NEWER
        private static string PropertyAndroidX = "android.useAndroidX";
        private static string PropertyJetifier = "android.enableJetifier";
        private static string EnableProperty = "=true";
#endif
        // AGP < 8.3: disable dexing artifact transform (historical ExoPlayer workaround).
        // AGP >= 8.3: the option was removed; strip it and write nothing in its place.
        private static string PropertyDexingArtifactTransform = "android.enableDexingArtifactTransform";
        private static string DisableProperty = "=false";
        private static readonly Version AgpDexingArtifactTransformRemovedVersion = new Version(8, 3);

        private static string KeyMetaDataGoogleApplicationId = "com.google.android.gms.ads.APPLICATION_ID";
        private static string KeyMetaDataGoogleAdManagerApp = "com.google.android.gms.ads.AD_MANAGER_APP";

        private static readonly XNamespace AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace ToolsNamespace = "http://schemas.android.com/tools";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
             ATLog.log("OnPostGenerateGradleAndroidProject() >>> path: " + path);
           
#if UNITY_2019_3_OR_NEWER
            var gradlePropertiesPath = Path.Combine(path, "../gradle.properties");
#else
            var gradlePropertiesPath = Path.Combine(path, "gradle.properties");
#endif
            if (!ATConfig.isDefaultAndroidX()) {
                processGradleProperties(gradlePropertiesPath, path);
            }
            processAndroidManifest(path);
            processNetworkConfigXml(path);
            ATProcessBuildGradleAndroid.processBuildGradle(path);
        }

        public int callbackOrder
        {
            get { return int.MaxValue; }
        }

        /// <summary>
        /// Key for a <c>key=value</c> line in gradle.properties, or <c>null</c> if the line is not a replaceable property (comment, blank, no '=').
        /// </summary>
        private static string TryGetGradlePropertyKey(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;
            var trimmedStart = line.TrimStart();
            if (trimmedStart.Length == 0)
                return null;
            if (trimmedStart[0] == '#')
                return null;
            var eq = trimmedStart.IndexOf('=');
            if (eq < 0)
                return null;
            return trimmedStart.Substring(0, eq).Trim();
        }

        private static bool IsManagedGradlePropertyKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
#if UNITY_2019_3_OR_NEWER
            return key == PropertyAndroidX || key == PropertyJetifier || key == PropertyDexingArtifactTransform;
#else
            return key == PropertyDexingArtifactTransform;
#endif
        }

        /// <summary>
        /// Reads AGP version from root build.gradle classpath. Returns null if unavailable.
        /// </summary>
        private static Version TryGetAndroidGradlePluginVersion(string unityLibraryPath)
        {
            try
            {
#if UNITY_2019_3_OR_NEWER
                var buildGradlePath = Path.GetFullPath(Path.Combine(unityLibraryPath, "..", "build.gradle"));
#else
                var buildGradlePath = Path.Combine(unityLibraryPath, "build.gradle");
#endif
                if (!File.Exists(buildGradlePath))
                {
                    ATLog.log("[AnyThink] AGP version detect skipped, build.gradle missing: " + buildGradlePath);
                    return null;
                }

                var text = File.ReadAllText(buildGradlePath);
                var match = Regex.Match(text, @"com\.android\.tools\.build:gradle:(\d+\.\d+(?:\.\d+)?)");
                if (!match.Success)
                    return null;

                var versionText = match.Groups[1].Value;
                if (versionText.Split('.').Length == 2)
                    versionText += ".0";
                return new Version(versionText);
            }
            catch (Exception exception)
            {
                ATLog.log("[AnyThink] AGP version detect failed: " + exception.Message);
                return null;
            }
        }

        private static bool ShouldUseLegacyDexingArtifactTransformProperty(Version agpVersion)
        {
            // Unknown AGP -> keep original behavior to avoid changing older Gradle setups unexpectedly.
            // Known AGP >= 8.3 -> do not write the removed property.
            return agpVersion == null || agpVersion < AgpDexingArtifactTransformRemovedVersion;
        }

        private static void processGradleProperties(string gradlePropertiesPath, string unityLibraryPath)
        {
            ATLog.log("OnPostGenerateGradleAndroidProject() >>> gradlePropertiesPath: " + gradlePropertiesPath + " File.Exists(gradlePropertiesPath): " + File.Exists(gradlePropertiesPath));
            bool isChina = ATConfig.isSelectedChina();
            var agpVersion = TryGetAndroidGradlePluginVersion(unityLibraryPath);
            bool useLegacyDexingProperty = ShouldUseLegacyDexingArtifactTransformProperty(agpVersion);
            ATLog.log("[AnyThink] AGP version: " + (agpVersion != null ? agpVersion.ToString() : "unknown")
                + ", useLegacyDexingArtifactTransform=" + useLegacyDexingProperty);
         
            var gradlePropertiesUpdated = new List<string>();

            if (File.Exists(gradlePropertiesPath))
            {
                var lines = File.ReadAllLines(gradlePropertiesPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        gradlePropertiesUpdated.Add(line);
                        continue;
                    }

                    var trimmedForComment = line.TrimStart();
                    if (trimmedForComment.Length > 0 && trimmedForComment[0] == '#')
                    {
                        gradlePropertiesUpdated.Add(line);
                        continue;
                    }

                    var key = TryGetGradlePropertyKey(line);
                    if (key != null && IsManagedGradlePropertyKey(key))
                        continue;

                    gradlePropertiesUpdated.Add(line);
                }
            }

#if UNITY_2019_3_OR_NEWER
            //如果是国内，则根据选择来决定是否用AndroidX
            if (isChina)    
            {
                if (!ATConfig.enableAndroidX()) {
                    EnableProperty = "=false"; 
                } else {
                    EnableProperty = "=true"; 
                }
            } else {
                EnableProperty = "=true"; 
            }
            ATLog.log("[AnyThink] AndroidX EnableProperty" + EnableProperty);
            // Enable AndroidX and Jetifier properties 
            gradlePropertiesUpdated.Add(PropertyAndroidX + EnableProperty);
            gradlePropertiesUpdated.Add(PropertyJetifier + EnableProperty);
#endif
            // AGP < 8.3 (or unknown): keep legacy disable. AGP >= 8.3: property already stripped above, write nothing.
            if (useLegacyDexingProperty)
            {
                // Disable dexing using artifact transform (it causes issues for ExoPlayer with Gradle plugin 3.5.0+)
                gradlePropertiesUpdated.Add(PropertyDexingArtifactTransform + DisableProperty);
            }

            try
            {
                File.WriteAllText(gradlePropertiesPath, string.Join("\n", gradlePropertiesUpdated.ToArray()) + "\n");
            }
            catch (Exception exception)
            {
                ATLog.logError("Failed to enable AndroidX and Jetifier. gradle.properties file write failed.");
                Console.WriteLine(exception);
            }
        }

        private static void processAndroidManifest(string path)
        {
#if UNITY_2019_3_OR_NEWER
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
#else
            var manifestPath = Path.Combine(path, "unityLibrary/src/main/AndroidManifest.xml");
#endif
            // var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            XDocument manifest;
            try
            {
                manifest = XDocument.Load(manifestPath);
            }
#pragma warning disable 0168
            catch (IOException exception)
#pragma warning restore 0168
            {
                ATLog.log("[AnyThink] AndroidManifest.xml is missing.");
                return;
            }

            // Get the `manifest` element.
            var elementManifest = manifest.Element("manifest");
            if (elementManifest == null)
            {
                ATLog.log("[AnyThink] AndroidManifest.xml is invalid.");
                return;
            }

            var elementApplication = elementManifest.Element("application");
            if (elementApplication == null)
            {
                ATLog.log("[AnyThink] AndroidManifest.xml is invalid.");
                return;
            }

            var metaDataElements = elementApplication.Descendants().Where(element => element.Name.LocalName.Equals("meta-data"));
            addGoogleApplicationIdIfNeeded(elementApplication, metaDataElements);
            // Honor (com.hihonor.mcs:ads-base) + Oppo (com.thinkup.sdk:sdk-ads-oppo) <queries> provider merge.
            EnsureMediationQueriesProviderMergeOverrides(elementManifest);
            // Save the updated manifest file.
            manifest.Save(manifestPath);
        }

        private static void addGoogleApplicationIdIfNeeded(XElement elementApplication, IEnumerable<XElement> metaDataElements)
        {
            var googleApplicationIdMetaData = GetElementByName(metaDataElements, KeyMetaDataGoogleApplicationId);

            if (!ATConfig.isNetworkInstalledByName("Admob", ATConfig.OS_ANDROID))
            {   
                ATLog.log("addGoogleApplicationIdIfNeeded() >>> Admob not install.");
//                if (googleApplicationIdMetaData != null) googleApplicationIdMetaData.Remove();
                return;
            }

            var appId = ATConfig.getAdmobAppIdByOs(ATConfig.OS_ANDROID);
            // Fallback to Google sample App ID so local/demo builds are not blocked.
            if (string.IsNullOrEmpty(appId) || !appId.StartsWith("ca-app-pub-"))
            {
                UnityEngine.Debug.LogWarning(
                    "[AT] AdMob App ID is not set in Integration Manager; using Google sample App ID for this build. " +
                    "Set a real App ID (ca-app-pub-...) before release.");
                appId = "ca-app-pub-3940256099942544~3347511713";
            }

            // Check if the Google App ID meta data already exists. Update if it already exists.
            if (googleApplicationIdMetaData != null)
            {
                googleApplicationIdMetaData.SetAttributeValue(AndroidNamespace + "value", appId);
            }
            // Meta data doesn't exist, add it.
            else
            {
                elementApplication.Add(CreateMetaDataElement(KeyMetaDataGoogleApplicationId, appId));
            }
        }

        /// <summary>
        /// Looks through all the given meta-data elements to check if the required one exists. Returns <c>null</c> if it doesn't exist.
        /// </summary>
        private static XElement GetElementByName(IEnumerable<XElement> elements, string name)
        {
            foreach (var element in elements)
            {
                var attributes = element.Attributes();
                if (attributes.Any(attribute => attribute.Name.Namespace.Equals(AndroidNamespace)
                                                && attribute.Name.LocalName.Equals("name")
                                                && attribute.Value.Equals(name)))
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates and returns a <c>meta-data</c> element with the given name and value. 
        /// </summary>
        private static XElement CreateMetaDataElement(string name, object value)
        {
            var metaData = new XElement("meta-data");
            metaData.Add(new XAttribute(AndroidNamespace + "name", name));
            metaData.Add(new XAttribute(AndroidNamespace + "value", value));

            return metaData;
        }

        private static void processNetworkConfigXml(string path)
        {   
            bool isChina = ATConfig.isSelectedChina();
            // bool isChina = true;
            
            //在application标签加上：android:networkSecurityConfig="@xml/thinkup_network_security_config"
            addNetworkSecurityConfigInApplication(path, isChina);

#if UNITY_2019_3_OR_NEWER
            var resXmlPath = Path.Combine(path, "src/main/res/xml");
#else
            var resXmlPath = Path.Combine(path, "unityLibrary/src/main/res/xml");
#endif
            
            var rexXmlDir = Path.Combine(resXmlPath, "thinkup_network_security_config.xml");
            if (File.Exists(rexXmlDir))
            { 
                if (!isChina)   //海外不用配置这个xml
                {
                   FileUtil.DeleteFileOrDirectory(rexXmlDir);
                } 
                return;
            }
            if (!Directory.Exists(resXmlPath))
            {
                Directory.CreateDirectory(resXmlPath);
            }
            
            saveFile("Assets/ThinkupTpnPlugin/Script/Editor/thinkup_network_security_config.xml", resXmlPath);
        }

        public static void saveFile(string filePathName , string toFilesPath)
        {
            FileInfo file = new FileInfo(filePathName);
            string newFileName= file.Name;
            file.CopyTo(toFilesPath + "/" + newFileName, true);
        }

        private static void addNetworkSecurityConfigInApplication(string path, bool isChina)
        {
#if UNITY_2019_3_OR_NEWER
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
#else
            var manifestPath = Path.Combine(path, "unityLibrary/src/main/AndroidManifest.xml");
#endif
            // var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            XDocument manifest;
            try
            {
                manifest = XDocument.Load(manifestPath);
            }
#pragma warning disable 0168
            catch (IOException exception)
#pragma warning restore 0168
            {
                ATLog.log("[AnyThink] AndroidManifest.xml is missing.");
                return;
            }

            // Get the `manifest` element.
            var elementManifest = manifest.Element("manifest");
            if (elementManifest == null)
            {
                ATLog.log("[AnyThink] AndroidManifest.xml is invalid.");
                return;
            }

            var elementApplication = elementManifest.Element("application");
            if (elementApplication == null)
            {
                ATLog.log("[AnyThink] AndroidManifest.xml is invalid.");
                return;
            }
            //handle thinkup_network_security_config.xml
            if (isChina)
            {
                RemoveAllAndroidNetworkSecurityConfigAttributes(elementApplication);
            }
            else if (elementApplication.Attribute(AndroidNamespace + "networkSecurityConfig") == null)
            {
                RemoveApplicationToolsReplaceToken(elementApplication, "android:networkSecurityConfig");
            }
            if (isChina)
            {
                EnsureManifestHasToolsNamespace(elementManifest);
                elementApplication.Add(new XAttribute(AndroidNamespace + "networkSecurityConfig", "@xml/thinkup_network_security_config"));
                // Other SDKs may declare their own networkSecurityConfig; force ours at merge time.
                MergeApplicationToolsReplace(elementApplication, "android:networkSecurityConfig");
            }

            //这个设置主要是为了适配9.0以上的机器
            //<uses-library android:name="org.apache.http.legacy" android:required="false" />
            var usesLibraryElements = elementApplication.Descendants().Where(element => element.Name.LocalName.Equals("uses-library"));
            if (usesLibraryElements == null)
            {
                elementApplication.Add(createHttpLegacyElement());
            }
            else 
            {
               XElement httpLegacyElement = GetElementByName(usesLibraryElements, "org.apache.http.legacy");
               if (httpLegacyElement == null)
               {
                    elementApplication.Add(createHttpLegacyElement());
               }
            }
            manifest.Save(manifestPath);
        }

        private static void EnsureManifestHasToolsNamespace(XElement elementManifest)
        {
            var toolsXmlns = XNamespace.Xmlns + "tools";
            if (elementManifest.Attribute(toolsXmlns) == null)
            {
                elementManifest.Add(new XAttribute(toolsXmlns, ToolsNamespace.NamespaceName));
            }
        }

        private static void MergeApplicationToolsReplace(XElement elementApplication, string androidAttrFullName)
        {
            var replaceAttr = elementApplication.Attribute(ToolsNamespace + "replace");
            if (replaceAttr == null)
            {
                elementApplication.Add(new XAttribute(ToolsNamespace + "replace", androidAttrFullName));
                return;
            }

            var parts = replaceAttr.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (parts.Contains(androidAttrFullName))
            {
                return;
            }

            parts.Add(androidAttrFullName);
            replaceAttr.Value = string.Join(",", parts.ToArray());
        }

        /// <summary>
        /// Removes all android namespace attributes whose local name ends with "networkSecurityConfig"
        /// (covers <c>networkSecurityConfig</c> and bad typos such as <c>qwgnetworkSecurityConfig</c>).
        /// </summary>
        private static void RemoveAllAndroidNetworkSecurityConfigAttributes(XElement elementApplication)
        {
            const string suffix = "networkSecurityConfig";
            foreach (var attr in elementApplication.Attributes().ToList())
            {
                if (!attr.Name.Namespace.Equals(AndroidNamespace))
                {
                    continue;
                }

                if (attr.Name.LocalName.EndsWith(suffix, StringComparison.Ordinal))
                {
                    attr.Remove();
                }
            }
        }

        private static void RemoveApplicationToolsReplaceToken(XElement elementApplication, string androidAttrFullName)
        {
            var replaceAttr = elementApplication.Attribute(ToolsNamespace + "replace");
            if (replaceAttr == null)
            {
                return;
            }

            var parts = replaceAttr.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (!parts.Contains(androidAttrFullName))
            {
                return;
            }

            parts.Remove(androidAttrFullName);

            if (parts.Count == 0)
            {
                replaceAttr.Remove();
            }
            else
            {
                replaceAttr.Value = string.Join(",", parts.ToArray());
            }
        }

        /// <summary>
        /// Resolves manifest merger conflict between com.hihonor.mcs:ads-base and com.thinkup.sdk:sdk-ads-oppo
        /// over <c>&lt;queries&gt;&lt;provider android:authorities&gt;</c> (Honor names its provider; Oppo may declare authorities only).
        /// </summary>
        private static void EnsureMediationQueriesProviderMergeOverrides(XElement elementManifest)
        {
            const string honorProviderClass = "com.hihonor.id.router.RouterContentProvider";
            const string honorAuthorities = "com.hihonor.id.router.routercontentprovider";
            const string oplusAuthorities = "com.oplus.statistics.provider";

            EnsureManifestHasToolsNamespace(elementManifest);

            XElement queries = elementManifest.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("queries"));
            if (queries == null)
            {
                queries = new XElement("queries");
                var application = elementManifest.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("application"));
                if (application != null)
                {
                    application.AddBeforeSelf(queries);
                }
                else
                {
                    elementManifest.AddFirst(queries);
                }
            }

            void ensureHonorProvider()
            {
                if (queries.Elements().Any(e => e.Name.LocalName.Equals("provider")
                        && (string)e.Attribute(AndroidNamespace + "name") == honorProviderClass))
                {
                    return;
                }
                var p = new XElement("provider");
                p.Add(new XAttribute(AndroidNamespace + "name", honorProviderClass));
                p.Add(new XAttribute(AndroidNamespace + "authorities", honorAuthorities));
                p.Add(new XAttribute(ToolsNamespace + "replace", "android:authorities"));
                queries.Add(p);
            }

            void ensureOplusProvider()
            {
                if (queries.Elements().Any(e => e.Name.LocalName.Equals("provider")
                        && (string)e.Attribute(AndroidNamespace + "authorities") == oplusAuthorities))
                {
                    return;
                }
                var p = new XElement("provider");
                p.Add(new XAttribute(AndroidNamespace + "authorities", oplusAuthorities));
                p.Add(new XAttribute(ToolsNamespace + "replace", "android:authorities"));
                queries.Add(p);
            }

            ensureHonorProvider();
            ensureOplusProvider();
        }

        public static XElement createHttpLegacyElement()
        {
            var httpFeautre = new XElement("uses-library");
            httpFeautre.Add(new XAttribute(AndroidNamespace + "name", "org.apache.http.legacy"));
            httpFeautre.Add(new XAttribute(AndroidNamespace + "required", "false"));

            return httpFeautre;
        }

        private static XElement CreateMetaDataElement(string name, object value, object toolsNode)
        {
            var metaData = new XElement("meta-data");
            metaData.Add(new XAttribute(AndroidNamespace + "name", name));
            metaData.Add(new XAttribute(AndroidNamespace + "value", value));
            metaData.Add(new XAttribute(ToolsNamespace + "node", toolsNode));

            return metaData;
        }
    }
    
}

#endif