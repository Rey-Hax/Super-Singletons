using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PhantasmicGames.SuperSingletons
{
	[DisallowMultipleComponent]
	public class MonoBehaviourSingleton<TMonoBehaviour> : MonoBehaviour where TMonoBehaviour : MonoBehaviour
	{
		private static string s_ConfigName => typeof(TMonoBehaviour).FullName;
		private static TMonoBehaviour s_Instance;
		private static readonly object s_Lock = new object();

		[SerializeField, HideInInspector] private bool m_IsMain = true;


		public static TMonoBehaviour instance
		{
			get
			{
				if (quitting)
					return null;

				if (s_Instance == null)
				{
					lock (s_Lock)
					{
#if UNITY_EDITOR
						EditorBuildSettings.TryGetConfigObject(s_ConfigName, out MonoBehaviour prefab);
#else
						PrefabDatabase.instance.TryGetPrefab(typeof(TMonoBehaviour), out MonoBehaviour prefab);
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

		private void Awake()
		{
			if (s_Instance != null && s_Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			else
				s_Instance = this as TMonoBehaviour;

			quitting = false;
		}

		/// <summary>
		/// If overriding make sure to call base.OnDestroy() first
		/// </summary>
		protected virtual void OnDestroy()
		{
			if (s_Instance == this)
			{
				quitting = true;
				s_Instance = null;
			}
		}
	}
}