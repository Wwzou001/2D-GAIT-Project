using UnityEngine;
using UnityEngine.SceneManagement;

public class PlatformerGameManager : MonoBehaviour
{
    public static PlatformerGameManager Instance { get; private set; }

    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMPro.TMP_Text resultText;
    [SerializeField] private TMPro.TMP_Text coinCounterText;

    // When true,Win()/Lose() skip the end of level UI and Time.timeScale pause entirely
    private bool trainingMode = false;

    private bool levelOver = false;
    private int coinsCollected = 0;

    public bool LevelOver => levelOver;
    public int CoinsCollected => coinsCollected;

    private void Awake()
    {
        Instance = this;

        if (endPanel != null)
        {
            endPanel.SetActive(false);
        }

        trainingMode = Object.FindAnyObjectByType<PlatformerAgent>() != null;
    }

    private void Start()
    {
        UpdateCoinCounter();
    }

    public void CollectCoin(int value)
    {
        if (levelOver) return;

        coinsCollected += value;
        UpdateCoinCounter();
    }

    private void UpdateCoinCounter()
    {
        if (coinCounterText != null)
        {
            coinCounterText.text = $"Coins: {coinsCollected}";
        }
    }

    public void Win()
    {
        if (levelOver) return;
        levelOver = true;

        Debug.Log("Level complete!");

        if (trainingMode)
        {
            ResetForNextEpisode();
            return;
        }

        if (endPanel != null)
        {
            endPanel.SetActive(true);
        }

        if (resultText != null)
        {
            resultText.text = "Level Complete!";
        }

        Time.timeScale = 0f;
    }

    public void Lose(string reason)
    {
        if (levelOver) return;
        levelOver = true;

        Debug.Log($"Level failed: {reason}");

        if (trainingMode)
        {
            ResetForNextEpisode();
            return;
        }

        if (endPanel != null)
        {
            endPanel.SetActive(true);
        }

        if (resultText !=null)
        {
            resultText.text = "Try Again";
        }

        Time.timeScale = 0f;
    }

    // Training mode equivalent of RestartLevel()
    private void ResetForNextEpisode()
    {
        levelOver = false;
        coinsCollected = 0;
        UpdateCoinCounter();
    }

    public void RestartLevel()
    {
        levelOver = false;
        coinsCollected = 0;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
