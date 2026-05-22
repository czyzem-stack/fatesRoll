using UnityEngine;
using UnityEngine.AI;

public class POIManager : MonoBehaviour
{
    public GameObject[] poiPrefabs;
    public float spawnRadius = 25f;
    private GameObject currentPOI;

    void Start()
    {
        Invoke("SpawnNewPOI", 1.5f);
    }

    [ContextMenu("Resolve Current POI")]
    public void ResolveActivePOI()
    {
        var poi = GameObject.FindWithTag("POI");
        if (poi != null)
        {
            ResolvePOI(poi);
        }
    }

    public void ResolvePOI(GameObject poi)
    {
        if (poi != null)
        {
            Destroy(poi);
            SpawnNewPOI();
        }
    }

    public void ClearAllPOIs()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    public void SpawnNewPOI()
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero == null) return;

        Vector3 heroPos = hero.transform.position;
        bool found = false;

        float[] searchRadii = { spawnRadius, spawnRadius * 1.5f, 12f };

        foreach (float r in searchRadii)
        {
            for (int i = 0; i < 50; i++)
            {
                Vector3 randomPoint = heroPos + Random.insideUnitSphere * r;
                if (Vector3.Distance(heroPos, randomPoint) < 4f) continue;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, 15.0f, NavMesh.AllAreas))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(heroPos, hit.position, NavMesh.AllAreas, path))
                    {
                        if (path.status == NavMeshPathStatus.PathComplete)
                        {
                            currentPOI = Instantiate(poiPrefabs[0], hit.position, Quaternion.identity, transform);
                            currentPOI.tag = "POI";
                            Debug.Log($"POIManager: Target at {hit.position} (Dist: {Vector3.Distance(heroPos, hit.position):F1}m)");
                            found = true;
                            break;
                        }
                    }
                }
            }
            if (found) break;
        }

        if (!found)
        {
            Vector3 fallback = heroPos + hero.transform.forward * 8f;
            currentPOI = Instantiate(poiPrefabs[0], fallback, Quaternion.identity, transform);
            currentPOI.tag = "POI";
            Debug.LogWarning("POIManager: Spawning fallback near hero.");
        }
    }
}
