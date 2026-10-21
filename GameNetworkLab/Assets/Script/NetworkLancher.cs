using Unity.Netcode;
using UnityEngine;

public class NetworkLancher : MonoBehaviour
{
    public static NetworkLancher Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public bool StartHost()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || manager.IsListening ||
            manager.ShutdownInProgress)
            return false;

        return manager.StartHost();
    }

    public bool StartClient()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || manager.IsListening ||
            manager.ShutdownInProgress)
            return false;

        return manager.StartClient();
    }

    public void DisconnectClient()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        if (!manager.IsClient || manager.IsHost)
            return;

        manager.Shutdown();
    }
}