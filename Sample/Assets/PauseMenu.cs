using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI;

    void Start()
    {
        if (pauseMenuUI == null)
        {
            Debug.LogError("PauseMenu: pauseMenuUI is not assigned in the Inspector!");
        }
        else
        {
            pauseMenuUI.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Escape key pressed. GameIsPaused: " + GameIsPaused);
            TogglePause();
        }
    }

    public void TogglePause()
    {
        Debug.Log("TogglePause called. Current state: " + GameIsPaused);
        if (GameIsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Resume()
    {
        Debug.Log("Resuming game");
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    public void Pause()
    {
        Debug.Log("Pausing game");
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    public void LoadMainMenu()
    {
        Debug.Log("Loading main menu");
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartMenu");
    }

    public void OpenIntercomMessenger()
    {
        Debug.Log("Opening Intercom messenger");
        IntercomManager.Instance.ShowChat();
    }
}
