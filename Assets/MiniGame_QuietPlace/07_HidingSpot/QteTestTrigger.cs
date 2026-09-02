using UnityEngine;

public class QteTestTrigger : MonoBehaviour
{
    public HidingSpotQTE hidingSpot;
    public Transform player;
    public float triggerRange = 1.5f;

    private bool triggered;

    void Update()
    {
        if (triggered || player == null || hidingSpot == null) return;

        if (Vector3.Distance(transform.position, player.position) <= triggerRange)
        {
            triggered = true;
            hidingSpot.BeginQte();
        }
    }
}
