using UnityEngine;
using UnityEngine.Events;

public class WirePuzzleManager : MonoBehaviour
{
    public DraggableWire[] wires;
    public UnityEvent onAllWiresConnected;

    private int connectedCount;

    void Start()
    {
        foreach (DraggableWire wire in wires)
        {
            if (wire != null) wire.onConnected.AddListener(OnWireConnected);
        }
    }

    void OnWireConnected()
    {
        connectedCount++;
        if (connectedCount >= wires.Length)
        {
            onAllWiresConnected?.Invoke();
        }
    }
}
