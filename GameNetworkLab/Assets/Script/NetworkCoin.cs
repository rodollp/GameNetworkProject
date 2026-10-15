using Unity.Netcode;
using UnityEngine;

public class NetworkCoin : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Coin 충돌 감지: {other.name}, IsServer: {IsServer}");

        if (!IsServer)
            return;

        NetworkScorePlayer player =
            other.GetComponentInParent<NetworkScorePlayer>();

        if (player == null)
        {
            Debug.LogWarning("NetworkScorePlayer를 찾지 못함");
            return;
        }

        player.Score.Value += 1;

        Debug.Log($"Coin 획득! Score: {player.Score.Value}");

        NetworkObject.Despawn();
    }
}