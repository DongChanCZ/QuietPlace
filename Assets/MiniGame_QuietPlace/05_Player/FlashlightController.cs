using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightController : MonoBehaviour
{
    public Light flashlight;
    public bool startOn = false;

    void Start()
    {
        if (flashlight != null) flashlight.enabled = startOn;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (flashlight != null) flashlight.enabled = !flashlight.enabled;
    }

    public void TurnOn()
    {
        if (flashlight != null) flashlight.enabled = true;
    }
}
