using UnityEngine;

public class Item : MonoBehaviour
{
    public float moveSpeed = 2.0f;
    public float verticalOffset = 1.0f; 
    
    private Vector3Int currentGridPos;
    private Vector3 targetWorldPos;
    private bool isMoving = false;
    public bool hasArrived = true;

    public void Init(Vector3Int startGridPos)
    {
        currentGridPos = startGridPos;
        transform.position = GridManager.Instance.GetWorldPosition(startGridPos) + Vector3.up * verticalOffset;
        
        // 初始时把自己注册到当前格子的占位中
        BuildingData currentBuilding = GridManager.Instance.GetBuildingAt(currentGridPos);
        if (currentBuilding != null) currentBuilding.currentItem = this;
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

        // 传入当前坐标，让策略去计算方向
        Vector3 outDir = currentBuilding.GetOutputDirectionVector(currentGridPos);
    
        // 如果策略返回零向量，直接结束，继续占着当前位置
        if (outDir == Vector3.zero) return;

        Vector3Int nextGridPos = currentGridPos + new Vector3Int(Mathf.RoundToInt(outDir.x), 0, Mathf.RoundToInt(outDir.z));
        BuildingData nextBuilding = GridManager.Instance.GetBuildingAt(nextGridPos);

        if (nextBuilding != null && nextBuilding.CanAcceptInput())
        {
            nextBuilding.currentItem = this;
            currentBuilding.currentItem = null;

            targetWorldPos = GridManager.Instance.GetWorldPosition(nextGridPos) + Vector3.up * verticalOffset;
            isMoving = true;
            hasArrived = false;
        }
    }
}