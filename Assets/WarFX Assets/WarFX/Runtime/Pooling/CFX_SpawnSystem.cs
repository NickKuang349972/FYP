using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Cartoon FX  - (c) 2012-2016 Jean Moreno

// Spawn System:
// Preload GameObject to reuse them later, avoiding to Instantiate them.
// Very useful for mobile platforms.

namespace WarFX
{
	public class CFX_SpawnSystem : MonoBehaviour
	{
		/// <summary>
		/// Get the next available preloaded Object.
		/// </summary>
		/// <returns>
		/// The next available preloaded Object.
		/// </returns>
		/// <param name='sourceObj'>
		/// The source Object from which to get a preloaded copy.
		/// </param>
		/// <param name='activateObject'>
		/// Activates the object before returning it.
		/// </param>
		static public GameObject GetNextObject(GameObject sourceObj, bool activateObject = true)
		{
			if(sourceObj == null)
			{
				Debug.LogError("[CFX_SpawnSystem.GetNextObject()] sourceObj is null.");
				return null;
			}

			if(!HasInstance)
			{
				Debug.LogError("[CFX_SpawnSystem.GetNextObject()] No CFX_SpawnSystem in the Scene: add one and preload \"" + sourceObj.name + "\" before requesting it.");
				return null;
			}

			int uniqueId = sourceObj.GetInstanceID();

			if(!instance.poolCursors.ContainsKey(uniqueId) || !instance.instantiatedObjects.ContainsKey(uniqueId))
			{
				Debug.LogError("[CFX_SpawnSystem.GetNextObject()] Object hasn't been preloaded: " + sourceObj.name + " (ID:" + uniqueId + ")\n", instance);
				return null;
			}

			List<GameObject> pool = instance.instantiatedObjects[uniqueId];
			if(pool.Count == 0)
			{
				PreloadObject(sourceObj);
				pool = instance.instantiatedObjects[uniqueId];
			}

			int cursor = instance.poolCursors[uniqueId];
			GameObject returnObj = null;
			if(instance.onlyGetInactiveObjects)
			{
				int loop = cursor;
				while(true)
				{
					returnObj = pool[cursor];
					instance.increasePoolCursor(uniqueId);
					cursor = instance.poolCursors[uniqueId];

					if(returnObj != null && !returnObj.activeSelf)
						break;

					//complete loop: no active instance available
					if(cursor == loop)
					{
						if(instance.instantiateIfNeeded)
						{
							Debug.Log("[CFX_SpawnSystem.GetNextObject()] A new instance has been created for \"" + sourceObj.name + "\" because no active instance were found in the pool.\n", instance);
							PreloadObject(sourceObj);
							returnObj = pool[pool.Count-1];
							break;
						}
						else
						{
							Debug.LogWarning("[CFX_SpawnSystem.GetNextObject()] There are no active instances available in the pool for \"" + sourceObj.name +"\"\nYou may need to increase the preloaded object count for this prefab?", instance);
							return null;
						}
					}
				}
			}
			else
			{
				returnObj = pool[cursor];
				instance.increasePoolCursor(uniqueId);
			}

			if(activateObject && returnObj != null)
				returnObj.SetActive(true);

			return returnObj;
		}

		/// <summary>
		/// Preloads an object a number of times in the pool.
		/// </summary>
		/// <param name='sourceObj'>
		/// The source Object.
		/// </param>
		/// <param name='poolSize'>
		/// The number of times it will be instantiated in the pool (i.e. the max number of same object that would appear simultaneously in your Scene).
		/// </param>
		static public void PreloadObject(GameObject sourceObj, int poolSize = 1)
		{
			if(sourceObj == null)
			{
				Debug.LogError("[CFX_SpawnSystem.PreloadObject()] sourceObj is null.");
				return;
			}

			if(!HasInstance)
			{
				Debug.LogError("[CFX_SpawnSystem.PreloadObject()] No CFX_SpawnSystem in the Scene: add one before preloading \"" + sourceObj.name + "\".");
				return;
			}

			instance.addObjectToPool(sourceObj, Mathf.Max(1, poolSize));
		}

		/// <summary>
		/// Unloads all the preloaded objects from a source Object.
		/// </summary>
		/// <param name='sourceObj'>
		/// Source object.
		/// </param>
		static public void UnloadObjects(GameObject sourceObj)
		{
			if(sourceObj == null)
			{
				Debug.LogError("[CFX_SpawnSystem.UnloadObjects()] sourceObj is null.");
				return;
			}

			if(!HasInstance)
			{
				Debug.LogError("[CFX_SpawnSystem.UnloadObjects()] No CFX_SpawnSystem in the Scene: nothing to unload for \"" + sourceObj.name + "\".");
				return;
			}

			instance.removeObjectsFromPool(sourceObj);
		}

