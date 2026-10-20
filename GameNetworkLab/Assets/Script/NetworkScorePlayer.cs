using UnityEngine;
using Unity.Netcode;

public class NetworkScorePlayer : NetworkBehaviour
{
    public NetworkVariable<int> Score = new(0);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        ScoreUI scoreUI = FindAnyObjectByType<ScoreUI>();

        if (scoreUI != null)
        {
            scoreUI.SetPlayer(this);
        }
    }

    public void RequestCollectCoin(ulong coinId)
    {
        if (!IsOwner || !IsSpawned)
            return;

        RequestCollectCoinRpc(coinId);
    }



    public void TestInvaildCoinRequest()
    {
        if(!IsOwner)
        {
            return;
        }

        RequestCollectCoinRpc(ulong.MaxValue);
    }

    [Rpc(SendTo.Server)]
    private void RequestCollectCoinRpc(ulong coinId)
    {
        if (!IsServer)
            return;

        // 1. 네트워크 오브젝트 ID 검색
        bool found = NetworkManager.SpawnManager.SpawnedObjects
            .TryGetValue(coinId, out NetworkObject coinObject);

        if (!found)
        {
            Debug.Log($"코인 획득 거절: 잘못된 ID {coinId}");
            return;
        }

        // 2. 실제 코인인지 확인
        if (!coinObject.TryGetComponent(
            out NetworkCoin coin))
        {
            Debug.Log("코인 획득 거절: 코인이 아닌 오브젝트");
            return;
        }

        // 3. 코인에게 획득 검증 요청
        coin.TryCollect(this);
    }
}