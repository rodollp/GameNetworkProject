using UnityEngine;
using TMPro;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    private NetworkScorePlayer player;

    public void SetPlayer(NetworkScorePlayer newPlayer)
    {
        if (player != null)
        {
            player.Score.OnValueChanged -= UpdateScore;
        }

        player = newPlayer;

        player.Score.OnValueChanged += UpdateScore;

        scoreText.text = $"Score: {player.Score.Value}";
    }

    private void UpdateScore(int oldValue, int newValue)
    {
        scoreText.text = $"Score: {newValue}";
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.Score.OnValueChanged -= UpdateScore;
        }
    }
}