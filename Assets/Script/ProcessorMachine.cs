using UnityEngine;

public class ProcessorMachine : MonoBehaviour
{
    [Header("Processing Settings")]
    public float processTime = 2.0f;        // 加工所需时间
    public GameObject outputItemPrefab;     // 加工后产出的新物品Prefab

    private BuildingData myData;
    private float processTimer = 0f;
    
    // 内部状态机
    private enum State { Idle, Processing, WaitingToOutput }
    private State currentState = State.Idle;

    private Vector3Int myGridPos;

    void Start()
    {
        myData = GetComponent<BuildingData>();
        myGridPos = GridManager.Instance.GetGridPosition(transform.position);
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Idle:
                CheckForInput();
                break;
            case State.Processing:
                DoProcess();
                break;
            case State.WaitingToOutput:
                TryOutput();
                break;
        }
    }

    private void CheckForInput()
    {
        // 发现槽位里有物品，并且已经稳稳停在中心
        if (myData.currentItem != null && myData.currentItem.hasArrived)
        {
            // 1. 锁死状态，防止其他物品进入
            myData.isProcessing = true;
            
            // 2. 吞入物品（销毁旧的白球）
            Destroy(myData.currentItem.gameObject);
            myData.currentItem = null; 

            // 3. 进入加工状态
            currentState = State.Processing;
            processTimer = 0f;
            
            Debug.Log("机器开始加工！");
        }
    }

    private void DoProcess()
    {
        processTimer += Time.deltaTime;
        if (processTimer >= processTime)
        {
            currentState = State.WaitingToOutput;
        }
    }

    private void TryOutput()
    {
        // 尝试向输出方向寻找空位
        Vector3 outDir = myData.GetOutputDirectionVector(myGridPos);
        if (outDir == Vector3.zero) return; // 没有有效出口

        Vector3Int nextGridPos = myGridPos + new Vector3Int(Mathf.RoundToInt(outDir.x), 0, Mathf.RoundToInt(outDir.z));
        BuildingData nextBuilding = GridManager.Instance.GetBuildingAt(nextGridPos);

        // 如果前方是可以接收物品的传送带
        if (nextBuilding != null && nextBuilding.CanAcceptInput())
        {
            // 实例化新物品（比如红球）
            GameObject newObj = Instantiate(outputItemPrefab);
            Item newItem = newObj.GetComponent<Item>();
            
            // 将新物品初始化在机器自己的坐标上
            newItem.Init(myGridPos);
            
            // 【核心】强制让新物品立刻预定下一个格子，并开始移动
            nextBuilding.currentItem = newItem;
            newItem.hasArrived = false; // 强行触发移动状态
            
            // 恢复机器为空闲状态
            myData.isProcessing = false;
            currentState = State.Idle;
        }
    }
}