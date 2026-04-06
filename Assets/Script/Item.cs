using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ProcessTransition
{
    public string machineID;        // 遇到什么机器
    public GameObject outputPrefab; // 会变成什么预制体
}

public class Item : MonoBehaviour
{
    public float moveSpeed = 2.0f;
    public float verticalOffset = 1.0f; 
    
    private Vector3Int currentGridPos;
    private Vector3 targetWorldPos;
    private bool isMoving = false;
    public bool hasArrived = true;
    
    [Header("Processing Recipes")]
    public List<ProcessTransition> transitions;

    public void Init(Vector3Int startGridPos)
    {
        currentGridPos = startGridPos;
        transform.position = GridManager.Instance.GetWorldPosition(startGridPos) + Vector3.up * verticalOffset;
        
        // 初始时把自己注册到当前格子的占位中
        BuildingData currentBuilding = GridManager.Instance.GetBuildingAt(currentGridPos);
        if (currentBuilding != null) currentBuilding.currentItem = this;
    }
    
    public void InitForOutput(Vector3Int startGridPos, Vector3Int targetGridPos, BuildingData targetBuilding)
    {
        currentGridPos = startGridPos;
        // 初始物理位置在机器中心
        transform.position = GridManager.Instance.GetWorldPosition(startGridPos) + Vector3.up * verticalOffset;

        targetBuilding.currentItem = this;

        // 设定移动目标，并强行激活移动状态
        targetWorldPos = GridManager.Instance.GetWorldPosition(targetGridPos) + Vector3.up * verticalOffset;
        isMoving = true;
        hasArrived = false;
    }

    void Update()
    {
        if (isMoving)
        {
            MoveToTarget();
        }
        else
        {
            TryMoveForward();
        }
    }

    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPos) < 0.01f)
        {
            isMoving = false;
            hasArrived = true;
            currentGridPos = GridManager.Instance.GetGridPosition(transform.position);
        }
    }


    private void TryMoveForward()
    {
        BuildingData currentBuilding = GridManager.Instance.GetBuildingAt(currentGridPos);
        if (currentBuilding == null) return;

        // 将自身传给机器，以便机器进行策略预判
        Vector3 outDir = currentBuilding.GetOutputDirectionVector(currentGridPos, this);
        if (outDir == Vector3.zero) return;

        Vector3Int nextGridPos = currentGridPos + new Vector3Int(Mathf.RoundToInt(outDir.x), 0, Mathf.RoundToInt(outDir.z));
        BuildingData nextBuilding = GridManager.Instance.GetBuildingAt(nextGridPos);

        // 将自身传给下一个建筑
        if (nextBuilding != null && nextBuilding.CanAcceptInput(this, outDir))
        {
            nextBuilding.currentItem = this;
            currentBuilding.currentItem = null;

            targetWorldPos = GridManager.Instance.GetWorldPosition(nextGridPos) + Vector3.up * verticalOffset;
            isMoving = true;
            hasArrived = false; 
        }
    }
    
    // 辅助方法
    public bool CanBeProcessedBy(string machineID)
    {
        foreach (var t in transitions)
        {
            if (t.machineID == machineID) return true;
        }
        return false;
    }

    public GameObject GetOutputPrefab(string machineID)
    {
        foreach (var t in transitions)
        {
            if (t.machineID == machineID) return t.outputPrefab;
        }
        return null;
    }
}