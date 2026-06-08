using UnityEngine;

/// <summary>모든 건물의 공통 계약.</summary>
public abstract class BuildingBase : MonoBehaviour
{
    public string instanceId;

    // Deserialize 복원 중 Start()의 OnBuildingPlaced() 중복 호출 방지
    private bool _isRestoring;

    protected virtual void Start()
    {
        if (string.IsNullOrEmpty(instanceId))
            instanceId = System.Guid.NewGuid().ToString();

        TickManager.Instance.Register(this);
        BuildingRegistry.Register(instanceId, this);

        // 복원 중이면 Deserialize()에서 OnBuildingPlaced()를 호출하므로 여기선 스킵
        if (!_isRestoring)
            OnBuildingPlaced();
    }

    protected virtual void OnDestroy()
    {
        TickManager.Instance?.Unregister(this);
        BuildingRegistry.Unregister(instanceId);
    }

    public abstract void OnBuildingPlaced();
    public abstract void Tick(float deltaTime);

    public virtual BuildingSaveData Serialize() => new BuildingSaveData
    {
        buildingId = GetType().Name,
        instanceId = instanceId,
        position   = transform.position,
        rotation   = transform.rotation,
    };

    public virtual void Deserialize(BuildingSaveData data)
    {
        _isRestoring       = true;
        instanceId         = data.instanceId;
        transform.position = data.position;
        transform.rotation = data.rotation;
        // Start()가 아직 안 불렸을 수도 있으므로 OnBuildingPlaced는
        // Start() 이후 시점에 보장하기 위해 _isRestoring 플래그로 제어.
        // Start()에서 _isRestoring=true를 감지해 OnBuildingPlaced를 스킵하고,
        // 여기서 한 번만 호출한다.
        OnBuildingPlaced();
        _isRestoring = false;
    }
}

public interface IItemOutput { bool TryOutputItem(out ItemData item); }
public interface IItemInput  { bool TryInputItem(ItemData item); }
