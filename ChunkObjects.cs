using System.Collections.Generic;
using UnityEngine;

// ── ChunkInstance ─────────────────────────────────────────────
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkInstance : MonoBehaviour
{
    public ChunkData Data { get; private set; }
    private MeshFilter _filter; private MeshCollider _collider;

    private void Awake()
    {
        _filter   = GetComponent<MeshFilter>();
        _collider = GetComponent<MeshCollider>();
    }

    public void BuildMesh(ChunkData data)
    {
        Data = data;
        int size  = ChunkUtils.CHUNK_SIZE;
        var verts = new Vector3[size * size];
        var tris  = new int[(size - 1) * (size - 1) * 6];
        var uvs   = new Vector2[size * size];

        for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
        {
            int i    = z * size + x;
            verts[i] = new Vector3(x, data.GetHeight(x, z), z);
            uvs[i]   = new Vector2((float)x / size, (float)z / size);
        }

        int t = 0;
        for (int z = 0; z < size - 1; z++) for (int x = 0; x < size - 1; x++)
        {
            int i = z * size + x;
            tris[t++] = i; tris[t++] = i + size; tris[t++] = i + 1;
            tris[t++] = i + 1; tris[t++] = i + size; tris[t++] = i + size + 1;
        }

        var mesh = new Mesh { name = $"Chunk_{data.coord}" };
        mesh.vertices = verts; mesh.triangles = tris; mesh.uv = uvs;
        mesh.RecalculateNormals();
        _filter.mesh = mesh; _collider.sharedMesh = mesh;
    }
}

// ── BuildingCatalog ───────────────────────────────────────────
public class BuildingCatalog : MonoBehaviour
{
    [System.Serializable]
    public class Entry { public string buildingId; public BuildingBase prefab; }

    [SerializeField] private List<Entry> _entries = new();
    private Dictionary<string, BuildingBase> _map;

    private void Awake()
    {
        _map = new();
        foreach (var e in _entries) _map[e.buildingId] = e.prefab;
    }

    public BuildingBase Get(string id) => _map.TryGetValue(id, out var p) ? p : null;
}

// ── BuildingRegistry ──────────────────────────────────────────
public static class BuildingRegistry
{
    private static readonly Dictionary<Vector2Int, List<BuildingSaveData>> _registry = new();
    private static readonly Dictionary<string, BuildingBase>               _live      = new();

    public static void Register(string id, BuildingBase b)   => _live[id]  = b;
    public static void Unregister(string id)                  => _live.Remove(id);
    public static BuildingBase FindById(string id)            => _live.TryGetValue(id, out var b) ? b : null;

    // 키 복사 버퍼 재사용 — SerializeChunk 호출마다 new List 생성 방지
    private static readonly List<string> _keyBuffer = new();

    public static void SerializeChunk(Vector2Int coord)
    {
        var list = new List<BuildingSaveData>();

        _keyBuffer.Clear();
        _keyBuffer.AddRange(_live.Keys);

        foreach (var id in _keyBuffer)
        {
            if (!_live.TryGetValue(id, out var building) || building == null) continue;
            if (ChunkUtils.WorldToChunk(building.transform.position) != coord) continue;
            list.Add(building.Serialize());
            building.gameObject.SetActive(false);
        }
        _registry[coord] = list;
    }

    public static void RestoreChunk(Vector2Int coord, BuildingCatalog catalog,
                                    List<BuildingSaveData> buildings,
                                    List<NodeEntry> nodes)
    {
        if (buildings == null) return;
        foreach (var save in buildings)
        {
            var prefab = catalog.Get(save.buildingId);
            if (prefab == null) continue;
            Object.Instantiate(prefab, save.position, save.rotation).Deserialize(save);
        }
        // TODO: nodes → ResourceNodeBehaviour 스폰
    }
}
