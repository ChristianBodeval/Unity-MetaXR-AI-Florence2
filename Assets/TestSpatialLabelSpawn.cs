using UnityEngine;

public class TestSpatialLabelSpawn : MonoBehaviour
{
    public GameObject prefabToSpawn;
    public Vector3 spawnPosition;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Instantiate(prefabToSpawn, transform.position, Quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(spawnPosition, 0.1f);
    }
}
