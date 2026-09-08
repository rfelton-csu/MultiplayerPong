using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject leftPaddleSpawn;
    public GameObject rightPaddleSpawn;

    void Awake()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner needs a player prefab assigned.", this);
            return;
        }

        if (!playerPrefab.TryGetComponent(out TestSpawnPositionPlayer player))
        {
            Debug.LogError(
                "The player prefab needs a TestSpawnPositionPlayer component.",
                playerPrefab
            );
            return;
        }

        player.leftPaddleSpawn = leftPaddleSpawn;
        player.rightPaddleSpawn = rightPaddleSpawn;
    }

    // Update is called once per frame
    void Update() { }
}
