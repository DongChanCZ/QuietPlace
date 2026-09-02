using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class GuardRoomExitTrigger : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        FacilityPlayerController player = other.GetComponentInParent<FacilityPlayerController>();
        if (player != null)
            player.EnterLobby();
    }
}
