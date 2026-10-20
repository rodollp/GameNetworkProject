using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkLancher : MonoBehaviour
{
    public static NetworkLancher Instance { get; private set; }

    [SerializeField] private Button disconnect;

    public Button DisconnectButton => disconnect;

    private void Awake()
    {
        Instance = this;
    }

    public void StartHost()
    {
        if (NetworkManager.Singleton.IsListening)
            return;

        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton.IsListening)
            return;

        NetworkManager.Singleton.StartClient();
    }

    public void DisconnectClient()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.IsHost)
            return;

        NetworkManager.Singleton.Shutdown();
    }
}