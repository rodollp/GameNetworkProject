using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class NetworkPlayerMove : NetworkBehaviour
{
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float speed = 4f;

    private Vector2 moveInput;
    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // 서버에서 모든 플레이어의 충돌 처리 활성화
        if (IsServer && controller != null)
            controller.enabled = true;

        // 자기 캐릭터만 입력 활성화
        if (IsOwner)
            moveAction.action.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            moveAction.action.Disable();
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
            return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        MoveRequestRpc(input);
    }

    [Rpc(SendTo.Server)]
    private void MoveRequestRpc(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned)
            return;

        Vector3 movement = new Vector3(
            moveInput.x,
            0f,
            moveInput.y
        ) * speed * Time.fixedDeltaTime;

        if (controller != null && controller.enabled)
        {
            controller.Move(movement);
        }
    }
}