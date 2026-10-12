using System.Collections;
using UnityEngine;

/// <summary>Spawns cars at random lane spawn points. Cars destroy themselves at the end of the road.</summary>
public class CarSpawner : MonoBehaviour
{
    [SerializeField] private IntersectionManager intersection;
    [SerializeField] private TrafficCar carPrefab;
    [SerializeField] private LaneSpawnPoint[] spawnPoints;

    [Header("Spawning")]
    [SerializeField] private Vector2 spawnInterval = new Vector2(2f, 5f); // min / max seconds
    [SerializeField] private float spawnClearRadius = 4f;                 // skip if a car is still here
    [SerializeField] private int maxCars = 20;

    private IEnumerator Start()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnInterval.x, spawnInterval.y));
            TrySpawn();
        }
    }

    private void TrySpawn()
    {
        if (spawnPoints.Length == 0) return;
        if (intersection.CarCount >= maxCars) return;

        LaneSpawnPoint point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (IsBlocked(point.transform.position)) return;

        TrafficCar car = Instantiate(carPrefab, point.transform.position, point.transform.rotation);
        car.Init(intersection, point.approach, point.PickIntent());
    }

    private bool IsBlocked(Vector3 position)
    {
        foreach (Collider c in Physics.OverlapSphere(position, spawnClearRadius))
        {
            if (c.GetComponentInParent<TrafficCar>() != null) return true;
        }
        return false;
    }
}
