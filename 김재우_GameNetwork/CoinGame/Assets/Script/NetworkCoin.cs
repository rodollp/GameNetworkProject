using UnityEngine;
using Unity.Netcode;

public class NetworkCoin : NetworkBehaviour
{
    [SerializeField] private float collectDistance = 2f;

    private bool isCollected = false;

    private void OnTriggerEnter(Collider other)
    {
        NetworkScorePlayer player = other.GetComponentInParent<NetworkScorePlayer>();

        if (player == null || !player.IsOwner)
            return;

        // 본인 플레이어만 획득 요청
        player.RequestCollectCoin(NetworkObjectId);
    }

    // 서버에서만 호출하는 획득 판정
    public bool TryCollect(NetworkScorePlayer player)
    {
        if (!IsServer || !IsSpawned || isCollected)
            return false;

        if (player == null || !player.IsSpawned)
            return false;

        // 서버 기준 거리 계산
        float distance = Vector3.Distance(player.transform.position,transform.position);

        if (distance > collectDistance)
        {
            Debug.Log($"코인 획득 거절: 거리 초과 ({distance:F2})");
            return false;
        }

        // 중복 획득 방지
        isCollected = true;

        // 서버에서 점수 확정
        player.Score.Value += 1;

        Debug.Log(
            $"Coin 획득! Player: {player.OwnerClientId}, " +
            $"Score: {player.Score.Value}"
        );

        // 모든 클라이언트에 코인 제거 동기화
        NetworkObject.Despawn();

        return true;
    }
}