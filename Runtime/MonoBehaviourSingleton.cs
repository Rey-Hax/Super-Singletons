using UnityEngine;
using UnityEngine.SceneManagement;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PhantasmicGames.SuperSingletons
{
	[DisallowMultipleComponent]
	public class MonoBehaviourSingleton<TMonoBehaviour> : MonoBehaviour where TMonoBehaviour : MonoBehaviour
	{
		internal static string configName => typeof(TMonoBehaviour).FullName;

		private static TMonoBehaviour s_Instance;
		private static readonly object s_Lock = new object();

#pragma warning disable CS0414
		[SerializeField, HideInInspector] private bool m_IsMain = true;
#pragma warning restore CS0414

		public static TMonoBehaviour instance
		{
			get
			{
				if (quitting)
					return null;

#if UNITY_EDITOR
				if (!Application.isPlaying)
					throw new Exception($"Getting the instance of '{typeof(TMonoBehaviour).Name}' is only supported in Play Mode");
#endif

				if (s_Instance == null)
				{
					lock (s_Lock)
					{
#if UNITY_EDITOR
						EditorBuildSettings.TryGetConfigObject(configName, out MonoBehaviour prefab);
#else
						PrefabDatabase.instance.TryGetPrefab(configName, out MonoBehaviour prefab);
#endif
						if (prefab)
							s_Instance = Instantiate(prefab as TMonoBehaviour);
						else
							s_Instance = new GameObject($"{typeof(TMonoBehaviour).Name} - Singleton").AddComponent<TMonoBehaviour>();
					}
				}
				return s_Instance;
			}
		}

		public static bool quitting { get; private set; }

		protected virtual (bool value, bool callOnAwakeOnNewScene) persistScenes => (false, false);

		private void CallOnAwake(Scene current, Scene next) => OnAwake();

		private void Awake()
		{
			if (s_Instance != this && s_Instance != null)
			{
				Destroy(gameObject);
				return;
			}
			else
				s_Instance = this as TMonoBehaviour;

			if (persistScenes.value)
			{
				DontDestroyOnLoad(gameObject);
				if (persistScenes.callOnAwakeOnNewScene)
					SceneManager.activeSceneChanged += CallOnAwake;
			}

			if (!persistScenes.value || persistScenes.value && !persistScenes.callOnAwakeOnNewScene)
				OnAwake();
		}

		protected virtual void OnAwake()
		{
		}

		protected virtual void OnApplicationQuit()
		{
			quitting = true;
		}

		protected virtual void OnDestroy()
		{
			if (persistScenes.callOnAwakeOnNewScene)
				SceneManager.activeSceneChanged -= CallOnAwake;
		}
	}
}