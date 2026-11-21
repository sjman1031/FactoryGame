using UnityEngine;

/// <summary>
/// 모든 건물의 공통 베이스
/// Start/OnDestroy에서 TickManager 등록/해제
/// </summary>
public abstract class BuildingBase : MonoBehaviour
{
    // 청크 경계 연결용 고유 ID
    public string instanceId;

    private Renderer[] _renderers;

    // Deserialize가 Awake보다 먼저 호출될 수 있으므로
    // Awake에서 instanceId를 발급하되, 이미 Deserialize로 설정된 값이 있으면 유지
    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(instanceId))
            instanceId = System.Guid.NewGuid().ToString();

        // 배치·복원 어느 경로로 생겨도 Building 레이어가 되도록 여기서 정한다.
        // 자식까지 바꿔야 자식에 달린 콜라이더도 BuildingPlacer의 겹침 검사에 걸린다.
        int layer = LayerMask.NameToLayer("Building");
        if (layer >= 0)
            foreach (var t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
    }

    // Start(): Awake 이후 보장 -> instanceId와 Deserialize 데이터가 이미 세팅된 상태
    protected virtual void Start()
    {
        TickManager.Instance.Register(this);
        BuildingRegistry.Register(instanceId, this);

        // 저장에서 복원된 건물은 청크가 아직 안 로드된 상태일 수 있으므로
        // 청크가 로드되지 않은 상태라면 렌더러를 끈다. (청크 로드 시 다시 켜짐)
        var world = WorldStreamingManager.Instance;
        if(world != null && !world.IsChunkLoaded(ChunkUtils.WorldToChunk(transform.position)))
            SetVisible(false);

        OnBuildingPlaced();
    }

    protected virtual void OnDestroy()
    {
        TickManager.Instance?.UnRegister(this);
        BuildingRegistry.Unregister(instanceId);
    }

    /// <summary> 배치 직후 1회 초기화. Start()에서 호출 </summary> 
    public abstract void OnBuildingPlaced();
    
    /// <summary> TickManager가 _tickInterval 마다 호출 </summary>
    public abstract void Tick(float deltaTime);
    
    public void SetVisible(bool visible)
    {
        _renderers ??= GetComponentsInChildren<Renderer>(true);
        foreach (var r in _renderers) r.enabled = visible;
    }

    // 직렬화
    public virtual BuildingSaveData Serialize()
    {
        return new BuildingSaveData
        {
            buildingId = GetType().Name,
            instanceId = instanceId,
            position   = transform.position,
            rotation   = transform.rotation
        };
    }

    // Deserialize는 Instantiate 직후 RestoreChunk에서 호출됨.
    // Unity 실행 순서: Instantiate -> Awake -> (이 프레임 끝) -> Start
    // Deserialize는 Awake 직후, Start 이전에 호출되므로
    // instanceId를 여기서 덮어쓰면 Start()의 Register에 올바른 값이 전달됨.
    // OnBuildingPlaced()는 Start()에서 한 번만 호출되므로 중복 없음.
    public virtual void Deserialize(BuildingSaveData data)
    {
        instanceId         = data.instanceId;
        transform.position = data.position;
        transform.rotation = data.rotation;
    }
}

// 컨베이어 연결 계약
/// <summary> 아이템을 내보낼 수 있는 건물 (채굴기, 제작기, 창고) </summary>
public interface IItemOutput
{
    bool TryOutputItem(out ItemData item);
}

/// <summary> 아이템을 받아들일 수 있는 건물 (제작기, 창고) </summary>
public interface IItemInput
{
    bool TryInputItem(ItemData item);
}