using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PhantasmicGames.SuperSingletons
{
	public class ScriptableObjectSingleton<TScriptableObject> : ScriptableObject where TScriptableObject : ScriptableObject
	{
		private static string s_ConfigName => typeof(TScriptableObject).FullName;
		private static TScriptableObject s_Instance;

		[SerializeField, HideInInspector] private bool m_IsMain = true;

		public static TScriptableObject instance
		{
			get
			{
#if UNITY_EDITOR
				if (s_Instance == null)
					EditorBuildSettings.TryGetConfigObject(s_ConfigName, out s_Instance);
#endif
				return s_Instance;
			}
		}

		/// <summary>
		/// If this ScriptableObjectSingleton is not referenced in any scene, should it be included in the build?
		/// </summary>
		protected virtual bool includeInBuild => true;

		protected virtual void OnEnable()
		{
#if UNITY_EDITOR
			if (EditorBuildSettings.TryGetConfigObject(s_ConfigName, out TScriptableObject result) && this == result)
				s_Instance = this as TScriptableObject;
#else
				if(s_Instance == null && m_IsMain)
					s_Instance = this as TScriptableObject;
#endif
		}
	}
}
