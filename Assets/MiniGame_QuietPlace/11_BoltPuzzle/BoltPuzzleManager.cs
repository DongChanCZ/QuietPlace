using UnityEngine;
using UnityEngine.Events;

public class BoltPuzzleManager : MonoBehaviour
{
    public BoltUnscrew[] bolts;
    public UnityEvent onAllBoltsRemoved;

    private int removedCount;

    void Start()
    {
        foreach (BoltUnscrew bolt in bolts)
        {
            if (bolt != null) bolt.onRemoved.AddListener(OnBoltRemoved);
        }
    }

    void OnBoltRemoved()
    {
        removedCount++;
        if (removedCount >= bolts.Length)
        {
            onAllBoltsRemoved?.Invoke();
        }
    }
}
