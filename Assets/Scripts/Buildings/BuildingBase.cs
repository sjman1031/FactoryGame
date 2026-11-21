using UnityEngine;

/// <summary>모든 건물의 공통 계약.</summary>
public abstract class BuildingBase : MonoBehaviour
{
    // 직렬화하지 않는다 — 프리팹이나 씬에 값이 박히면 복제한 건물끼리 id가 겹쳐 저장에서 하나가 빠진다.
    // 값은 Awake(새로 배치)나 Deserialize(저장에서 복원)에서만 정해진다.
    [System.NonSerialized] public string instanceId;

    private Renderer[] _renderers;

    // Deserialize가 Awake보다 먼저 호출될 수 있으므로
    // Awake에서 instanceId를 발급하되, 이미 Deserialize로 설정된 값이 있으면 유지.
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

    // Start(): Awake 이후 보장 → instanceId와 Deserialize 데이터가 이미 세팅된 상태
    protected virtual void Start()
    {
        TickManager.Instance.Register(this);
        BuildingRegistry.Register(instanceId, this);

        // 저장에서 복원된 건물은 발밑 청크가 아직 안 올라왔을 수 있다.
        // 숨겨 두면 그 청크가 로드될 때 WorldStreamingManager가 다시 보이게 한다.
        var world = WorldStreamingManager.Instance;
        if (world != null && !world.IsChunkLoaded(ChunkUtils.WorldToChunk(transform.position)))
            SetVisible(false);

        OnBuildingPlaced();
    }

    protected virtual void OnDestroy()
    {
        TickManager.Instance?.Unregister(this);
        BuildingRegistry.Unregister(instanceId, this);
    }

    public abstract void OnBuildingPlaced();
    public abstract void Tick(float deltaTime);

    /// <summary>렌더러만 켜고 끈다. 숨겨도 Tick은 계속 받는다.</summary>
    public void SetVisible(bool visible)
    {
        _renderers ??= GetComponentsInChildren<Renderer>(true);
        foreach (var r in _renderers) r.enabled = visible;
    }

    public virtual BuildingSaveData Serialize() => new BuildingSaveData
    {
        buildingId = GetType().Name,
        instanceId = instanceId,
        position   = transform.position,
        rotation   = transform.rotation,
    };

    // Deserialize는 Instantiate 직후 BuildingRegistry.RestoreAll에서 호출됨.
    // Unity 실행 순서: Instantiate → Awake → (이 프레임 끝) → Start
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

/// <summary>아이템을 내보낼 수 있는 건물 (채굴기·제작기·창고).</summary>
public interface IItemOutput { bool TryOutputItem(out ItemData item); }
/// <summary>아이템을 받을 수 있는 건물 (제작기·창고·벨트).</summary>
public interface IItemInput  { bool TryInputItem(ItemData item); }
