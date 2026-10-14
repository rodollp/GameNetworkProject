using UnityEngine;
using Unity.Netcode;
public class NetworkScorePlayer : NetworkBehaviour
{

    public NetworkVariable<int> Score = new(0);

    public void RequestAddScore()
    {
        if(!IsOwner)
        {
            return;
        }

        RequestAddScoreRpc();
    }
       private void RequestAddScoreRpc()
    {
        Score.Value += 1;
        Debug.Log($"Sever가 점수를 {Score.Value}로 변경하였습니다");
    }
}
