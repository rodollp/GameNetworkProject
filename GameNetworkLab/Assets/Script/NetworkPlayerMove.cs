using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerMove : NetworkBehaviour
{
    // InputActionReference는 Inspector에서 만든 Move Input Action을 연결합니다.
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float speed = 4f;

    // OnNetworkSpawn은 이 NetworkObject가 네트워크에 생성된 직후 한 번 호출됩니다.
    public override void OnNetworkSpawn()
    {
        // IsOwner인 Player만 자신의 키보드 입력 Action을 켭니다.
        if (IsOwner)
            moveAction.action.Enable();
    }

    public override void OnNetworkDespawn()
    {
        // NetworkObject가 제거될 때 입력 Action도 꺼서 남은 입력을 막습니다.
        if (IsOwner)
            moveAction.action.Disable();
    }

    private void Update()
    {
        // 다른 Client의 Player에는 로컬 입력을 적용하지 않습니다.
        if (!IsOwner)
            return;

        // ReadValue<Vector2>는 Move Action의 가로·세로 입력값을 읽습니다.
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        if (input.sqrMagnitude > 0f)
            MoveRequestRpc(input);
    }

    [Rpc(SendTo.Server)]
    private void MoveRequestRpc(Vector2 input)
    {
        // Server가 받은 입력으로 위치를 바꾸면 NetworkTransform이 결과를 동기화합니다.
        Vector3 movement =new Vector3(input.x, 0f, input.y) * speed * Time.deltaTime;

        transform.position += movement;
        transform.position += movement;
    }
}