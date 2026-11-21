using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저장 파일의 id 문자열 → ScriptableObject 조회.
/// DataImporter가 임포트할 때마다 Assets/Resources/GameDatabase.asset을 갱신하고,
/// 런타임은 Resources에서 한 번 읽어 쓴다 — 씬에 연결할 것이 없다.
/// </summary>
public class GameDatabase : ScriptableObject
{
    public const string RESOURCE_PATH = "GameDatabase";

    public List<ItemData>         items   = new();
    public List<RecipeData>       recipes = new();
    public List<ResourceNodeData> nodes   = new();

    private Dictionary<string, ItemData>         _itemMap;
    private Dictionary<string, RecipeData>       _recipeMap;
    private Dictionary<string, ResourceNodeData> _nodeMap;

    private static GameDatabase _instance;
    private static bool         _missingLogged;

    public static ItemData GetItem(string itemId)
        => !string.IsNullOrEmpty(itemId) && Load() is GameDatabase db
           && db._itemMap.TryGetValue(itemId, out var v) ? v : null;

    public static RecipeData GetRecipe(string recipeId)
        => !string.IsNullOrEmpty(recipeId) && Load() is GameDatabase db
           && db._recipeMap.TryGetValue(recipeId, out var v) ? v : null;

    /// <summary>광물 id(OreDefinition.id)와 같은 nodeId를 가진 노드 데이터.</summary>
    public static ResourceNodeData GetNode(string nodeId)
        => !string.IsNullOrEmpty(nodeId) && Load() is GameDatabase db
           && db._nodeMap.TryGetValue(nodeId, out var v) ? v : null;

    private static GameDatabase Load()
    {
        if (_instance != null) return _instance;

        _instance = Resources.Load<GameDatabase>(RESOURCE_PATH);
        if (_instance == null)
        {
            if (!_missingLogged)
                Debug.LogError("[GameDatabase] Resources/GameDatabase.asset이 없습니다. Tools › Data Importer에서 전체 임포트를 실행하세요.");
            _missingLogged = true;
            return null;
        }

        _instance.BuildMaps();
        return _instance;
    }

    private void BuildMaps()
    {
        _itemMap = new();
        foreach (var i in items)   if (i != null && !string.IsNullOrEmpty(i.itemId))   _itemMap[i.itemId]     = i;
        _recipeMap = new();
        foreach (var r in recipes) if (r != null && !string.IsNullOrEmpty(r.recipeId)) _recipeMap[r.recipeId] = r;
        _nodeMap = new();
        foreach (var n in nodes)   if (n != null && !string.IsNullOrEmpty(n.nodeId))   _nodeMap[n.nodeId]     = n;
    }

    // Enter Play Mode에서 Domain Reload를 꺼도 이전 플레이의 캐시가 남지 않게 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; _missingLogged = false; }
}
