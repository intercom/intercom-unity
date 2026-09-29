using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LogicScript : MonoBehaviour
{
    public int playerScore;
    public TextMeshProUGUI scoreText;
    public GameObject gameOverScreen;
    public BirdScript bird;

    [ContextMenu("Increase Score")]
    public void AddScore(int scoreToAdd)
    {
        playerScore += scoreToAdd;
        scoreText.text = playerScore.ToString();
    }

    [ContextMenu("Restart Game")]
    public void RestartGame()
    {
        // Reset game state
        playerScore = 0;
        scoreText.text = "0";
        gameOverScreen.SetActive(false);

        // Reset bird position and state
        if (bird != null)
        {
            Debug.Log("Restarting game - Bird found");
            // Position the bird in the center of the screen
            bird.transform.position = new Vector3(0, 0, 0);
            Debug.Log($"Bird position set to: {bird.transform.position}");
            bird.myRigidbody.linearVelocity = Vector2.zero;
            bird.myRigidbody.angularVelocity = 0f;
            bird.birdIsAlive = true;

            // Ensure the bird's sprite renderer is enabled and visible
            SpriteRenderer spriteRenderer = bird.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                Debug.Log($"Sprite renderer found - Enabled: {spriteRenderer.enabled}, Color: {spriteRenderer.color}, Sprite: {spriteRenderer.sprite != null}");
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white;
                Debug.Log($"Sprite renderer after reset - Enabled: {spriteRenderer.enabled}, Color: {spriteRenderer.color}");
            }
            else
            {
                Debug.LogError("Sprite renderer not found on bird!");
            }
        }
        else
        {
            Debug.LogError("Bird reference is null!");
        }

        // Find and destroy all pipes
        PipeScript[] pipes = FindObjectsByType<PipeScript>(FindObjectsSortMode.None);
        foreach (PipeScript pipe in pipes)
        {
            Destroy(pipe.gameObject);
        }

        // Reset pipe spawner
        PipeSpawnerScript pipeSpawner = FindFirstObjectByType<PipeSpawnerScript>();
        if (pipeSpawner != null)
        {
            pipeSpawner.ResetSpawner();
        }
    }

    public void GameOver()
    {
        gameOverScreen.SetActive(true);
    }
}
