using UnityEngine;

/// <summary>
/// 레시피에 따라 재료를 자동 소모하고 결과물을 생산하는 건물
/// 입력 / 출력 버퍼를 각각 Inventory로 관리
/// </summary> 
public class ManufactureBuilding : BuildingBase, IItemInput, IItemOutput
{
    [Header("제작기 설정")]
    public RecipeData currentRecipe;

    private Inventory _inputBuffer; // 재료 대기 버퍼
    private Inventory _outputBuffer; // 결과물 대기 버퍼
    private float _craftTimer;
    private bool _isCrafting;

    public override void OnBuildingPlaced()
    {
        _inputBuffer = new Inventory(8);
        _outputBuffer = new Inventory(4);
    }

    public override void Tick(float deltaTime)
    {
        if (currentRecipe == null) return;

        if (!_isCrafting)
            TryStartCraft();

        if (_isCrafting)
        {
            _craftTimer += deltaTime;
            if (_craftTimer >= currentRecipe.craftingTime)
                FinishCraft();
        }
    }

    public void TryStartCraft()
    {
        // 출력 버퍼가 가득 차면 대기
        if (_outputBuffer.GetAmount(currentRecipe.output) + currentRecipe.outputAmount
            > currentRecipe.output.stackSize) return;

        if (!currentRecipe.CanCraft(_inputBuffer)) return;

        _inputBuffer.TryConsumeRecipe(currentRecipe);
        _craftTimer = 0f;
        _isCrafting = true;
    }

    public void FinishCraft()
    {
        _outputBuffer.TryAdd(currentRecipe.output, currentRecipe.outputAmount);
        _isCrafting = false;
    }

    // IItemInput - 컨베이어가 재료를 넣을 때
    public bool TryInputItem(ItemData item)
    {
        if (currentRecipe == null)
            return false;

        // 같은 아이템이 inputs에 여러 번 등장할 수 있으므로(예: CSV로 2×철판을 두 행으로 정의)
        // 첫 번째 매칭에서 즉시 반환하지 않고, 버퍼에 실제로 추가 가능한 슬롯을 찾는다.
        foreach (var ingredient in currentRecipe.inputs)
        {
            if (ingredient.item != item) continue;
            if (_inputBuffer.TryAdd(item, 1)) return true;
        }

        return false; // 레시피에 없는 아이템은 거부
    }

    // IItemOutput - 컨베이어가 결과물을 가져갈 때
    public bool TryOutputItem(out ItemData item)
    {
        if (currentRecipe == null
            || _outputBuffer.GetAmount(currentRecipe.output) <= 0)
        {
            item = null;
            return false;
        }

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

    /// <summary> 런타임에 레시피 변경 시 호출 </summary>
    public void SetRecipe(RecipeData recipe)
    {
        currentRecipe = recipe;
        _isCrafting   = false; // 레시피 변경 시 제작 중단
        _craftTimer   = 0f;
    }

}