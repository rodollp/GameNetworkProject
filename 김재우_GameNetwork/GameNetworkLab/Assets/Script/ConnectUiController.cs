
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ConnectUiController : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private NetworkManager manager;

    private bool isConnecting;
    private bool isShuttingDown;
    private bool isHostMode;

    private void Start()
    {
        manager = NetworkManager.Singleton;

        if (manager == null)
        {
            statusText.text = "NetworkManager 없음";
            SetButtons(false, false, false);
            return;
        }

        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnected;

        RefreshUi();
    }

    private void OnDestroy()
    {
        if (manager == null)
            return;

        manager.OnClientConnectedCallback -= OnClientConnected;
        manager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Update()
    {
        if (manager == null)
            return;

        if (isShuttingDown && !manager.ShutdownInProgress)
        {
            isShuttingDown = false;
            isConnecting = false;

            RefreshUi();
        }
    }

    public void OnHostButtonClicked()
    {
        if (!CanStart())
            return;

        isConnecting = true;
        isHostMode = true;

        statusText.text = "Host 시작 중...";
        SetButtons(false, false, false);

        bool started = NetworkLancher.Instance.StartHost();

        if (!started)
        {
            isConnecting = false;
            statusText.text = "Host 시작 실패";
            RefreshUi();
            return;
        }

        // Host 시작 성공은 로컬 Host 실행 상태로 확인
        isConnecting = false;
        statusText.text = "Host 실행 중";
        RefreshUi();
    }

    public void OnClientButtonClicked()
    {
        if (!CanStart())
            return;

        isConnecting = true;
        isHostMode = false;

        statusText.text = "Client 접속 중...";
        SetButtons(false, false, false);

        bool started = NetworkLancher.Instance.StartClient();

        if (!started)
        {
            isConnecting = false;
            statusText.text = "Client 시작 실패";
            RefreshUi();
        }

        // 실제 접속 성공은 OnClientConnected에서 처리
    }

    public void OnDisconnectButtonClicked()
    {
        if (manager == null || !manager.IsClient || manager.IsHost)
            return;

        statusText.text = "연결 종료 중...";

        isConnecting = false;
        isShuttingDown = true;

        SetButtons(false, false, false);

        NetworkLancher.Instance.DisconnectClient();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (manager == null)
            return;

        if (clientId != manager.LocalClientId)
            return;

        isConnecting = false;

        statusText.text = manager.IsHost
            ? "Host 실행 중"
            : "Client 접속 완료";

        RefreshUi();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (manager == null)
            return;

        if (clientId != manager.LocalClientId)
            return;

        bool wasConnecting = isConnecting;

        isConnecting = false;
        isShuttingDown = true;

        statusText.text = wasConnecting
            ? "Client 접속 실패"
            : "Client 연결 종료";

        // 연결 실패나 서버 종료 후에는 네트워크 정리가 필요
        if (!manager.ShutdownInProgress)
            manager.Shutdown();

        SetButtons(false, false, false);
    }

    private bool CanStart()
    {
        return manager != null
            && NetworkLancher.Instance != null
            && !manager.IsListening
            && !manager.ShutdownInProgress
            && !isConnecting
            && !isShuttingDown;
    }

    private void RefreshUi()
    {
        if (manager == null)
            return;

        if (isConnecting || isShuttingDown || manager.ShutdownInProgress)
        {
            SetButtons(false, false, false);
            return;
        }

        if (manager.IsHost)
        {
            SetButtons(false, false, false);
        }
        else if (manager.IsConnectedClient)
        {
            SetButtons(false, false, true);
        }
        else
        {
            SetButtons(true, true, false);
        }
    }

    private void SetButtons(bool host, bool client, bool disconnect)
    {
        if (hostButton != null)
            hostButton.interactable = host;

        if (clientButton != null)
            clientButton.interactable = client;

        if (disconnectButton != null)
            disconnectButton.interactable = disconnect;
    }
}
