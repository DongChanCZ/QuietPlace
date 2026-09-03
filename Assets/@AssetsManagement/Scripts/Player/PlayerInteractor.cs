using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Lets the real player trigger LabBlockout's Interactable objects (doors,
/// flashlight/keycard pickups) with E, the same way BlockoutWalkPlayer does
/// for the blockout walk-test. Kept separate from PlayerController so the
/// interaction sweep doesn't pile onto an already-large script.
public sealed class PlayerInteractor : MonoBehaviour, IInteractor
{
    public float interactionDistance = 3f;

    Camera eyes;
    bool hasFlashlight;
    bool hasKeycard;

    public bool HasFlashlight => hasFlashlight;
    public bool HasKeycard => hasKeycard;

    public event Action<string> OnNotice;

    void Awake()
    {
        eyes = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            Interactable target = AimedInteractable();
            if (target != null) target.Interact(this);
        }
    }

    readonly RaycastHit[] reach = new RaycastHit[16];

    Interactable AimedInteractable()
    {
        if (eyes == null) return null;
        int count = Physics.SphereCastNonAlloc(eyes.transform.position, 0.08f,
            eyes.transform.forward, reach, interactionDistance, ~0, QueryTriggerInteraction.Collide);
        Interactable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var candidate = reach[i].collider.GetComponentInParent<Interactable>();
            if (candidate == null || reach[i].distance >= nearestDistance) continue;
            nearest = candidate;
            nearestDistance = reach[i].distance;
        }
        return nearest;
    }

    public void GiveFlashlight() => hasFlashlight = true;
    public void GiveKeycard() => hasKeycard = true;
    public void ShowNotice(string text) => OnNotice?.Invoke(text);
}
