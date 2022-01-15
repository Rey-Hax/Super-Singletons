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

		[SerializeField, HideInInspector] private bool m_IsMainInstance = true;

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
		/// Is this instance considered the main Singleton of it's type?
		/// </summary>
		public bool isMainInstance
		{
			get => m_IsMainInstance;
			internal set => m_IsMainInstance = value;
		}

		/// <summary>
		/// If this ScriptableObjectSingleton is not referenced in any scene, should it be included in the build?
		/// </summary>
		protected virtual bool includeInBuild => true;

		/// <summary>
		/// Is only called when ScriptableObject is created in the Editor and when the Editor is first opened.
		/// If overiding, make sure to call base.Awake() before anything else to ensure it's correct 'isMain' state.
		/// </summary>
		protected virtual void Awake()
		{
#if UNITY_EDITOR
			if (EditorBuildSettings.TryGetConfigObject(s_ConfigName, out TScriptableObject result) && result != null && result != this)
				isMainInstance = false;
#endif
		}

		protected virtual void OnEnable()
		{
#if UNITY_EDITOR
			if (EditorBuildSettings.TryGetConfigObject(s_ConfigName, out TScriptableObject result) && this == result)
				s_Instance = this as TScriptableObject;
#else
				if(s_Instance == null && isMainInstance)
					s_Instance = this as TScriptableObject;
#endif
		}
	}
}
