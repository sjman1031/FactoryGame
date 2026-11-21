using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 BuildingBase의 Tick()을 일정 주기로 일괄 호출
/// 건물마다 Update를 쓰지 않아 성능 절약 가능
/// </summary>
public class TickManager : SingletonManager<TickManager>
{
    [SerializeField] private float _tickInterval = 0.1f; // 초당 10회

    private readonly List<BuildingBase> _buildings = new();
    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;
        if(_timer < _tickInterval) return;

        float delta = _timer;
        _timer = 0f;

        // 건물이 없으면 타이머만 리셋하고 종료
        if (_buildings.Count == 0) return;

        int i = _buildings.Count - 1;
        while (i >= 0)
        {
            if (_buildings[i] == null)
            {
                int last = _buildings.Count - 1;
                if (i < last)
                    _buildings[i] = _buildings[last];
                _buildings.RemoveAt(last);
            }
            else
            {
                _buildings[i].Tick(delta);
                i--;
            }
        }
    }

    public void Register(BuildingBase b) => _buildings.Add(b);
    public void UnRegister(BuildingBase b) => _buildings.Remove(b);

}