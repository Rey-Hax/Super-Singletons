//using System;
using UnityEngine;
using UnityEditor;
using PhantasmicGames.SuperSingletons;

namespace PhantasmicGames.SuperSingletonsEditor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(ScriptableObjectSingleton<>), true)]
	public class ScriptableObjectSingletonEditor : Editor
	{
		private System.Type m_Type;
		private string m_Name;
		private GUIContent m_Label;

		private string m_ConfigName;
		private Object m_CurrentMainInstance;

		private void OnEnable()
		{
			m_Type = target.GetType();
			m_Name = m_Type.Name;
			m_Label = new GUIContent($"Main '{m_Name}': ");
			m_ConfigName = SuperSingletonsEditor.GetConfigName(target);
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
				SuperSingletonsEditor.SetIsMain(target, target.GetType().BaseType, true);
				m_CurrentMainInstance = target;
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}
	}
}