using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 BuildingBase의 Tick()을 일정 주기로 일괄 호출.
/// 건물마다 Update를 쓰지 않아 성능을 절약한다.
/// </summary>
public class TickManager : SingletonManager<TickManager>
{
    [SerializeField] private float _tickInterval = 0.1f;

    private readonly List<BuildingBase> _buildings = new();
    private float _timer;

    private void Update()
    {
        // [최적화] 등록된 건물 없으면 타이머 연산 스킵
        if (_buildings.Count == 0) return;

        _timer += Time.deltaTime;
        if (_timer < _tickInterval) return;

        float delta = _timer;
        _timer = 0f;

        // [최적화] RemoveAt(i)는 뒤 요소를 전부 한 칸씩 당기는 O(n) 연산.
        // 순서가 중요하지 않은 리스트에서는 마지막 요소와 교체 후
        // RemoveAt(마지막)으로 O(1)에 처리하는 swap-back 패턴을 사용.
        int i = _buildings.Count - 1;
        while (i >= 0)
        {
            if (_buildings[i] == null)
            {
                int last = _buildings.Count - 1;
                // i가 마지막 요소면 그냥 제거 (자기 자신과 교체 방지)
                if (i < last)
                    _buildings[i] = _buildings[last];
                _buildings.RemoveAt(last);
                // swap-back 후 i는 그대로 — 방금 올라온 요소를 다음 루프에서 검사
            }
            else
            {
                _buildings[i].Tick(delta);
                i--;
            }
        }
    }

    public void Register(BuildingBase b)   => _buildings.Add(b);
    public void Unregister(BuildingBase b) => _buildings.Remove(b);
}
