using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ConnectUiController : MonoBehaviour
{
    // SerializeField 필드는 Inspector에서 Button과 TMP Text를 연결합니다.
    [SerializeField] private Button connectButton;
    [SerializeField] private TextMeshProUGUI statusText;

    public void StartHostFromButton()
    {
        // Forget은 Button 이벤트에서 기다리지 않는 UniTask의 예외를 UniTask 쪽으로 전달합니다.
        StartHostAsync().Forget();
    }

    // UniTask는 Unity PlayerLoop에 맞춘 비동기 반환 형식입니다.
    private async UniTask StartHostAsync()
    {
        connectButton.interactable = false;
        statusText.text = "Host 시작 중...";

        // StartHost는 NetworkManager의 Host 시작 성공 여부를 반환합니다.
        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            statusText.text = "Host 시작 실패";
            connectButton.interactable = true;
            return;
        }

        // Yield는 다음 PlayerLoop 시점까지 기다립니다.
        // GetCancellationTokenOnDestroy는 이 GameObject가 파괴되면 대기를 취소합니다.
        await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
        statusText.text = "Host 시작 완료";
        connectButton.interactable = true;
    }
}