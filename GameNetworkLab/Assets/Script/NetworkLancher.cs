using Unity.Netcode;
using UnityEngine;

public class NetworkLancher : MonoBehaviour
{
   public void StartHost()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            return;
        }

        NetworkManager.Singleton.StartHost();
    }


    public void StartClient()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            return;
        }

        NetworkManager.Singleton.StartClient();
    }
}
