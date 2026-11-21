using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 실제로 존재하는 청크 오브젝트
/// 높이맵 데이터를 받아 Mesh를 빌드함
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkInstance : MonoBehaviour
{
    public ChunkData Data { get; private set; }

    private MeshFilter _filter;
    private MeshCollider _collider;

    private void Awake()
    {
        _filter   = GetComponent<MeshFilter>();
        _collider = GetComponent<MeshCollider>();
    }

    /// <summary> ChunkData의 높이맵으로 Terrain Mesh를 생성 </summary>
    public void BuildMesh(ChunkData data)
    {
        Data = data;

        int size        = ChunkUtils.CHUNK_SIZE;
        var vertices    = new Vector3[size * size];
        var triangles   = new int[(size - 1) * (size - 1) * 6];
        var uvs         = new Vector2[size * size];

        // 버텍스 배치
        for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                int i       = z * size + x;
                vertices[i] = new Vector3(x, data.GetHeight(x, z), z);
                uvs[i]      = new Vector2((float)x / size, (float)z / size);
            }

        // 삼각형 인덱스 배치
        int t = 0;
        for (int z = 0; z < size - 1; z++)
            for (int x = 0; x < size - 1; x++)
            {
                int i = z * size + x;
                // 삼각형 1 (왼쪽 아래)
                triangles[t++] = i;
                triangles[t++] = i + size;
                triangles[t++] = i + 1;

                // 삼각형 2 (오른쪽 위)
                triangles[t++] = i + 1;
                triangles[t++] = i + size;
                triangles[t++] = i + size + 1;
            }

        var mesh = new Mesh { name = $"Chunk_{data.coord}" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        _filter.mesh = mesh;
        _collider.sharedMesh = mesh;
    }
}