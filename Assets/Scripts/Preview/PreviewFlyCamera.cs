using UnityEngine;

/// <summary>
/// 미리보기 씬용 자유 비행 카메라. 게임 플레이어(PlayerController·InputManager)와 무관하다.
/// 우클릭 드래그로 회전, WASD 이동, Q/E 아래/위, Shift 빠르게, 휠 앞뒤.
/// </summary>
public class PreviewFlyCamera : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _moveSpeed       = 80f;
    [SerializeField, Min(1f)] private float _fastMultiplier  = 4f;
    [SerializeField, Min(0f)] private float _lookSensitivity = 3f;
    [SerializeField, Min(0f)] private float _scrollSpeed     = 400f;

    private float _yaw, _pitch;

    private void Update()
    {
        // 다른 코드(TerrainPreview.FrameCamera)가 카메라를 옮겼을 수 있으므로 회전을 시작할 때 현재 각도를 읽는다
        if (Input.GetMouseButtonDown(1))
        {
            var e  = transform.eulerAngles;
            _yaw   = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
        }
        if (Input.GetMouseButton(1))
        {
            _yaw  += Input.GetAxis("Mouse X") * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * _lookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        var local = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) local += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) local += Vector3.back;
        if (Input.GetKey(KeyCode.D)) local += Vector3.right;
        if (Input.GetKey(KeyCode.A)) local += Vector3.left;

        var world = transform.rotation * local;
        if (Input.GetKey(KeyCode.E)) world += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) world += Vector3.down;

        float speed = _moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? _fastMultiplier : 1f);
        var   dt    = Time.unscaledDeltaTime;
        transform.position += world * (speed * dt)
                            + transform.forward * (Input.mouseScrollDelta.y * _scrollSpeed * dt);
    }
}
