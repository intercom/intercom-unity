using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdScript : MonoBehaviour
{
    public float flapStrength;
    public Rigidbody2D myRigidbody;
    public LogicScript logic;
    public bool birdIsAlive = true;
    private Vector3 startPosition;

    // Start is called before the first frame update
    void Start()
    {
        logic = GameObject.FindGameObjectWithTag("Logic").GetComponent<LogicScript>();
        startPosition = transform.position;
        Debug.Log($"Bird initialized at position: {transform.position}");
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Debug.Log($"Bird sprite renderer - Enabled: {spriteRenderer.enabled}, Color: {spriteRenderer.color}, Sprite: {spriteRenderer.sprite != null}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Debug log if bird is not visible
        if (birdIsAlive)
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && !spriteRenderer.isVisible)
            {
                Debug.LogWarning($"Bird is alive but not visible! Position: {transform.position}, Enabled: {spriteRenderer.enabled}, Color: {spriteRenderer.color}");
            }
        }

        // --- Desktop Input (for testing in editor) ---
        if (Input.GetKeyDown(KeyCode.Space) && birdIsAlive)
        {
            Flap();
        }

        // --- Mobile Input ---
        if (Input.touchCount > 0 && birdIsAlive)
        {
            Touch touch = Input.GetTouch(0); // Get the first touch

            // Check if the touch has just begun (Tap)
            if (touch.phase == TouchPhase.Began)
            {
                Flap();
            }
        }

        // Check if bird hits top or bottom of screen
        if (birdIsAlive)
        {
            float topBoundary = Camera.main.orthographicSize;
            float bottomBoundary = -Camera.main.orthographicSize;

            if (transform.position.y >= topBoundary || transform.position.y <= bottomBoundary)
            {
                birdIsAlive = false;
                logic.GameOver();
            }
        }
    }

    private void Flap()
    {
        myRigidbody.linearVelocity = Vector2.up * flapStrength;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        birdIsAlive = false;
        logic.GameOver();
    }
}
