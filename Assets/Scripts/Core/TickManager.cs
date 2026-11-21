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
        _timer += Time.deltaTime;
        if (_timer < _tickInterval) return;

        float delta = _timer;
        _timer = 0f;

        // 건물 없으면 타이머만 리셋하고 종료
        // 이전 코드는 Count==0일 때 return을 먼저 해서
        // 건물이 없는 동안 _timer가 계속 누적되는 버그가 있었음.
        // 건물이 다시 생겼을 때 첫 Tick에 누적된 시간이 한꺼번에 전달되는 문제.
        if (_buildings.Count == 0) return;

        // 뒤에서 앞으로 순회. 파괴된 건물(null)은 끝 원소를 그 자리로 옮겨 지운다.
        // 끝 원소는 이번 패스에서 이미 Tick을 받았으므로 다시 처리하지 않고 넘어간다.
        // (이전 코드는 i를 유지한 채 재검사해서, 끝 원소가 null이면 범위 밖 접근이,
        //  옮겨 온 원소는 같은 프레임에 두 번 Tick 받는 문제가 있었음)
        for (int i = _buildings.Count - 1; i >= 0; i--)
        {
            var building = _buildings[i];
            if (building == null) { RemoveAtSwapBack(i); continue; }

            // 건물 하나의 예외가 루프를 끊으면 그보다 앞 인덱스의 건물은 매 틱 Tick을 못 받는다.
            // 건물 단위로 격리하고, 문제 건물은 목록에서 빼서 같은 에러가 초당 10번씩 쌓이지 않게 한다.
            try
            {
                building.Tick(delta);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, building);   // 콘솔에서 클릭하면 해당 건물로 이동
                Debug.LogError($"[TickManager] {building.name} Tick 예외 — 이 건물의 Tick을 멈춥니다.", building);
                building.enabled = false;
                RemoveAtSwapBack(i);
            }
        }
    }

    // 끝 원소를 i 자리로 옮기고 끝을 지운다 — O(1).
    // 역순 순회이므로 옮겨 온 끝 원소는 이번 패스에서 이미 Tick을 받았다.
    private void RemoveAtSwapBack(int i)
    {
        int last = _buildings.Count - 1;
        _buildings[i] = _buildings[last];
        _buildings.RemoveAt(last);
    }

    public void Register(BuildingBase b)   => _buildings.Add(b);
    public void Unregister(BuildingBase b) => _buildings.Remove(b);
}
