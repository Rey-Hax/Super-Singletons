using PhantasmicGames.SuperSingletons;
using UnityEditor;
using UnityEngine;

namespace PhantasmicGames.SuperSingletonsEditor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(MonoBehaviourSingleton<>), true)]
	public class MonoBehaviourSingletonEditor : Editor
	{
		private System.Type m_Type;
		private string m_ConfigName;
		private Object m_CurrentMain;
		private Object m_targetPrefab;

		private void OnEnable()
		{
			m_Type = target.GetType();

			m_ConfigName = SuperSingletonsEditor.GetConfigName(m_Type);
			EditorBuildSettings.TryGetConfigObject(m_ConfigName, out m_CurrentMain);

			if (PrefabUtility.IsPartOfAnyPrefab(target))
			{
				m_targetPrefab = GetTargetPrefab();
			}
		}

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			if (PrefabUtility.IsPartOfAnyPrefab(target) && !serializedObject.isEditingMultipleObjects && m_targetPrefab != m_CurrentMain)
			{
				EditorGUI.BeginChangeCheck();
				var mainSingleton = SuperSingletonsEditor.SetMainSingletonGUI(m_targetPrefab, m_CurrentMain, m_Type);
				if (EditorGUI.EndChangeCheck())
				{
					m_CurrentMain = mainSingleton;
					m_targetPrefab = GetTargetPrefab();
				}
			}
		}

		private Object GetTargetPrefab()
		{
			return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target)).GetComponent(m_Type);
		}
	}
}