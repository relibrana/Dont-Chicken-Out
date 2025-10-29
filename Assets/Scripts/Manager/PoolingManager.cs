using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolingManager : MonoBehaviour
{

    public List<PooledObject> pooledObjects = new List<PooledObject>();
    [HideInInspector] public List<GameObject> parentTransform = new List<GameObject>();


    private void Awake()
	{
		Initialize();
    }
    private void Initialize()
	{
        // Create a parent for each object type
        foreach (PooledObject obj in pooledObjects)
		{
            InitializePoolObjects(obj, parentTransform);
        }
    }

    public GameObject GetPooledObject(int _objIndex, Vector3 _spawnPosition, float _aliveTime = 3f, float _xScale = 1f)
	{
		GameObject tr = parentTransform[_objIndex].transform.GetChild(0).gameObject;

		if (tr.activeSelf)
		{
			Debug.Log ("All instances are busy, spawn new one");
			tr = GameObject.Instantiate(pooledObjects[_objIndex].pooledObjPrefab, parentTransform[_objIndex].transform);
		}

		tr.SetActive(false);
		tr.SetActive(true);
		tr.transform.position = _spawnPosition;
		tr.transform.localScale = new Vector3(_xScale, 1, 1);
		tr.transform.SetSiblingIndex(tr.transform.parent.childCount);
		if(_aliveTime > 0f)
		{
			StartCoroutine(TurnOffObject(tr, _aliveTime));
        }
        return tr;
    }

    public void TurnOffObjectInstance(GameObject _object, float _time)
	{
        StartCoroutine(TurnOffObject(_object, _time));
    }

    IEnumerator TurnOffObject(GameObject _obj, float _aliveTime)
	{
        yield return new WaitForSeconds(_aliveTime);
        _obj.SetActive(false);
    }

    private void InitializePoolObjects(PooledObject _pooledObject, List<GameObject> _list)
	{
        GameObject pooledObjectsParent = new GameObject();
        pooledObjectsParent.name = _pooledObject.pooledObjPrefab.ToString();
        pooledObjectsParent.transform.parent = transform;
        _list.Add(pooledObjectsParent);
        // Spawn the gameObject clones inside the parent
        for (int i = 0; i < _pooledObject.ammountToPool; i++)
		{
            GameObject spawnedObject = GameObject.Instantiate(_pooledObject.pooledObjPrefab);
            spawnedObject.transform.parent = pooledObjectsParent.transform;
            spawnedObject.SetActive(false);
        }
    }

	private void SpawnNewInstance ()
	{

	}
}

[System.Serializable]
public struct PooledObject
{
    [Tooltip("Ammount of individual objects that will be spawned")]
    public int ammountToPool;
    [Tooltip("The particle system made into a prefab")]
    public GameObject pooledObjPrefab;
}
