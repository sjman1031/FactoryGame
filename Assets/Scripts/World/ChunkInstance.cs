using System.Collections.Generic;
using UnityEngine;

/// <summary>청크 하나의 지형 메시와 콜라이더. 풀에서 재사용된다.</summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkInstance : MonoBehaviour
{
    public ChunkData Data { get; private set; }
    private MeshFilter _filter; private MeshCollider _collider;

    // 청크 오브젝트는 풀에서 재사용되므로 메시도 하나만 만들어 계속 다시 채운다.
    // (이전에는 BuildMesh마다 new Mesh를 만들고 이전 것을 지우지 않아 재사용할수록 메시가 쌓였음)
    private Mesh _mesh;

    private void Awake()
    {
        _filter   = GetComponent<MeshFilter>();
        _collider = GetComponent<MeshCollider>();
        _mesh     = new Mesh();
        _filter.sharedMesh = _mesh;
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
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

        _mesh.Clear();
        _mesh.name = $"Chunk_{data.coord}";
        _mesh.vertices = verts; _mesh.triangles = tris; _mesh.uv = uvs;
        _mesh.RecalculateNormals();

        // MeshCollider는 같은 메시를 다시 넣어야 바뀐 형태로 다시 굽는다
        _collider.sharedMesh = null;
        _collider.sharedMesh = _mesh;
    }
}
