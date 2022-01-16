using PhantasmicGames.Common;
using UnityEngine;

namespace PhantasmicGames.SuperSingletons
{
	internal class PrefabDatabase : ScriptableObjectSingleton<PrefabDatabase>
	{
		[SerializeField] private SerializableDictionary<SerializableType, MonoBehaviour> m_Prefabs;

		protected override bool includeInBuild => m_Prefabs.Count > 0;

		public bool TryGetPrefab(SerializableType type, out MonoBehaviour prefab) => m_Prefabs.TryGetValue(type, out prefab);
	}
}