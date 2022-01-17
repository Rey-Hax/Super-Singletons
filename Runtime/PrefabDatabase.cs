using System.Collections.Generic;
using UnityEngine;

namespace PhantasmicGames.SuperSingletons
{
	internal class PrefabDatabase : ScriptableObjectSingleton<PrefabDatabase>
	{
		[SerializeField] private List<string> m_ConfigNames;
		[SerializeField] private List<MonoBehaviour> m_Prefabs;

		internal bool TryGetPrefab(string configName, out MonoBehaviour prefab)
		{
			var index = m_ConfigNames.IndexOf(configName);

			if (index >= 0)
			{
				prefab = m_Prefabs[index];
				return true;
			}
			else
			{
				prefab = null;
				return false;
			}
		}
	}
}