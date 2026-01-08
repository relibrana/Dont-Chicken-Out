using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolingManager : MonoBehaviour
{

    public List<PooledObject> pooledBlocks = new List<PooledObject>();
    public List<PooledObject> pooledItems = new List<PooledObject>();
    public PooledObject itemCapsule;
    [HideInInspector] public List<GameObject> blockList = new List<GameObject>();
    [HideInInspector] public List<GameObject> itemList = new List<GameObject>();
    [HideInInspector] public List<GameObject> capsuleList = new List<GameObject>();
    private GameObject capsuleParent;

    private void Awake()
	{
		Initialize();
    }
    private void Initialize()
    {
        // Create a parent for each object type
        foreach (PooledObject obj in pooledBlocks)
        {
            InitializePoolObjects(obj, blockList);
        }
        foreach (PooledObject item in pooledItems)
        {
            InitializePoolObjects(item, itemList);   
        }
        InitializePoolObject(itemCapsule, capsuleList);
    }
    
    public void ResetPool()
    {
        foreach (GameObject parent in blockList)
        {
            foreach (Transform blockGroup in parent.transform)
            {
                if(blockGroup.gameObject.activeSelf)
                {
                    blockGroup.gameObject.SetActive(false);
                    ResetSingleBlocks(blockGroup.gameObject);
                }
            }
        }
        
        foreach (GameObject parent in itemList)
        {
            foreach (Transform item in parent.transform)
            {
                if(item.gameObject.activeSelf)
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        foreach (GameObject capsule in capsuleList)
        {
            capsule.SetActive(false);
        }
    }

    public void ResetSingleBlocks(GameObject parent)
    {
        if (parent == null) return;

        Transform parentTransform = parent.transform;

        for (int i = 0; i < parentTransform.childCount; i++)
        {
            Transform child = parentTransform.GetChild(i);
            GameObject block = child.gameObject;

            if (!block.CompareTag("Block"))
                continue;

            if (!block.activeSelf)
                block.SetActive(true);
        }
    }


    public GameObject GetPooledBlock(int _objIndex, Vector3 _spawnPosition)
	{
		GameObject pObject = blockList[_objIndex].transform.GetChild(0).gameObject;

		if (pObject.activeSelf)
		{
			Debug.Log ("All instances are busy, spawn new one");
			pObject = Instantiate(pooledBlocks[_objIndex].pooledObjPrefab, blockList[_objIndex].transform);
		}

		pObject.SetActive(false);
		pObject.SetActive(true);
		pObject.transform.position = _spawnPosition;
		pObject.transform.localScale = Vector3.one;
		pObject.transform.SetSiblingIndex(pObject.transform.parent.childCount);
        return pObject;
    }
    public GameObject GetPooledItem(int _objIndex, Vector3 _spawnPosition)
	{
		GameObject pObject = itemList[_objIndex].transform.GetChild(0).gameObject;

		if (pObject.activeSelf)
		{
			Debug.Log ("All instances are busy, spawn new one");
			pObject = Instantiate(pooledItems[_objIndex].pooledObjPrefab, itemList[_objIndex].transform);
		}

		pObject.SetActive(false);
		pObject.SetActive(true);
		pObject.transform.position = _spawnPosition;
		pObject.transform.localScale = Vector3.one;
		pObject.transform.SetSiblingIndex(pObject.transform.parent.childCount);
        return pObject;
    }
    
    public GameObject GetCapsule()
	{
		GameObject pObject = capsuleList[0];

		if (pObject.activeSelf)
		{
			Debug.Log ("All instances are busy, spawn new one");
			pObject = Instantiate(itemCapsule.pooledObjPrefab, capsuleParent.transform);
		}

		pObject.SetActive(false);
		pObject.SetActive(true);
		pObject.transform.localScale = Vector3.one;
		pObject.transform.SetSiblingIndex(pObject.transform.parent.childCount);
        return pObject;
    }
    
    private void InitializePoolObject(PooledObject _pooledObject, List<GameObject> _list)
	{
        capsuleParent = new GameObject();
        capsuleParent.name = _pooledObject.pooledObjPrefab.ToString();
        capsuleParent.transform.parent = transform;
        // Spawn the gameObject clones inside the parent
        for (int i = 0; i < _pooledObject.ammountToPool; i++)
		{
            GameObject spawnedObject = GameObject.Instantiate(_pooledObject.pooledObjPrefab);
            spawnedObject.transform.parent = capsuleParent.transform;
            spawnedObject.SetActive(false);
            _list.Add(spawnedObject);
        }
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

}

[System.Serializable]
public struct PooledObject
{
    [Tooltip("Ammount of individual objects that will be spawned")]
    public int ammountToPool;
    [Tooltip("The particle system made into a prefab")]
    public GameObject pooledObjPrefab;
}
