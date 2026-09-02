using UnityEngine;
public class BasementZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) LabEntranceGame.Instance.ReachBasement();
    }
}
