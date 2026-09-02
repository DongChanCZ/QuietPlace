using UnityEngine;

public class BreakableObstacle : MonoBehaviour
{
    public string monsterTag = "Monster";
    public GameObject glassShardPrefab;
    public Vector3 shardSpawnOffset = new Vector3(1f, 0f, 0f);
    public float shardGroundY = 0f;

    private bool isBroken = false;

    void OnCollisionEnter(Collision collision)
    {
        TryBreak(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        TryBreak(other.gameObject);
    }

    void TryBreak(GameObject other)
    {
        if (isBroken) return;
        if (!other.CompareTag(monsterTag)) return;

        Break();
    }

    void Break()
    {
        isBroken = true;

        if (glassShardPrefab != null)
        {
            Vector3 spawnPos = transform.position + shardSpawnOffset;
            spawnPos.y = shardGroundY;
            Instantiate(glassShardPrefab, spawnPos, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
