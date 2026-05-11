using UnityEngine;
using Unity.Netcode;

public class NPCSpawner : NetworkBehaviour
{
    [Header("Spawning")]
    public GameObject npcPrefab;
    public float spawnInterval = 2f;
    public int maxNPCsActive = 10;
    
    [Header("Spawn Location")]
    public float boundaryRadius = 5f;
    public float spawnDistanceFromBoundary = 1f;
    
    private float spawnTimer = 0f;
    private int currentNPCCount = 0;

    void Start()
    {
        if (npcPrefab == null)
        {
            Debug.LogError("NPCSpawner: NPC Prefab not assigned!");
        }
    }

    void Update()
    {
        if (!IsServer) return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f && currentNPCCount < maxNPCsActive)
        {
            SpawnNPC();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnNPC()
    {
        if (npcPrefab == null) return;

        // Find player to spawn around them
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null) return;

        // Spawn randomly outside the boundary circle
        float spawnRadius = boundaryRadius + spawnDistanceFromBoundary;
        float randomAngle = Random.Range(0f, Mathf.PI * 2f);
        
        Vector2 spawnPosition = new Vector2(
            Mathf.Cos(randomAngle),
            Mathf.Sin(randomAngle)
        ) * spawnRadius;

        // Add player position offset
        spawnPosition += (Vector2)player.transform.position;

        GameObject npcObj = Instantiate(
            npcPrefab,
            spawnPosition,
            Quaternion.identity
        );

        NetworkObject npcNetObj = npcObj.GetComponent<NetworkObject>();
        if (npcNetObj != null)
        {
            npcNetObj.Spawn();
            currentNPCCount++;
            
            // Track when NPC is destroyed
            NPC npc = npcObj.GetComponent<NPC>();
            if (npc != null)
            {
                StartCoroutine(TrackNPCDespawn(npcNetObj));
            }
        }
    }

    private System.Collections.IEnumerator TrackNPCDespawn(NetworkObject npcNetObj)
    {
        while (npcNetObj != null && npcNetObj.gameObject != null)
        {
            yield return new WaitForSeconds(0.5f);
        }

        currentNPCCount--;
    }

    public void Reset()
    {
        currentNPCCount = 0;
        spawnTimer = spawnInterval;
    }
}
