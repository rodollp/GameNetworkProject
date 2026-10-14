using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkLancher : MonoBehaviour
{
    public static NetworkLancher Instance { get; private set; }

    [SerializeField] private Button addScore;

    public Button AddScore => addScore;

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
}