using UnityEngine;
using System.Collections.Generic;

public class ProcessorMachine : MonoBehaviour
{
    [Header("Global Data")]
    public RecipeDatabaseSO recipeDatabase;

    private BuildingData myData;
    private float processTimer = 0f;
    private AlchemyRecipe activeRecipe; // 确定要炼制的配方

    private List<string> currentInventory = new List<string>();
    private enum State { Idle, Processing, WaitingToOutput }
    private State currentState = State.Idle;
    private Vector3Int myGridPos;

    void Start()
    {
        myData = GetComponent<BuildingData>();
		myGridPos = myData.anchorGridPos;
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Idle: CheckForInput(); break;
            case State.Processing: DoProcess(); break;
            case State.WaitingToOutput: TryOutput(); break;
        }
    }

    // 判断如果吃下这个材料，是否还有可能合成某个配方（判断子集）
    public bool NeedsIngredient(string incomingItemID)
    {
        if (currentState != State.Idle) return false;

        // 模拟吃下这个材料后的库存状态
        List<string> testInventory = new List<string>(currentInventory);
        testInventory.Add(incomingItemID);

        // 遍历全局配方库，只要这个模拟库存是任意一个配方的子集，就允许进入
        foreach (var recipe in recipeDatabase.allRecipes)
        {
            if (IsSubset(testInventory, recipe.requiredInputs))
            {
                return true;
            }
        }
        return false;
    }

    // 集合比对逻辑
    private bool IsSubset(List<string> subset, List<string> superset)
    {
        Dictionary<string, int> superCounts = new Dictionary<string, int>();
        foreach (var item in superset)
        {
            if (!superCounts.ContainsKey(item)) superCounts[item] = 0;
            superCounts[item]++;
        }

        foreach (var item in subset)
        {
            if (!superCounts.ContainsKey(item)) return false; // 配方里不需要这个
            superCounts[item]--;
            if (superCounts[item] < 0) return false; // 超出配方需求
        }
        return true;
    }

    private void CheckForInput()
    {
        if (myData.currentItem != null && myData.currentItem.hasArrived)
        {
            string incomingID = myData.currentItem.itemID;

            if (NeedsIngredient(incomingID))
            {
                currentInventory.Add(incomingID);
                Destroy(myData.currentItem.gameObject);
                myData.currentItem = null;

                foreach (var recipe in recipeDatabase.allRecipes)
                {
                    // 数量一致且是子集，说明完美匹配
                    if (currentInventory.Count == recipe.requiredInputs.Count && IsSubset(currentInventory, recipe.requiredInputs))
                    {
                        activeRecipe = recipe;
                        myData.isProcessing = true;
                        currentState = State.Processing;
                        processTimer = 0f;
                        Debug.Log($"【图鉴匹配成功】开始炼制：{activeRecipe.recipeName}");
                        break; // 找到就跳出
                    }
                }
            }
        }
    }

    private void DoProcess()
    {
        processTimer += Time.deltaTime;
        if (processTimer >= activeRecipe.processTime)
        {
            currentState = State.WaitingToOutput;
            currentInventory.Clear();
        }
    }

    private void TryOutput()
    {
        Item nextItemComp = activeRecipe.outputPrefab.GetComponent<Item>();

        if (myData.ports != null && myData.ports.Count > 0)
        {
            foreach (var port in myData.ports)
            {
                if (port.type != PortType.Output) continue;

                Vector2Int rotatedOffset = myData.RotateOffset(port.localCellOffset, myData.layoutDirection);
                Vector3Int portWorldPos = myGridPos + new Vector3Int(rotatedOffset.x, 0, rotatedOffset.y);

                Vector3 outDir = myData.GetWorldDirection(port.portDirection, myData.layoutDirection);
                
                Vector3Int nextGridPos = portWorldPos + new Vector3Int(Mathf.RoundToInt(outDir.x), 0, Mathf.RoundToInt(outDir.z));
                BuildingData nextBuilding = GridManager.Instance.GetBuildingAt(nextGridPos);

                // 让门外的建筑检查
                if (nextBuilding != null && nextBuilding.CanAcceptInput(nextItemComp, outDir, nextGridPos))
                {
                    GameObject newObj = Instantiate(activeRecipe.outputPrefab);
                    Item newItem = newObj.GetComponent<Item>();
                    
                    // 发往下一个格子
                    newItem.InitForOutput(portWorldPos, nextGridPos, nextBuilding);

                    myData.isProcessing = false;
                    currentState = State.Idle;
                    return; // 吐出一个就结束当前循环
                }
            }
        }
        else
        {
            Vector3 outDir = myData.GetOutputDirectionVector(myGridPos, nextItemComp);
            if (outDir == Vector3.zero) return;

            Vector3Int nextGridPos = myGridPos + new Vector3Int(Mathf.RoundToInt(outDir.x), 0, Mathf.RoundToInt(outDir.z));
            BuildingData nextBuilding = GridManager.Instance.GetBuildingAt(nextGridPos);

            if (nextBuilding != null && nextBuilding.CanAcceptInput(nextItemComp, outDir, nextGridPos))
            {
                GameObject newObj = Instantiate(activeRecipe.outputPrefab);
                Item newItem = newObj.GetComponent<Item>();
                newItem.InitForOutput(myGridPos, nextGridPos, nextBuilding);

                myData.isProcessing = false;
                currentState = State.Idle;
            }
        }
    }
}