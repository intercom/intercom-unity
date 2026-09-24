using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PipeSpawnerScript : MonoBehaviour
{
    public GameObject pipe;
    private float spawnRate;
    private float timer = 0;
    public float heightOffset = 10;
    private bool isSpawning = true;

    // Start is called before the first frame update
    void Start()
    {
        ResetSpawner();
    }

    public void ResetSpawner()
    {
        spawnRate = Random.Range(2f, 10f);
        timer = spawnRate;
        isSpawning = true;
    }

    public void StopSpawning()
    {
        isSpawning = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isSpawning) return;

        if (timer < spawnRate)
        {
            timer += Time.deltaTime;
        }
        else
        {
            float lowestPoint = transform.position.y - heightOffset;
            float highestPoint = transform.position.y + heightOffset;

            Instantiate(pipe, new Vector3(transform.position.x, Random.Range(lowestPoint, highestPoint), 0), transform.rotation);
            spawnRate = Random.Range(2f, 10f);
            timer = 0;
        }
    }
}
