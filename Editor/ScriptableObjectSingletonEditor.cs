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
		private Object m_CurrentMain;

		private void OnEnable()
		{
			m_Type = target.GetType();
			m_Name = m_Type.Name;
			m_Label = new GUIContent($"Main '{m_Name}': ");
			m_ConfigName = SuperSingletonsEditor.GetConfigName(m_Type);
			EditorBuildSettings.TryGetConfigObject(m_ConfigName, out m_CurrentMain);
		}

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			if (!EditorUtility.IsPersistent(target))
				return;

			if (!serializedObject.isEditingMultipleObjects && target != m_CurrentMain)
			{
				EditorGUI.BeginChangeCheck();
				var mainSingleton = SuperSingletonsEditor.SetMainSingletonGUI(target, m_CurrentMain, m_Type);
				if (EditorGUI.EndChangeCheck())
					m_CurrentMain = mainSingleton;
			}
		}
	}
}