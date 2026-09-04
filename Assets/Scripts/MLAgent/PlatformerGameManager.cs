using UnityEngine;
using UnityEngine.SceneManagement;

public class PlatformerGameManager : MonoBehaviour
{
    public static PlatformerGameManager Instance { get; private set; }

    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMPro.TMP_Text resultText;

    private bool levelOver = false;

    public bool LevelOver => levelOver;

    private void Awake()
    {
        Instance = this;

        if (endPanel != null)
        {
            endPanel.SetActive(false);
        }
    }

    public void Win()
    {
        if (levelOver) return;
        levelOver = true;

        Debug.Log("Level complete!");

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

    public void RestartLevel()
    {
        levelOver = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
