using GameFrameX.Ad;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GameFrameX.Ad.Runtime;
using GameFrameX.Runtime;
using SysType = System.Type;

namespace GameFrameX.Editor
{
    [CustomEditor(typeof(ADComponent))]
    internal sealed class ADComponentInspector : GameFrameworkInspector
    {
        private SerializedProperty m_InstanceRoot;
        private SerializedProperty m_Timeout;
        private SerializedProperty m_ADHelperConfigs;

        private string[] m_ADHelperTypeNames;
        private List<string> m_SelectedTypeNames = new List<string>();

        // AgentHelper 类型列表（懒刷新）
        private string[] m_ADRequestAgentHelperTypeNames;
        private string[] m_ADPlayAgentHelperTypeNames;
        private bool m_AgentHelperTypesDirty = true;

        private static readonly string[] s_EmptyStringArray = new string[0];
        private static readonly Dictionary<string, string> s_AgentHelperDisplayNameCache = new Dictionary<string, string>();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.UpdateIfRequiredOrScript();

            ADComponent adc = (ADComponent)target;

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
            {
                // ── Instance Root ──
                EditorGUILayout.PropertyField(m_InstanceRoot);

                // ── Timeout ──
                m_Timeout.floatValue = EditorGUILayout.Slider("Timeout", m_Timeout.floatValue, 0f, 120f);

                // ── Helper Selection ──
                EditorGUILayout.Separator();
                GUILayout.Label("Available AD Helpers", EditorStyles.boldLabel);
                if (m_ADHelperTypeNames.Length > 0)
                {
                    EditorGUILayout.BeginVertical("box");
                    {
                        foreach (string typeName in m_ADHelperTypeNames)
                        {
                            bool selected = m_SelectedTypeNames.Contains(typeName);
                            bool newSelected = EditorGUILayout.ToggleLeft(GetDisplayName(typeName), selected);
                            if (newSelected != selected)
                            {
                                if (newSelected)
                                    AddADHelperConfig(typeName);
                                else
                                    RemoveADHelperConfig(typeName);
                                RefreshSelectedTypeNames();
                            }
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.HelpBox("No IADHelper implementation found.", MessageType.Warning);
                }

                // ── Per-Helper Config ──
                if (m_SelectedTypeNames.Count > 0)
                {
                    EditorGUILayout.Separator();
                    GUILayout.Label("Helper Settings", EditorStyles.boldLabel);

                    if (m_AgentHelperTypesDirty || m_ADRequestAgentHelperTypeNames == null || m_ADPlayAgentHelperTypeNames == null)
                    {
                        m_ADRequestAgentHelperTypeNames = GatherRequestAgentHelperTypeNames();
                        m_ADPlayAgentHelperTypeNames = GatherPlayAgentHelperTypeNames();
                        m_AgentHelperTypesDirty = false;
                    }

                    // 通过反射获取配置列表，避免 SerializedProperty 深层嵌套 NRE
                    var configsField = typeof(ADComponent).GetField("m_ADHelperConfigs",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var configsList = configsField?.GetValue(adc) as IList<ADHelperConfigItem>;

                    if (configsList != null)
                    {
                        for (int i = 0; i < configsList.Count; i++)
                        {
                            var config = configsList[i];
                            if (config.Helper == null) continue;

                            EditorGUILayout.BeginVertical("box");
                            {
                                EditorGUILayout.LabelField(GetDisplayName(config.Helper.GetType().FullName), EditorStyles.boldLabel);

                                // Helper 自身属性通过 SerializedProperty 绘制（保留 Inspector 交互）
                                if (i < m_ADHelperConfigs.arraySize)
                                {
                                    var configElem = m_ADHelperConfigs.GetArrayElementAtIndex(i);
                                    if (configElem != null)
                                    {
                                        var helperProp = configElem.FindPropertyRelative("Helper");
                                        if (helperProp?.managedReferenceValue != null)
                                        {
                                            var iter = helperProp.Copy();
                                            int depth = iter.depth;
                                            bool enterChildren = true;
                                            while (iter.NextVisible(enterChildren) && iter.depth > depth)
                                            {
                                                enterChildren = false;
                                                EditorGUILayout.PropertyField(iter, true);
                                            }
                                        }
                                    }
                                }

                                EditorGUILayout.Separator();
                                EditorGUILayout.LabelField("Request Agent Helpers", EditorStyles.boldLabel);

                                string[] requestTypeNames = m_ADRequestAgentHelperTypeNames ?? s_EmptyStringArray;
                                if (requestTypeNames.Length <= 1)
                                {
                                    EditorGUILayout.HelpBox("No IADRequestAgentHelper implementation found.", MessageType.Info);
                                }
                                else
                                {
                                    EditorGUILayout.BeginVertical("box");
                                    {
                                        for (int j = 1; j < requestTypeNames.Length; j++)
                                        {
                                            string typeName = requestTypeNames[j];
                                            bool selected = config.AgentConfigs != null &&
                                                config.AgentConfigs.Any(x => x.AgentHelperTypeName == typeName);
                                            bool newSelected = EditorGUILayout.ToggleLeft(GetAgentHelperDisplayName(typeName), selected);
                                            if (newSelected != selected)
                                            {
                                                serializedObject.ApplyModifiedProperties();
                                                Undo.RecordObject(adc, "Change AD Request Agent Helpers");
                                                if (newSelected)
                                                {
                                                    AddAgentConfig(config, typeName);
                                                }
                                                else
                                                {
                                                    RemoveAgentConfig(config, typeName);
                                                }

                                                EditorUtility.SetDirty(adc);
                                                serializedObject.Update();
                                            }
                                        }
                                    }
                                    EditorGUILayout.EndVertical();
                                }

                                EditorGUILayout.Separator();
                                EditorGUILayout.LabelField("Play Agent Helpers", EditorStyles.boldLabel);

                                string[] playTypeNames = m_ADPlayAgentHelperTypeNames ?? s_EmptyStringArray;
                                if (playTypeNames.Length <= 1)
                                {
                                    EditorGUILayout.HelpBox("No IADPlayAgentHelper implementation found.", MessageType.Info);
                                }
                                else
                                {
                                    EditorGUILayout.BeginVertical("box");
                                    {
                                        for (int j = 1; j < playTypeNames.Length; j++)
                                        {
                                            string typeName = playTypeNames[j];
                                            bool selected = config.PlayAgentConfigs != null &&
                                                config.PlayAgentConfigs.Any(x => x.AgentHelperTypeName == typeName);
                                            bool newSelected = EditorGUILayout.ToggleLeft(GetAgentHelperDisplayName(typeName), selected);
                                            if (newSelected != selected)
                                            {
                                                serializedObject.ApplyModifiedProperties();
                                                Undo.RecordObject(adc, "Change AD Play Agent Helpers");
                                                if (newSelected)
                                                {
                                                    AddPlayAgentConfig(config, typeName);
                                                }
                                                else
                                                {
                                                    RemovePlayAgentConfig(config, typeName);
                                                }

                                                EditorUtility.SetDirty(adc);
                                                serializedObject.Update();
                                            }
                                        }
                                    }
                                    EditorGUILayout.EndVertical();
                                }
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            // ── 运行时状态 ──
            if (EditorApplication.isPlaying && IsPrefabInHierarchy(adc.gameObject))
            {
                EditorGUILayout.Separator();
                GUILayout.Label("Runtime Status", EditorStyles.boldLabel);

                // TaskPool 统计
                for (int i = 0; i < m_ADHelperConfigs.arraySize; i++)
                {
                    var helper = m_ADHelperConfigs.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("Helper").managedReferenceValue as IADHelper;
                    if (helper == null) continue;
                    string platformName = helper.Name;
                    foreach (ADFormat fmt in Enum.GetValues(typeof(ADFormat)))
                    {
                        if (adc.GetTaskPoolInfo(platformName, fmt, out int f, out int w, out int t))
                        {
                            EditorGUILayout.BeginVertical("box");
                            EditorGUILayout.LabelField($"[{platformName}] {fmt}", $"Free={f}  Working={w}  Waiting={t}");
                            EditorGUILayout.EndVertical();
                        }
                    }
                }

                // PlacementID 广告加载状态
                EditorGUILayout.Separator();
                GUILayout.Label("Ad Placement Status", EditorStyles.boldLabel);

                var allStatuses = adc.GetAllAdStatuses();
                if (allStatuses.Count == 0)
                {
                    EditorGUILayout.HelpBox("No placement IDs have been requested yet.", MessageType.Info);
                }
                else
                {
                    foreach (var platformKvp in allStatuses)
                    {
                        string platformName = platformKvp.Key;
                        EditorGUILayout.BeginVertical("box");
                        {
                            GUILayout.Label($"[{platformName}]", EditorStyles.boldLabel);
                            foreach (var formatKvp in platformKvp.Value)
                            {
                                ADFormat format = formatKvp.Key;
                                var placements = formatKvp.Value;
                                if (placements.Count == 0) continue;

                                EditorGUILayout.LabelField($"  {format}:", EditorStyles.miniBoldLabel);
                                EditorGUI.indentLevel++;
                                foreach (var info in placements)
                                {
                                    EditorGUILayout.BeginVertical("helpbox");
                                    {
                                        EditorGUILayout.LabelField("PlacementID", info.PlacementID);
                                        string status = string.IsNullOrEmpty(info.Status) ? "(empty)" : info.Status;
                                        EditorGUILayout.LabelField("Status", status, EditorStyles.wordWrappedLabel);
                                    }
                                    EditorGUILayout.EndVertical();
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed)
                EditorUtility.SetDirty(adc);
            Repaint();
        }

        private void OnEnable()
        {
            m_InstanceRoot = serializedObject.FindProperty("m_InstanceRoot");
            m_Timeout = serializedObject.FindProperty("m_Timeout");
            m_ADHelperConfigs = serializedObject.FindProperty("m_ADHelperConfigs");

            RefreshTypeNames();
        }

        private static string[] GatherRequestAgentHelperTypeNames()
        {
            var list = new List<string> { "<None>" };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                SysType[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t.IsClass && !t.IsAbstract && typeof(IADRequestAgentHelper).IsAssignableFrom(t) &&
                        typeof(MonoBehaviour).IsAssignableFrom(t))
                    {
                        list.Add(t.FullName);
                    }
                }
            }
            return list.ToArray();
        }

        private static string[] GatherPlayAgentHelperTypeNames()
        {
            var list = new List<string> { "<None>" };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                SysType[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t.IsClass && !t.IsAbstract && typeof(IADPlayAgentHelper).IsAssignableFrom(t) &&
                        typeof(MonoBehaviour).IsAssignableFrom(t))
                    {
                        list.Add(t.FullName);
                    }
                }
            }
            return list.ToArray();
        }

        protected override void OnCompileComplete()
        {
            base.OnCompileComplete();
            s_AgentHelperDisplayNameCache.Clear();
            m_AgentHelperTypesDirty = true;
            RefreshTypeNames();
        }

        private void RefreshTypeNames()
        {
            m_ADHelperTypeNames = TypeHelper.GetRuntimeTypeNames(typeof(IADHelper));
            RefreshSelectedTypeNames();
        }

        private void RefreshSelectedTypeNames()
        {
            m_SelectedTypeNames.Clear();

            ADComponent adc = (ADComponent)target;
            var configsField = typeof(ADComponent).GetField("m_ADHelperConfigs",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var configsList = configsField?.GetValue(adc) as IList<ADHelperConfigItem>;

            if (configsList != null)
            {
                foreach (var c in configsList)
                {
                    if (c.Helper != null)
                        m_SelectedTypeNames.Add(c.Helper.GetType().FullName);
                }
            }

            // 清理无效配置
            int oldCount = m_SelectedTypeNames.Count;
            m_SelectedTypeNames = m_SelectedTypeNames.Where(x => m_ADHelperTypeNames.Contains(x)).ToList();
            if (m_SelectedTypeNames.Count != oldCount)
            {
                CleanupInvalidConfigs();
                RefreshSelectedTypeNames();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void CleanupInvalidConfigs()
        {
            for (int i = m_ADHelperConfigs.arraySize - 1; i >= 0; i--)
            {
                var configElem = m_ADHelperConfigs.GetArrayElementAtIndex(i);
                var helperProp = configElem.FindPropertyRelative("Helper");
                if (helperProp?.managedReferenceValue == null ||
                    !m_ADHelperTypeNames.Contains(helperProp.managedReferenceValue.GetType().FullName))
                {
                    m_ADHelperConfigs.DeleteArrayElementAtIndex(i);
                }
            }
        }

        private void AddADHelperConfig(string typeName)
        {
            SysType type = GetTypeByName(typeName);
            if (type == null)
            {
                Debug.LogError($"[ADComponentInspector] Cannot find type: {typeName}");
                return;
            }

            object instance = Activator.CreateInstance(type);

            m_ADHelperConfigs.arraySize++;
            SerializedProperty newElem = m_ADHelperConfigs.GetArrayElementAtIndex(m_ADHelperConfigs.arraySize - 1);
            SerializedProperty helperProp = newElem.FindPropertyRelative("Helper");
            helperProp.managedReferenceValue = instance;
            newElem.FindPropertyRelative("AgentConfigs").ClearArray();
            newElem.FindPropertyRelative("PlayAgentConfigs").ClearArray();
            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveADHelperConfig(string typeName)
        {
            for (int i = m_ADHelperConfigs.arraySize - 1; i >= 0; i--)
            {
                var configElem = m_ADHelperConfigs.GetArrayElementAtIndex(i);
                var helperProp = configElem.FindPropertyRelative("Helper");
                if (helperProp?.managedReferenceValue != null &&
                    helperProp.managedReferenceValue.GetType().FullName == typeName)
                {
                    m_ADHelperConfigs.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static void AddAgentConfig(ADHelperConfigItem config, string typeName)
        {
            if (config.AgentConfigs == null)
            {
                config.AgentConfigs = new List<ADRequestAgentConfigItem>();
            }

            if (config.AgentConfigs.Any(x => x.AgentHelperTypeName == typeName))
            {
                return;
            }

            config.AgentConfigs.Add(new ADRequestAgentConfigItem
            {
                AgentHelperTypeName = typeName,
            });
        }

        private static void RemoveAgentConfig(ADHelperConfigItem config, string typeName)
        {
            if (config.AgentConfigs == null)
            {
                return;
            }

            for (int i = config.AgentConfigs.Count - 1; i >= 0; i--)
            {
                if (config.AgentConfigs[i].AgentHelperTypeName == typeName)
                {
                    config.AgentConfigs.RemoveAt(i);
                }
            }
        }

        private static void AddPlayAgentConfig(ADHelperConfigItem config, string typeName)
        {
            if (config.PlayAgentConfigs == null)
            {
                config.PlayAgentConfigs = new List<ADPlayAgentConfigItem>();
            }

            if (config.PlayAgentConfigs.Any(x => x.AgentHelperTypeName == typeName))
            {
                return;
            }

            config.PlayAgentConfigs.Add(new ADPlayAgentConfigItem
            {
                AgentHelperTypeName = typeName,
            });
        }

        private static void RemovePlayAgentConfig(ADHelperConfigItem config, string typeName)
        {
            if (config.PlayAgentConfigs == null)
            {
                return;
            }

            for (int i = config.PlayAgentConfigs.Count - 1; i >= 0; i--)
            {
                if (config.PlayAgentConfigs[i].AgentHelperTypeName == typeName)
                {
                    config.PlayAgentConfigs.RemoveAt(i);
                }
            }
        }

        private static SysType GetTypeByName(string typeName)
        {
            SysType type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) break;
            }
            return type;
        }

        private static string GetAgentHelperDisplayName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return GetDisplayName(fullTypeName);
            }

            if (s_AgentHelperDisplayNameCache.TryGetValue(fullTypeName, out string displayName))
            {
                return displayName;
            }

            displayName = GetDisplayName(fullTypeName);
            SysType type = GetTypeByName(fullTypeName);
            if (type != null)
            {
                try
                {
                    GameObject temp = new GameObject("AD Agent Helper Preview");
                    temp.hideFlags = HideFlags.HideAndDontSave;
                    try
                    {
                        Component component = temp.AddComponent(type);
                        if (component is IADRequestAgentHelper requestAgentHelper)
                        {
                            displayName = $"{displayName} ({requestAgentHelper.SupportedFormat})";
                        }
                        else if (component is IADPlayAgentHelper playAgentHelper)
                        {
                            displayName = $"{displayName} ({playAgentHelper.SupportedFormat})";
                        }
                    }
                    finally
                    {
                        DestroyImmediate(temp);
                    }
                }
                catch
                {
                    // Keep the plain type name if the helper cannot be instantiated for display purposes.
                }
            }

            s_AgentHelperDisplayNameCache[fullTypeName] = displayName;
            return displayName;
        }

        private static string GetDisplayName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return "Unknown";

            int lastDot = fullTypeName.LastIndexOf('.');
            string shortName = lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;

            if (shortName.EndsWith("ADHelper"))
                shortName = shortName.Substring(0, shortName.Length - "ADHelper".Length);

            return shortName;
        }
    }
}
