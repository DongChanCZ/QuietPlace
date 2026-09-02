using UnityEngine;
public class Stage2Zone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) LabEntranceGame.Instance.ReachStage2();
    }
}
