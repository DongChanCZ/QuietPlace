using UnityEngine;

public class WireTerminal : MonoBehaviour
{
    public WireColor terminalColor;
    public float snapRadius = 0.3f;

    public bool IsConnected { get; private set; }

    public void MarkConnected()
    {
        IsConnected = true;
    }
}
