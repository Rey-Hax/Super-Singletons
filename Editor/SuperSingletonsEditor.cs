using PhantasmicGames.SuperSingletons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

using UnityObject = UnityEngine.Object;

namespace PhantasmicGames.SuperSingletonsEditor 
{
	public class SuperSingletonsEditor
	{
		private static bool IsSingletonAsset(UnityObject asset, out Type singletonGenericDefinition, out UnityObject singleton)
		{
			if (asset is ScriptableObject)
			{
				if (IsSingletonObject(asset, out singletonGenericDefinition))
				{
					singleton = asset;
					return true;
				}
			}
			else if (asset is GameObject)
			{
				foreach (var monoBehaviour in (asset as GameObject).GetComponents<MonoBehaviour>())
				{
					if (IsSingletonObject(monoBehaviour, out singletonGenericDefinition))
					{
						singleton = monoBehaviour;
						return true;
					}
				}
			}
			singletonGenericDefinition = null;
			singleton = null;
			return false;
		}

		public static bool IsSingletonObject(UnityObject singletonObject, out Type singletonGenericType)
		{
			singletonGenericType = GetSingletonGenericType(singletonObject.GetType());
			return singletonGenericType != null;
		}

		private static Type GetSingletonGenericType(Type type)
		{
			while (type != null)
			{
				if (type.IsGenericType)
				{
					var genericTypeDefinition = type.GetGenericTypeDefinition();
					if (genericTypeDefinition == typeof(ScriptableObjectSingleton<>) || genericTypeDefinition == typeof(MonoBehaviourSingleton<>))
						return type;
				}
				type = type.BaseType;
			}
			return null;
		}

		private static bool IncludedInBuild(ScriptableObject scriptableObjectSingleton)
		{
			var genericType = GetSingletonGenericType(scriptableObjectSingleton.GetType());
			return (bool)genericType.GetProperty("includeInBuild", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scriptableObjectSingleton);
		}

		public static string GetConfigName(Type singletonType)
		{
			var singletonGenericType = GetSingletonGenericType(singletonType);

			if (singletonGenericType == null)
				throw new Exception("'singletonType' does not derived from either ScriptableObjectSingleton<> or MonoBehaviourSingleton<>!");

			return (string)singletonGenericType.GetProperty("configName", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
		}

		public static void SetIsMain(UnityObject singletonAsset, bool value)
		{
			if (!IsSingletonAsset(singletonAsset, out Type genericTypeDefinition, out UnityObject singleton))
				throw new Exception("'singletonAsset' is neither a ScriptableObjectSingleton<> or a MonoBehaviourSingleton<>!");

			var configName = GetConfigName(genericTypeDefinition);

			var isMainField = genericTypeDefinition.GetField("m_IsMain", BindingFlags.NonPublic | BindingFlags.Instance);
			isMainField.SetValue(singleton, value);

			if (EditorBuildSettings.TryGetConfigObject(configName, out UnityObject currentMain))
			{
				if (value && singleton != currentMain)
					isMainField.SetValue(currentMain, false);
				else if (!value && singleton == currentMain)
					EditorBuildSettings.RemoveConfigObject(configName);
			}

			if (value)
				EditorBuildSettings.AddConfigObject(configName, singleton, true);
		}

		public static UnityObject SetMainSingletonGUI(UnityObject target, UnityObject current, Type singletonType)
		{
			EditorGUILayout.BeginVertical("Box");
			EditorGUILayout.HelpBox($"This '{singletonType.Name}' is not set as the main '{singletonType.Name}'.", MessageType.Warning);

			EditorGUILayout.BeginHorizontal();
			var label = new GUIContent($"Main '{singletonType.Name}': ");
			EditorGUILayout.LabelField($"Main '{singletonType.Name}': ", GUILayout.MaxWidth(GUIStyle.none.CalcSize(label).x));
			using (new EditorGUI.DisabledScope(true))
				EditorGUILayout.ObjectField(current, singletonType, false);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			if (GUILayout.Button($"Set this as Main", GUILayout.MaxWidth(250)))
			{
				SetIsMain(target, true);
				current = target;
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();

			return current;
		}

		private class AssetPostprocessor : UnityEditor.AssetPostprocessor
		{
			private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFromPaths)
			{
				foreach (var assetPath in imported)
				{
					var asset = AssetDatabase.LoadAssetAtPath<UnityObject>(assetPath);
					if (!IsSingletonAsset(asset, out Type type, out UnityObject singleton))
						continue;

					var markedAsMain = (bool)type.GetField("m_IsMain", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(singleton);
					if (!markedAsMain)
						continue;

					var configName = GetConfigName(type);
					EditorBuildSettings.TryGetConfigObject(configName, out UnityObject currentSingleton);

					if (currentSingleton == null)
						EditorBuildSettings.AddConfigObject(configName, singleton, true);
					else if (currentSingleton != singleton)
						SetIsMain(singleton, false);
				}
			}
		}

		private class BuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
		{
			public int callbackOrder => 1;

			private static ScriptableObject s_PrefabDatabase;

			public void OnPreprocessBuild(BuildReport report)
			{
				RemoveScriptableObjectSingletonsFromPreloadedAssets();

				var scriptableObjectSingletons = new List<ScriptableObject>();
				foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(ScriptableObjectSingleton<>)))
				{
					var configName = GetConfigName(type);
					if (EditorBuildSettings.TryGetConfigObject(configName, out ScriptableObject sos))
						scriptableObjectSingletons.Add(sos);
				}

				var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
				
				foreach (var sos in scriptableObjectSingletons)
				{
					if (!IncludedInBuild(sos))
						continue;

					preloadedAssets.Add(sos);
				}

				s_PrefabDatabase = CreatePrefabDatabase();
				preloadedAssets.Add(s_PrefabDatabase);

				PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
			}

			public void OnPostprocessBuild(BuildReport report)
			{
				RemoveScriptableObjectSingletonsFromPreloadedAssets();
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(s_PrefabDatabase));
			}

			private static ScriptableObject CreatePrefabDatabase()
			{
				var prefabs = new Dictionary<string, MonoBehaviour>();

				foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(MonoBehaviourSingleton<>)))
				{
					var configName = GetConfigName(type);
					if (EditorBuildSettings.TryGetConfigObject(configName, out MonoBehaviour prefab))
						prefabs.Add(configName, prefab);
				}

				var prefabDatabaseType = Type.GetType("PhantasmicGames.SuperSingletons.PrefabDatabase, PhantasmicGames.SuperSingletons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
				var instance = ScriptableObject.CreateInstance(prefabDatabaseType);

				var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
				prefabDatabaseType.GetField("m_ConfigNames", bindingFlags).SetValue(instance, prefabs.Keys.ToList());
				prefabDatabaseType.GetField("m_Prefabs", bindingFlags).SetValue(instance, prefabs.Values.ToList());

				AssetDatabase.CreateAsset(instance, AssetDatabase.GenerateUniqueAssetPath("Assets/PrefabDatabase.asset"));

				return instance;
			}

			private void RemoveScriptableObjectSingletonsFromPreloadedAssets()
			{
				List<UnityObject> preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
				if (preloadedAssets == null)
					return;

				preloadedAssets.RemoveAll(a => IsSingletonObject(a, out _));
				PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
			}
		}
	}
}