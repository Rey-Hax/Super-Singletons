using PhantasmicGames.Common;
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
		/// <summary>Does the path point to a singleton asset?</summary>
		private static bool IsSingletonAsset(UnityObject obj, out Type singletonGenericDefinition, out UnityObject singleton)
		{
			var result = false;
			singletonGenericDefinition = null;
			singleton = null;

			if (obj is ScriptableObject)
			{
				result = IsScriptableObjectSingleton(obj as ScriptableObject, out singletonGenericDefinition);
				singleton = obj;
			}
			else if (obj is GameObject)
			{
				result = IsMonoBehaviourSingletonPrefab(obj as GameObject, out singletonGenericDefinition, out MonoBehaviour monoBehaviour);
				singleton = monoBehaviour;
			}
			else if (obj is MonoBehaviour)
			{
				result = IsMonoBehaviourSingleton(obj as MonoBehaviour, out singletonGenericDefinition);
				singleton = obj;
			}
			return result;
		}

		public static bool IsScriptableObjectSingleton(ScriptableObject scriptableObject, out Type singletonGenericTypeDefinition)
		{
			return Utility.TypeInheritsGenericTypeDefinition(scriptableObject.GetType(), typeof(ScriptableObjectSingleton<>), out singletonGenericTypeDefinition);
		}

		public static bool IncludedInBuild(ScriptableObject scriptableObjectSingleton)
		{
			if (Utility.TypeInheritsGenericTypeDefinition(scriptableObjectSingleton.GetType(), typeof(ScriptableObjectSingleton<>), out Type type))
			{
				return (bool)type.GetProperty("includeInBuild", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scriptableObjectSingleton);
			}
			throw new Exception("The ScriptableObject does not inherit ScriptableObjectSingleton<TScriptableObject>!");
		}

		public static bool IsMonoBehaviourSingletonPrefab(GameObject prefab, out Type singletonGenericTypeDefinition, out MonoBehaviour monoBehaviourSingleton)
		{
			foreach (var monoBehaviour in prefab.GetComponents<MonoBehaviour>())
			{
				if (IsMonoBehaviourSingleton(monoBehaviour, out singletonGenericTypeDefinition))
				{
					monoBehaviourSingleton = monoBehaviour;
					return true;
				}
			}
			singletonGenericTypeDefinition = null;
			monoBehaviourSingleton = null;
			return false;
		}

		public static bool IsMonoBehaviourSingleton(MonoBehaviour monoBehaviour, out Type singletonGenericTypeDefinition)
		{
			if (Utility.TypeInheritsGenericTypeDefinition(monoBehaviour.GetType(), typeof(MonoBehaviourSingleton<>), out singletonGenericTypeDefinition))
				return true;
			return false;
		}

		public static string GetConfigName(Type singletonType)
		{
			if (!Utility.TypeInheritsGenericTypeDefinition(singletonType, typeof(ScriptableObjectSingleton<>), out Type genericTypeDefinition))
				Utility.TypeInheritsGenericTypeDefinition(singletonType, typeof(MonoBehaviourSingleton<>), out genericTypeDefinition);

			if (genericTypeDefinition == null)
				throw new Exception("'singletonType' does not derived from either ScriptableObjectSingleton<> or MonoBehaviourSingleton<>!");
			return (string)genericTypeDefinition.GetProperty("s_ConfigName", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
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
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(s_PrefabDatabase));
				RemoveScriptableObjectSingletonsFromPreloadedAssets();
			}

			private static ScriptableObject CreatePrefabDatabase()
			{
				var prefabs = new SerializableDictionary<SerializableType, MonoBehaviour>();
				foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(MonoBehaviourSingleton<>)))
				{
					var configName = GetConfigName(type);
					if (EditorBuildSettings.TryGetConfigObject(configName, out MonoBehaviour prefab))
						prefabs.Add(type, prefab);
				}

				var prefabDatabaseType = Type.GetType("PhantasmicGames.SuperSingletons.PrefabDatabase, PhantasmicGames.SuperSingletons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
				var instance = ScriptableObject.CreateInstance(prefabDatabaseType);

				var prefabsField = prefabDatabaseType.GetField("m_Prefabs", BindingFlags.NonPublic | BindingFlags.Instance);
				prefabsField.SetValue(instance, prefabs);

				AssetDatabase.CreateAsset(instance, AssetDatabase.GenerateUniqueAssetPath("Assets/PrefabDatabase.asset"));

				return instance;
			}

			private void RemoveScriptableObjectSingletonsFromPreloadedAssets()
			{
				List<UnityObject> preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
				if (preloadedAssets == null)
					return;

				preloadedAssets.RemoveAll(a => (a is ScriptableObject && IsScriptableObjectSingleton(a as ScriptableObject, out _)));
				PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
			}
		}
	}
}