using Unity.Netcode;
using UnityEngine;

public class NetworkScorePlayer : NetworkBehaviour
{
    public NetworkVariable<int> Score = new(0);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        // 자기 Player의 점수를 UI에 연결
        ScoreUI scoreUI = FindAnyObjectByType<ScoreUI>();

        if (scoreUI != null)
        {
            scoreUI.SetPlayer(this);
        }

        // 자기 Player의 점수 요청 메서드를 버튼에 등록
        NetworkLancher.Instance.AddScore.onClick.AddListener(RequestAddScore);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        // Player가 사라지면 버튼 이벤트 연결 해제
        if (NetworkLancher.Instance != null)
        {
            NetworkLancher.Instance.AddScore.onClick.RemoveListener(RequestAddScore);
        }
    }

    public void RequestAddScore()
    {
        if (!IsOwner)
            return;

        RequestAddScoreRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestAddScoreRpc()
    {
        Score.Value += 1;

       Debug.Log($"Player {OwnerClientId} Score: {Score.Value}");
       
    }
}