using UnityEngine;

public class LaneSpawnPoint : MonoBehaviour
{
    public Direction approach;

    [Header("Which intents may spawn here")]
    public bool allowStraight = true;
    public bool allowLeft = true;
    public bool allowRight = true;

    private Intent[] allowedIntents;

    private void Awake()
    {
        RebuildAllowedIntents();
    }

    private void OnValidate()
    {
        RebuildAllowedIntents();
    }

    private void RebuildAllowedIntents()
    {
        int count = (allowStraight ? 1 : 0) + (allowLeft ? 1 : 0) + (allowRight ? 1 : 0);
        if (count == 0)
        {
            allowedIntents = new[] { Intent.Straight };
            return;
        }

        allowedIntents = new Intent[count];
        int index = 0;
        if (allowStraight) allowedIntents[index++] = Intent.Straight;
        if (allowLeft) allowedIntents[index++] = Intent.Left;
        if (allowRight) allowedIntents[index] = Intent.Right;
    }

    public Intent PickIntent()
    {
        if (allowedIntents == null || allowedIntents.Length == 0)
            RebuildAllowedIntents();

        return allowedIntents[Random.Range(0, allowedIntents.Length)];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
    }
}