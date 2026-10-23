
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

// NetworkBehaviour는 MonoBehaviour를 상속한 네트워크 전용 클래스다.
// MonoBehaviour의 Start(), Update(), Awake() 등을 사용할 수 있으며,
// 추가로 네트워크 관련 기능(IsOwner, IsSpawned 등)을 사용할 수 있다.
public class NetworkCharacterPlayer : NetworkBehaviour
{
    // Inspector에서 이동에 사용할 InputAction을 연결한다.
    // 예: MoveActionAsset 안에 있는 Player/Move
    [SerializeField] private InputActionReference moveActionAsset;

    // 캐릭터 이동 속도
    [SerializeField] private float moveSpeed = 4f;

    // 캐릭터를 실제로 움직이게 하는 컴포넌트
    private CharacterController controller;

    // WASD 입력값을 읽기 위해 사용할 InputAction
    private InputAction moveInput;

    private void Awake()
    {
        // 현재 GameObject에 붙어 있는 CharacterController를 가져온다.
        controller = GetComponent<CharacterController>();

        // 아직 이 캐릭터의 소유자가 누구인지 처리하기 전이므로
        // CharacterController를 비활성화한다.
        controller.enabled = false;

        // Inspector에 연결된 Move Action을 복제한다.
        // 여러 플레이어 객체가 하나의 InputAction을 공유하면
        // 다른 플레이어가 Disable()할 때 내 입력도 꺼질 수 있다.
        // 따라서 각 플레이어가 독립적인 InputAction을 사용하게 한다.
        moveInput = moveActionAsset.action.Clone();
    }

    // NetworkObject가 네트워크에 Spawn되었을 때 호출된다.
    // 일반적인 Start()와 달리 네트워크 생성 과정에 연결된 메서드다.
    // 이 시점에는 IsOwner, OwnerClientId 같은 네트워크 정보를 사용할 수 있다.
    public override void OnNetworkSpawn()
    {
        // IsOwner는 현재 실행 중인 클라이언트가
        // 이 캐릭터의 소유자인지 확인한다.
        //
        // Host 화면:
        // Host 캐릭터 -> true
        // Client 캐릭터 -> false
        //
        // Client 화면:
        // Host 캐릭터 -> false
        // Client 캐릭터 -> true

        // 내 캐릭터가 아니라면 입력과 이동을 활성화하지 않는다.
        if (!IsOwner)
        {
            return;
        }

        // Host와 Client의 시작 위치를 다르게 설정한다.
        //
        // IsHost가 true라면 Host이므로 X = -2
        // IsHost가 false라면 Client이므로 X = 2
        //
        // ?: 는 삼항 연산자다.
        // 조건 ? 참일 때 값 : 거짓일 때 값
        transform.position = new Vector3(NetworkManager.Singleton.IsHost ? -2f : 2f,0f,0f);

        // 소유자인 경우 CharacterController 활성화
        controller.enabled = true;

        // WASD 입력을 받을 수 있도록 InputAction 활성화
        moveInput.Enable();
    }

    // NetworkObject가 네트워크에서 Spawn 해제될 때 호출된다.
    // 예: NetworkObject.Despawn() 실행
    // 단순히 다른 플레이어가 접속을 끊었다고
    // 모든 플레이어 객체에서 호출되는 것은 아니다.
    public override void OnNetworkDespawn()
    {
        // 내 캐릭터가 아니라면 처리할 필요가 없다.
        if (!IsOwner)
        {
            return;
        }

        // 네트워크에서 Spawn 해제되었으므로
        // 더 이상 캐릭터를 움직이지 못하도록 한다.
        controller.enabled = false;

        // WASD 입력을 비활성화한다.
        moveInput.Disable();
    }

    private void Update()
    {
        // IsOwner:
        // 현재 클라이언트가 이 캐릭터의 소유자인가?
        //
        // IsSpawned:
        // 이 NetworkObject가 네트워크에 Spawn된 상태인가?
        //
        // 둘 중 하나라도 조건을 만족하지 않으면 이동하지 않는다.
        if (!IsOwner || !IsSpawned)
        {
            return;
        }

        // WASD 입력을 Vector2 값으로 읽는다.
        // W = (0, 1), S = (0, -1)
        // A = (-1, 0), D = (1, 0)
        Vector2 input = moveInput.ReadValue<Vector2>();

        // 2D 입력값을 Unity의 3D 이동 방향으로 변환한다.
        // input.x -> X축 이동
        // input.y -> Z축 이동
        // Y축은 0이므로 위아래로 움직이지 않는다.
        Vector3 move = new Vector3(input.x, 0f, input.y);

        // CharacterController를 사용해 캐릭터를 이동시킨다.
        // moveSpeed는 이동 속도,
        // Time.deltaTime은 프레임 시간 차이를 보정한다.
        controller.Move(move * moveSpeed * Time.deltaTime);
    }

    // Unity에서 이 컴포넌트가 파괴될 때 호출된다.
    public override void OnDestroy()
    {
        // Clone()으로 복제한 InputAction의 리소스를 정리한다.
        // ?. 는 moveInput이 null이 아닐 때만 실행한다는 뜻이다.
        moveInput?.Dispose();
        base.OnDestroy();
    }


    [Rpc(SendTo.Server)]

    private void MoveRequestRPC()
    {

    }


}
