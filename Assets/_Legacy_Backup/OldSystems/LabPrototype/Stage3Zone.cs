using UnityEngine;
public class Stage3Zone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) LabEntranceGame.Instance.ReachStage3();
    }
}
