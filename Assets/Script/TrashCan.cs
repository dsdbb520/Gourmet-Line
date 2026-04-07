using UnityEngine;

public class TrashCan : MonoBehaviour
{
    private BuildingData myData;

    void Start()
    {
        myData = GetComponent<BuildingData>();
    }

    void Update()
    {
        // 发现槽位里有物品，并且已经稳稳停在中心
        if (myData.currentItem != null && myData.currentItem.hasArrived)
        {
            // 直接销毁
            Destroy(myData.currentItem.gameObject);
            myData.currentItem = null;

        }
    }
}