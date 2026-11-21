using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광맥 위에 설치해 아이템을 주기적으로 생산하는 채굴기.
/// 놓인 칸의 광물별 확률대로 여러 광물을 섞어 캔다.
/// </summary>
public class MinerBuilding : BuildingBase, IItemOutput
{
    [Header("채굴기 설정")]
    [Range(0.5f, 2f)] public float efficiency = 1f;

    // 이 칸에서 나오는 광물과 광물별 분당 산출량 (= 기본 산출량 × 순도 × 효율 × 확률)
    private readonly List<(ItemData item, float perMinute)> _yields = new();
    private float    _totalPerMinute;
    private float    _timer;
    private float    _interval;
    private ItemData _pendingItem;

    private static readonly List<(string oreId, float probability)> _oreBuffer = new();

    // 배치·복원 모두 Start에서 한 번 호출된다. 위치만으로 다시 계산하므로 저장할 것이 없다.
    public override void OnBuildingPlaced()
    {
        _yields.Clear();
        _totalPerMinute = 0f;

        Vector3 p = transform.position;
        ChunkGenerator.GetOreDistribution(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z), p.y, _oreBuffer);

        foreach (var (oreId, probability) in _oreBuffer)
        {
            var node = GameDatabase.GetNode(oreId);
            if (node == null || node.item == null)
            {
                Debug.LogWarning($"[MinerBuilding] 광물 '{oreId}'의 ResourceNodeData가 없습니다 — CSV의 노드 id를 확인하세요.");
                continue;
            }
            float perMinute = node.GetActualOutput(efficiency) * probability;
            _yields.Add((node.item, perMinute));
            _totalPerMinute += perMinute;
        }

        if (_totalPerMinute <= 0f) { Debug.LogWarning($"[MinerBuilding] {name} — 이 칸에 광맥 없음"); return; }
        _interval = 60f / _totalPerMinute;
    }

    public override void Tick(float deltaTime)
    {
        if (_totalPerMinute <= 0f || _pendingItem != null) return;
        _timer += deltaTime;
        if (_timer < _interval) return;
        _timer -= _interval;
        _pendingItem = PickItem();
    }

    // 한 번 캘 때 나올 광물을 분당 산출량 비율로 뽑는다.
    // 그래서 광물별 기대 산출량이 '기본 산출량 × 순도 × 효율 × 확률'과 같아진다.
    private ItemData PickItem()
    {
        float r = UnityEngine.Random.value * _totalPerMinute;
        foreach (var (item, perMinute) in _yields)
        {
            if (r < perMinute) return item;
            r -= perMinute;
        }
        return _yields[_yields.Count - 1].item;
    }

    public bool TryOutputItem(out ItemData item)
    {
        if (_pendingItem == null) { item = null; return false; }
        item = _pendingItem; _pendingItem = null; return true;
    }
}
