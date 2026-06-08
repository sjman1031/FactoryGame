using UnityEngine;

// ── MinerBuilding ─────────────────────────────────────────────
/// <summary>자원 노드 위에 설치해 아이템을 주기적으로 생산하는 채굴기.</summary>
public class MinerBuilding : BuildingBase, IItemOutput
{
    [Header("채굴기 설정")]
    public ResourceNodeData targetNode;
    [Range(0.5f, 2f)] public float efficiency = 1f;

    private float    _timer;
    private float    _interval;
    private ItemData _pendingItem;

    public override void OnBuildingPlaced()
    {
        if (targetNode == null) { Debug.LogWarning($"[Miner] {name} — targetNode 없음"); return; }
        _interval = 60f / targetNode.GetActualOutput(efficiency);
    }

    public override void Tick(float deltaTime)
    {
        if (targetNode == null || _pendingItem != null) return;
        _timer += deltaTime;
        if (_timer < _interval) return;
        _timer -= _interval;
        _pendingItem = targetNode.item;
    }

    public bool TryOutputItem(out ItemData item)
    {
        if (_pendingItem == null) { item = null; return false; }
        item = _pendingItem; _pendingItem = null; return true;
    }
}

// ── ManufacturerBuilding ──────────────────────────────────────
/// <summary>레시피에 따라 재료를 자동 소모하고 결과물을 생산하는 제작기.</summary>
public class ManufacturerBuilding : BuildingBase, IItemInput, IItemOutput
{
    [Header("제작기 설정")]
    public RecipeData currentRecipe;

    private Inventory _inputBuffer;
    private Inventory _outputBuffer;
    private float     _craftTimer;
    private bool      _isCrafting;

    public override void OnBuildingPlaced()
    {
        _inputBuffer  = new Inventory(8);
        _outputBuffer = new Inventory(4);
    }

    public override void Tick(float deltaTime)
    {
        if (currentRecipe == null) return;
        if (!_isCrafting) TryStartCraft();
        if (_isCrafting)
        {
            _craftTimer += deltaTime;
            if (_craftTimer >= currentRecipe.craftTime) FinishCraft();
        }
    }

    private void TryStartCraft()
    {
        if (_outputBuffer.GetAmount(currentRecipe.output) + currentRecipe.outputAmount
            > currentRecipe.output.stackSize) return;
        if (!currentRecipe.CanCraft(_inputBuffer)) return;
        _inputBuffer.TryConsumeRecipe(currentRecipe);
        _craftTimer = 0f; _isCrafting = true;
    }

    private void FinishCraft()
    {
        _outputBuffer.TryAdd(currentRecipe.output, currentRecipe.outputAmount);
        _isCrafting = false;
    }

    public bool TryInputItem(ItemData item)
    {
        foreach (var ing in currentRecipe.inputs)
            if (ing.item == item) return _inputBuffer.TryAdd(item, 1);
        return false;
    }

    public bool TryOutputItem(out ItemData item)
    {
        if (currentRecipe == null || _outputBuffer.GetAmount(currentRecipe.output) <= 0)
        { item = null; return false; }
        item = currentRecipe.output;
        _outputBuffer.TryRemove(item, 1);
        return true;
    }

    public override BuildingSaveData Serialize()
    {
        var data = base.Serialize();
        data.recipeId = currentRecipe != null ? currentRecipe.recipeId : "";
        return data;
    }

    public void SetRecipe(RecipeData recipe)
    {
        currentRecipe = recipe; _isCrafting = false; _craftTimer = 0f;
    }
}