		/// <summary>
		/// Gets a value indicating whether a CFX_SpawnSystem is present in the Scene.
		/// </summary>
		static public bool HasInstance
		{
			get { return instance != null; }
		}

		/// <summary>
		/// Gets a value indicating whether all objects defined in the Editor are loaded or not.
		/// </summary>
		/// <value>
		/// <c>true</c> if all objects are loaded; otherwise, <c>false</c>.
		/// </value>
		static public bool AllObjectsLoaded
		{
			get
			{
				return instance != null && instance.allObjectsLoaded;
			}
		}

		// INTERNAL SYSTEM ----------------------------------------------------------------------------------------------------------------------------------------

		static private CFX_SpawnSystem instance;

		public GameObject[] objectsToPreload = new GameObject[0];
		public int[] objectsToPreloadTimes = new int[0];
		public bool hideObjectsInHierarchy = false;
		public bool spawnAsChildren = true;
		public bool onlyGetInactiveObjects = false;
		public bool instantiateIfNeeded = false;

		private bool allObjectsLoaded;
		private Dictionary<int,List<GameObject>> instantiatedObjects = new Dictionary<int, List<GameObject>>();
		private Dictionary<int,int> poolCursors = new Dictionary<int, int>();

		private void addObjectToPool(GameObject sourceObject, int number)
		{
			int uniqueId = sourceObject.GetInstanceID();

			//Add new entry if it doesn't exist
			if(!instantiatedObjects.ContainsKey(uniqueId))
			{
				instantiatedObjects.Add(uniqueId, new List<GameObject>());
				poolCursors.Add(uniqueId, 0);
			}

			//Add the new objects
			GameObject newObj;
			for(int i = 0; i < number; i++)
			{
				newObj = (GameObject)Instantiate(sourceObject);
				newObj.SetActive(false);

				//Set flag to not destruct object
				CFX_AutoDestructShuriken[] autoDestruct = newObj.GetComponentsInChildren<CFX_AutoDestructShuriken>(true);
				foreach(CFX_AutoDestructShuriken ad in autoDestruct)
				{
					ad.OnlyDeactivate = true;
				}
				//Set flag to not destruct light
				CFX_LightIntensityFade[] lightIntensity = newObj.GetComponentsInChildren<CFX_LightIntensityFade>(true);
				foreach(CFX_LightIntensityFade li in lightIntensity)
				{
					li.autodestruct = false;
				}

				instantiatedObjects[uniqueId].Add(newObj);

				if(hideObjectsInHierarchy)
					newObj.hideFlags = HideFlags.HideInHierarchy;

				if(spawnAsChildren)
					newObj.transform.parent = this.transform;
			}
		}

		private void removeObjectsFromPool(GameObject sourceObject)
		{
			int uniqueId = sourceObject.GetInstanceID();

			if(!instantiatedObjects.ContainsKey(uniqueId))
			{
				Debug.LogWarning("[CFX_SpawnSystem.removeObjectsFromPool()] There aren't any preloaded object for: " + sourceObject.name + " (ID:" + uniqueId + ")\n", this.gameObject);
				return;
			}

			//Destroy all objects
			for(int i = instantiatedObjects[uniqueId].Count - 1; i >= 0; i--)
			{
				GameObject obj = instantiatedObjects[uniqueId][i];
				instantiatedObjects[uniqueId].RemoveAt(i);
				GameObject.Destroy(obj);
			}

			//Remove pool entry
			instantiatedObjects.Remove(uniqueId);
			poolCursors.Remove(uniqueId);
		}

		private void increasePoolCursor(int uniqueId)
		{
			instance.poolCursors[uniqueId]++;
			if(instance.poolCursors[uniqueId] >= instance.instantiatedObjects[uniqueId].Count)
			{
				instance.poolCursors[uniqueId] = 0;
			}
		}

		//--------------------------------

		void Awake()
		{
			if(instance != null && instance != this)
			{
				// Keep the first instance: silently replacing it used to orphan every already pooled object.
				Debug.LogError("CFX_SpawnSystem: there should only be one instance of CFX_SpawnSystem per Scene! Duplicate on \"" + this.gameObject.name + "\" has been disabled.\n", this.gameObject);
				enabled = false;
				return;
			}

			instance = this;
		}

		void OnDestroy()
		{
			if(instance == this)
				instance = null;
		}

		void Start()
		{
			allObjectsLoaded = false;

			for(int i = 0; i < objectsToPreload.Length; i++)
			{
				if(objectsToPreload[i] == null) continue;

				// The two arrays are edited by hand in the Inspector and can drift out of sync.
				int times = (i < objectsToPreloadTimes.Length) ? objectsToPreloadTimes[i] : 1;
				PreloadObject(objectsToPreload[i], times);
			}

			allObjectsLoaded = true;
		}
	}
}
