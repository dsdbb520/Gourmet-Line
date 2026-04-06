using UnityEngine;

public class ProcessorMachine : MonoBehaviour
{
    [Header("Processing Settings")]
    public string machineID = "Oven_A"; // 定义这台机器的唯一ID
    public float processTime = 2.0f; // 加工时间
    
    private GameObject cachedOutputPrefab; // 暂存当前正在加工的物品的产物预制体

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
        if (myData.currentItem != null && myData.currentItem.hasArrived)
        {
            // 吞入前，询问小球它经过我这台机器会变成什么，存下来
            cachedOutputPrefab = myData.currentItem.GetOutputPrefab(machineID);
            
            myData.isProcessing = true;
            Destroy(myData.currentItem.gameObject);
            myData.currentItem = null; 
            currentState = State.Processing;
            processTimer = 0f;
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
        // 获取将要生成的产物身上的Item组件，用于路况预判
        Item nextItemComp = cachedOutputPrefab.GetComponent<Item>();

        // 传入将要产出的物品进行方向检测
        Vector3 outDir = myData.GetOutputDirectionVector(myGridPos, nextItemComp);
        if (outDir == Vector3.zero) return; 

        Vector3Int nextGridPos = myGridPos + new Vector3Int(Mathf.RoundToInt(outDir.x), 0, Mathf.RoundToInt(outDir.z));
        BuildingData nextBuilding = GridManager.Instance.GetBuildingAt(nextGridPos);

        // 让下一个建筑判断是否能接受这件即将生成的新产物
        if (nextBuilding != null && nextBuilding.CanAcceptInput(nextItemComp, outDir))
        {
            // 确认道路畅通且接受产物后，再真正执行实例化
            GameObject newObj = Instantiate(cachedOutputPrefab);
            Item newItem = newObj.GetComponent<Item>();
            
            newItem.InitForOutput(myGridPos, nextGridPos, nextBuilding);
            
            nextBuilding.currentItem = newItem;
            newItem.hasArrived = false; 
            
            myData.isProcessing = false;
            currentState = State.Idle;
        }
    }
}