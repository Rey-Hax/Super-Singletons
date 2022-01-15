using PhantasmicGames.SuperSingletons;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System;

using UnityObject = UnityEngine.Object;
using PhantasmicGames.Common;
using UnityEditor.Build;

namespace SuperSingletonsEditor
{
	[CustomEditor(typeof(ScriptableObjectSingleton<>), true)]
	[CanEditMultipleObjects]
	public class ScriptableObjectSingletonEditor : Editor
	{
		private Type m_Type;
		private string m_Name;
		private GUIContent m_Label;

		private string m_ConfigName;
		private UnityObject m_CurrentMainInstance;

		private void OnEnable()
		{
			m_Type = target.GetType();
			m_Name = m_Type.Name;
			m_Label = new GUIContent($"Main {m_Name}: ");
			m_ConfigName = GetConfigName(target as ScriptableObject);
			EditorBuildSettings.TryGetConfigObject(m_ConfigName, out m_CurrentMainInstance);
		}

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			if (!EditorUtility.IsPersistent(target))
				return;

			if (!serializedObject.isEditingMultipleObjects && target != m_CurrentMainInstance)
				DrawWarningGUI();
		}

		private void DrawWarningGUI()
		{
			EditorGUILayout.BeginVertical("Box");
			EditorGUILayout.HelpBox($"This '{m_Name}' is not set as the main '{m_Name}'.", MessageType.Warning);

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(m_Label, GUILayout.MaxWidth(GUIStyle.none.CalcSize(m_Label).x + 1f));
			using (new EditorGUI.DisabledScope(true))
				EditorGUILayout.ObjectField(m_CurrentMainInstance, m_Type, false);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			if (GUILayout.Button($"Set this as Main", GUILayout.MaxWidth(250)))
			{
				SetAsMainInstance(target as ScriptableObject, true);
				m_CurrentMainInstance = target;
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}

		public static bool IsScriptableObjectSingleton(ScriptableObject scriptableObject, out Type genericType)
		{
			return Utility.TypeInheritsGenericTypeDefinition(scriptableObject.GetType(), typeof(ScriptableObjectSingleton<>), out genericType);
		}

		public static string GetConfigName(ScriptableObject scriptableObjectSingleton)
		{
			if (Utility.TypeInheritsGenericTypeDefinition(scriptableObjectSingleton.GetType(), typeof(ScriptableObjectSingleton<>), out Type genericType))
			{
				return (string)genericType.GetProperty("s_ConfigName", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
			}
			throw new Exception($"The ScriptableObject does not inherit ScriptableObjectSingleton<TScriptableObject>!");
		}

		public static void SetAsMainInstance(ScriptableObject scriptableObjectSingleton, bool value)
		{
			var configName = GetConfigName(scriptableObjectSingleton);
			if(!Utility.TypeInheritsGenericTypeDefinition(scriptableObjectSingleton.GetType(), typeof(ScriptableObjectSingleton<>), out Type genericType))
				throw new Exception($"The ScriptableObject does not inherit ScriptableObjectSingleton<TScriptableObject>!");

			var isMainInstanceField = genericType.GetField("m_IsMainInstance", BindingFlags.NonPublic | BindingFlags.Instance);

			isMainInstanceField.SetValue(scriptableObjectSingleton, value);

			if (EditorBuildSettings.TryGetConfigObject(configName, out ScriptableObject currentMain))
			{
				if (value && scriptableObjectSingleton != currentMain)
					isMainInstanceField.SetValue(currentMain, false);
				else if (!value && scriptableObjectSingleton == currentMain)
					EditorBuildSettings.RemoveConfigObject(configName);
			}

			if (value)
				EditorBuildSettings.AddConfigObject(configName, scriptableObjectSingleton, true);
		}

		private class AssetPostprocessor : UnityEditor.AssetPostprocessor
		{
			private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFromPaths)
			{
				foreach (var assetPath in imported)
				{
					var scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

					if (scriptableObject == null || !IsScriptableObjectSingleton(scriptableObject, out Type type))
						continue;

					var markedAsMainInstance = (bool)type.GetField("m_IsMainInstance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scriptableObject);
					if (!markedAsMainInstance)
						continue;

					var configName = GetConfigName(scriptableObject);
					EditorBuildSettings.TryGetConfigObject(configName, out ScriptableObject currentMain);

					if (currentMain == null)
						EditorBuildSettings.AddConfigObject(configName, scriptableObject, true);
					else if(currentMain != scriptableObject)
						SetAsMainInstance(scriptableObject, false);
				}
			}
		}
	}
}