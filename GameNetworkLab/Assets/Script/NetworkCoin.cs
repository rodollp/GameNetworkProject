using UnityEngine;
using Unity.Netcode;

public class NetworkCoin : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        NetworkScorePlayer player =
            other.GetComponentInParent<NetworkScorePlayer>();

        if (player == null)
            return;

        player.Score.Value += 1;

        Debug.Log($"Coin È¹µæ! Player: {player.OwnerClientId}, " + $"Score: {player.Score.Value}");

        NetworkObject.Despawn();
    }
}