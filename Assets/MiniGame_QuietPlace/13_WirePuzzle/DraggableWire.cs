using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class DraggableWire : MonoBehaviour
{
    public Camera cam;
    public WireColor wireColor;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip connectSound;
    public AudioClip wrongSound;

    public UnityEvent onConnected;

    private Vector3 startPosition;
    private Vector3 dragOffset;
    private bool dragging;
    private bool connected;

    void Start()
    {
        startPosition = transform.position;
    }

    void OnMouseDown()
    {
        if (connected) return;
        dragging = true;
        dragOffset = transform.position - GetMouseWorldPos();
    }

    void Update()
    {
        if (!dragging) return;
        transform.position = GetMouseWorldPos() + dragOffset;
    }

    void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;

        WireTerminal nearestTerminal = FindNearestTerminal();

        if (nearestTerminal != null && nearestTerminal.terminalColor == wireColor && !nearestTerminal.IsConnected)
        {
            transform.position = nearestTerminal.transform.position;
            connected = true;
            nearestTerminal.MarkConnected();
            PlaySound(connectSound);
            onConnected?.Invoke();
        }
        else
        {
            transform.position = startPosition;
            PlaySound(wrongSound);
        }
    }

    WireTerminal FindNearestTerminal()
    {
        WireTerminal[] terminals = FindObjectsByType<WireTerminal>(FindObjectsSortMode.None);
        WireTerminal nearest = null;
        float nearestDist = float.MaxValue;

        foreach (WireTerminal t in terminals)
        {
            float dist = Vector3.Distance(transform.position, t.transform.position);
            if (dist <= t.snapRadius && dist < nearestDist)
            {
                nearest = t;
                nearestDist = dist;
            }
        }

        return nearest;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    Vector3 GetMouseWorldPos()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        float depth = cam.WorldToScreenPoint(transform.position).z;
        Vector3 screenPos = new Vector3(mouseScreen.x, mouseScreen.y, depth);
        return cam.ScreenToWorldPoint(screenPos);
    }
}
