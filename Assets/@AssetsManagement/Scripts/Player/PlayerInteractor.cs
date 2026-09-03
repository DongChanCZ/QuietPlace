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
    Light playerFlashlight;
    bool hasFlashlight;
    bool hasKeycard;

    public bool HasFlashlight => hasFlashlight;
    public bool FlashlightEnabled => playerFlashlight != null && playerFlashlight.enabled;
    public bool HasKeycard => hasKeycard;

    public event Action<string> OnNotice;

    void Awake()
    {
        eyes = GetComponentInChildren<Camera>();
        CreatePlayerFlashlight();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.eKey.wasPressedThisFrame)
        {
            Interactable target = AimedInteractable();
            if (target != null) target.Interact(this);
        }
        if (keyboard.fKey.wasPressedThisFrame) ToggleFlashlight();
    }

    // BlockoutWalkPlayer와 동일한 값 - 블록아웃 인스턴스의 (1.5, 1.3, 1.5) 스케일 배율 기준으로 맞춰진 값.
    void CreatePlayerFlashlight()
    {
        if (eyes == null) return;
        GameObject lightObject = new GameObject("PLAYER_FLASHLIGHT");
        lightObject.transform.SetParent(eyes.transform, false);
        lightObject.transform.localPosition = new Vector3(0.16f, -0.12f, 0.08f);
        lightObject.transform.localRotation = Quaternion.identity;
        playerFlashlight = lightObject.AddComponent<Light>();
        playerFlashlight.type = LightType.Spot;
        playerFlashlight.color = new Color(0.78f, 0.88f, 1f);
        playerFlashlight.intensity = 25f;
        playerFlashlight.range = 26f;
        playerFlashlight.spotAngle = 48f;
        playerFlashlight.innerSpotAngle = 28f;
        playerFlashlight.shadows = LightShadows.Soft;
        playerFlashlight.shadowStrength = 0.85f;
        playerFlashlight.enabled = false;
    }

    public void ToggleFlashlight()
    {
        if (!hasFlashlight || playerFlashlight == null) return;
        playerFlashlight.enabled = !playerFlashlight.enabled;
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
