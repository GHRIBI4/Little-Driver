using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put one on each lane where cars should appear (on the NavMesh, blue arrow pointing toward the intersection).
/// Tip: make a lane that only allows Left so left-turners queue in their own lane
/// and do not block the cars going straight.
/// </summary>
public class LaneSpawnPoint : MonoBehaviour
{
    public Direction approach;

    [Header("Which intents may spawn here")]
    public bool allowStraight = true;
    public bool allowLeft = true;
    public bool allowRight = true;

    public Intent PickIntent()
    {
        List<Intent> options = new List<Intent>();
        if (allowStraight) options.Add(Intent.Straight);
        if (allowLeft)     options.Add(Intent.Left);
        if (allowRight)    options.Add(Intent.Right);

        if (options.Count == 0) return Intent.Straight;
        return options[Random.Range(0, options.Count)];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
    }
}
