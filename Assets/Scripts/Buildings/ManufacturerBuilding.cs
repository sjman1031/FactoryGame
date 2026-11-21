using System.Collections.Generic;
using UnityEngine;

/// <summary>레시피에 따라 재료를 자동 소모하고 결과물을 생산하는 제작기.</summary>
public class ManufacturerBuilding : BuildingBase, IItemInput, IItemOutput
{
    [Header("제작기 설정")]
    public RecipeData currentRecipe;

    // 이웃 벨트가 Start보다 먼저 배달할 수 있으므로(같은 프레임에 배치·복원된 경우) 생성과 동시에 만든다
    private readonly Inventory _inputBuffer  = new(8);
    private readonly Inventory _outputBuffer = new(4);
    private float     _craftTimer;
    private bool      _isCrafting;

    // 저장된 recipeId를 GameDatabase에서 찾지 못했을 때 원본 그대로 보관 — 다음 저장에서 지워지지 않게
    private string _unresolvedRecipeId;

    private static readonly List<ItemData> _staleBuffer = new();

    public override void OnBuildingPlaced() { }

    public override void Tick(float deltaTime)
    {
        if (!HasValidRecipe) return;
        if (!_isCrafting) TryStartCraft();
        if (_isCrafting)
        {
            _craftTimer += deltaTime;
            if (_craftTimer >= currentRecipe.craftTime) FinishCraft();
        }
    }

    // output이 비어 있는 레시피(CSV id 오타 등)는 동작시키지 않는다 — Tick에서 예외가 나지 않게
    private bool HasValidRecipe => currentRecipe != null && currentRecipe.output != null;

    private void TryStartCraft()
    {
        // 출력 버퍼에 결과물이 들어갈 자리가 없으면 시작하지 않는다 (재료만 소모되고 결과물이 사라지는 것 방지)
        if (_outputBuffer.GetAddableAmount(currentRecipe.output) < currentRecipe.outputAmount) return;
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
        if (!HasValidRecipe || item == null) return false;
        if (!IsIngredient(currentRecipe, item)) return false;
        // 재료 하나가 입력 버퍼 8칸을 다 차지하면 다른 재료가 못 들어와 제작이 영원히 멈춘다.
        // 재료마다 한 스택까지만 받는다.
        if (_inputBuffer.GetAmount(item) >= item.stackSize) return false;
        return _inputBuffer.TryAdd(item, 1);
    }

    public bool TryOutputItem(out ItemData item)
    {
        // 레시피를 바꾼 뒤 입력 버퍼에 남은 이전 재료를 먼저 내보낸다 — 슬롯이 영구히 막히지 않게
        if (TryTakeStaleInput(out item)) return true;

        if (!HasValidRecipe || _outputBuffer.GetAmount(currentRecipe.output) <= 0)
        { item = null; return false; }
        item = currentRecipe.output;
        _outputBuffer.TryRemove(item, 1);
        return true;
    }

    private bool TryTakeStaleInput(out ItemData item)
    {
        _staleBuffer.Clear();
        foreach (var kv in _inputBuffer.GetAll())
            if (currentRecipe == null || !IsIngredient(currentRecipe, kv.Key)) _staleBuffer.Add(kv.Key);

        if (_staleBuffer.Count == 0) { item = null; return false; }
        item = _staleBuffer[0];
        _inputBuffer.TryRemove(item, 1);
        return true;
    }

    private static bool IsIngredient(RecipeData recipe, ItemData item)
    {
        foreach (var ing in recipe.inputs)
            if (ing.item == item) return true;
        return false;
    }

    public override BuildingSaveData Serialize()
    {
        var data = base.Serialize();
        data.recipeId = currentRecipe != null ? currentRecipe.recipeId : _unresolvedRecipeId ?? "";
        return data;
    }

    // Serialize가 저장한 recipeId를 되살린다
    public override void Deserialize(BuildingSaveData data)
    {
        base.Deserialize(data);
        if (string.IsNullOrEmpty(data.recipeId)) return;

        currentRecipe = GameDatabase.GetRecipe(data.recipeId);
        if (currentRecipe == null)
        {
            _unresolvedRecipeId = data.recipeId;
            Debug.LogWarning($"[ManufacturerBuilding] 레시피 '{data.recipeId}'를 찾지 못했습니다 — id를 보관해 두고 다음 저장에 그대로 씁니다.", this);
        }
    }

    public void SetRecipe(RecipeData recipe)
    {
        // 제작 중이었다면 이미 소모한 재료를 입력 버퍼로 되돌린다 (이전에는 그대로 사라졌음)
        if (_isCrafting && currentRecipe != null)
            foreach (var ing in currentRecipe.inputs)
                if (ing.item != null) _inputBuffer.TryAdd(ing.item, ing.amount);

        currentRecipe       = recipe;
        _unresolvedRecipeId = null;
        _isCrafting         = false;
        _craftTimer         = 0f;
        // 새 레시피에 없는 재료는 TryOutputItem이 벨트로 먼저 내보낸다
    }
}
