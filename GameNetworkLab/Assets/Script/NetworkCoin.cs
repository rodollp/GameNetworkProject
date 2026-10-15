using Unity.Netcode;
using UnityEngine;

public class NetworkCoin : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // IsServer로 검사해 Coin 제거와 점수 변경은 Server에서만 실행합니다.
        if (!IsServer)
            return;

        // GetComponent는 충돌한 오브젝트에서 점수 관리 컴포넌트를 찾습니다.
        NetworkScorePlayer player = other.GetComponent<NetworkScorePlayer>();
        if (player == null)
            return;

        // Server가 공유 점수를 변경하면 NetworkVariable이 모든 Client에 반영합니다.
        player.Score.Value += 1;
        // Despawn은 Coin NetworkObject를 모든 참여자에게서 제거합니다.
        NetworkObject.Despawn();
    }
}