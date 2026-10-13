using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.VisualScripting;


public class NetworkCharacterPlayer : NetworkBehaviour
{
    [SerializeField] private InputActionReference moveActionAsset;
    [SerializeField] private float moveSpeed = 4f;

    private CharacterController controller;
    private InputAction moveInput;
    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.enabled = false;

        moveInput = moveActionAsset.action.Clone();
        
    }

    public override void OnNetworkSpawn()
    {
        if(!IsOwner)
        {
            return;
        }

        transform.position = new Vector3(NetworkManager.Singleton.IsHost ? -2f : 2f, 0f,0f);
        controller.enabled = true;
        moveInput.Enable();

    }
    public override void OnNetworkDespawn()
    {
        if(!IsOwner)
        {
            return;
        }

        controller.enabled = false;
        moveInput.Disable();
    }

    private void Update()
    {
        if(!IsOwner || !IsSpawned)
        {
            return;
        }

        Vector2 input = moveInput.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0f, input.y);
        controller.Move(move* moveSpeed * Time.deltaTime);
    }

    private void OnDestroy()
    {
        moveInput?.Disable();
    }

}
