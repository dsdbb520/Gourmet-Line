using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public GameObject itemPrefab;
    public float spawnInterval = 2.0f;
    
    private float timer;
    private Vector3Int myGridPos;
    private BuildingData myData;

    void Start()
    {
        
        myData = GetComponent<BuildingData>();
		if (myData != null) myGridPos = myData.anchorGridPos;
        // 覆盖默认策略，切换为“自动寻找方向”
        if (myData != null)
        {
            myData.outputStrategy = new AutoFindDirectionStrategy();
        }
    }

    void Update()
    {
        // 如果当前 Spawner 的槽位已经被物品占了还没走，就会卡住倒计时
        if (myData != null && myData.currentItem != null) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnItem();
            timer = 0;
        }
    }

    private void SpawnItem()
    {
        GameObject obj = Instantiate(itemPrefab);
        Item item = obj.GetComponent<Item>();
        if (item != null)
        {
            item.Init(myGridPos);
        }
    }
}