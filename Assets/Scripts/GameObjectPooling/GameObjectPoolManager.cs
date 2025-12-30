using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Pool;

public class GameObjectPoolManager : MonoBehaviour
{
    public static GameObjectPoolManager Instance { get; private set; }

    [SerializeField] private List<PoolSettings> _poolSettings = new ();
    private Dictionary<GameObject, ObjectPool<GameObject>> _objectPools = new ();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        InitializePools();
    }

    private void InitializePools()
    {
        for(int i = 0; i < _poolSettings.Count; i++)
            InitializePool(_poolSettings[i]);
    }
        
    private void InitializePool(PoolSettings settings)
    {
        GameObject onCreate()
        {
            return Instantiate(settings.prefab);
        }

        void onGet(GameObject obj)
        {
            obj.SetActive(true);
        }

        void onRelease(GameObject obj)
        {
            obj.SetActive(false);
        }

        void onDestroy(GameObject obj)
        {
            Destroy(obj);
        }

        ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
            createFunc: onCreate,
            actionOnGet: onGet,
            actionOnRelease: onRelease,
            actionOnDestroy: onDestroy,
            collectionCheck: true,
            defaultCapacity: settings.defaultCapacity,
            maxSize: settings.maxCapacity
        );

        for (int i = 0; i < settings.prewarm; i++)
        {
            GameObject obj = pool.Get();
            pool.Release(obj);
        }

        _objectPools[settings.prefab] = pool;
    }

    public GameObject Get(GameObject prefab)
    {
        ObjectPool<GameObject> pool = FindPool(prefab);
        if (pool != null)
            return pool.Get();
        return null;
    }

    public void Release(GameObject prefab, GameObject obj)
    {
        ObjectPool<GameObject> pool = FindPool(prefab);
        pool?.Release(obj);
    }

    private ObjectPool<GameObject> FindPool(GameObject prefab)
    {
        if (_objectPools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
            return pool;

        Debug.LogError($"No pool found for prefab: {prefab.name}");
        return null;
    }
}