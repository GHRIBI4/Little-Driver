using System.Collections;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] private IntersectionManager intersection;
    [SerializeField] private TrafficCar carPrefab;
    [SerializeField] private LaneSpawnPoint[] spawnPoints;

    [Header("Spawning")]
    [SerializeField] private Vector2 spawnInterval = new Vector2(2f, 5f);
    [SerializeField] private float spawnClearRadius = 4f;
    [SerializeField] private int maxCars = 20;

    private readonly Collider[] overlapBuffer = new Collider[32];

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
        if (spawnPoints == null || spawnPoints.Length == 0 || intersection == null) return;
        if (intersection.CarCount >= maxCars) return;

        LaneSpawnPoint point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (IsBlocked(point.transform.position)) return;

        TrafficCar car = Instantiate(carPrefab, point.transform.position, point.transform.rotation);
        car.Init(intersection, point.approach, point.PickIntent());
    }

    private bool IsBlocked(Vector3 position)
    {
        int hits = Physics.OverlapSphereNonAlloc(position, spawnClearRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            if (overlapBuffer[i].GetComponentInParent<TrafficCar>() != null)
                return true;
        }
        return false;
    }
}