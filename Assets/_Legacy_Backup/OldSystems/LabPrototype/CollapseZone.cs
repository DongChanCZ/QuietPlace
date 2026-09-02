using UnityEngine;
public class CollapseZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) LabEntranceGame.Instance.TriggerCollapse();
    }
}
