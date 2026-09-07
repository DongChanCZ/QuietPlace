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

    [Header("Battery")]
    public float batteryLevel = 100f;
    public float batteryDrainInterval = 3f; // 이 초마다 1씩 소모 (불 켜져 있을 때만)
    float batteryDrainTimer;

    Camera eyes;
    Light playerFlashlight;
    bool hasFlashlight;
    bool hasKeycard;

    public bool HasFlashlight => hasFlashlight;
    public bool FlashlightEnabled => playerFlashlight != null && playerFlashlight.enabled;
    public bool HasKeycard => hasKeycard;
    public float BatteryLevel => batteryLevel;

    public event Action<string> OnNotice;

    void Awake()
    {
        eyes = GetComponentInChildren<Camera>();
        CreatePlayerFlashlight();
    }

    void OnEnable()
    {
        if (QuickSlotManager.Instance != null)
        {
            QuickSlotManager.Instance.OnSlotsChanged += HandleQuickSlotChanged;
            QuickSlotManager.Instance.OnItemAcquired += HandleItemAcquired;
        }
    }

    void OnDisable()
    {
        if (QuickSlotManager.Instance != null)
        {
            QuickSlotManager.Instance.OnSlotsChanged -= HandleQuickSlotChanged;
            QuickSlotManager.Instance.OnItemAcquired -= HandleItemAcquired;
        }
    }

    // 퀵슬롯에서 손전등이 선택 해제되면(다른 슬롯 선택/빈 슬롯 선택) 켜져 있던 불도 꺼야 한다.
    void HandleQuickSlotChanged()
    {
        if (!IsFlashlightSelected && playerFlashlight != null && playerFlashlight.enabled)
            playerFlashlight.enabled = false;
    }

    void HandleItemAcquired(ItemType item, string displayName, int slotIndex)
    {
        int slotNumber = slotIndex + 1;
        string usageText = QuickSlotManager.IsUsable(item)
            ? slotNumber + "번을 눌러 선택 후 F를 눌러 사용 가능합니다."
            : slotNumber + "번을 눌러 선택할 수 있습니다.";
        ShowNotice(displayName + ObjectParticle(displayName) + " 습득하였습니다. " + usageText);
    }

    // 마지막 글자 받침 유무로 을/를 결정 (한글 음절 U+AC00~U+D7A3, 종성 인덱스 = (코드 - AC00) % 28, 0이면 받침 없음)
    static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "를";
        char last = word[word.Length - 1];
        if (last < '가' || last > '힣') return "를";
        return (last - '가') % 28 != 0 ? "을" : "를";
    }

    bool IsFlashlightSelected =>
        QuickSlotManager.Instance != null &&
        QuickSlotManager.Instance.TryGetSelectedItem(out ItemType item) &&
        item == ItemType.Flashlight;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.eKey.wasPressedThisFrame)
        {
            Interactable target = Aimed;
            if (target != null) target.Interact(this);
        }
        if (keyboard.fKey.wasPressedThisFrame) ToggleFlashlight();
        DrainBattery();
    }

    void DrainBattery()
    {
        if (playerFlashlight == null || !playerFlashlight.enabled) return;
        if (batteryLevel <= 0f) { playerFlashlight.enabled = false; return; }
        batteryDrainTimer += Time.deltaTime;
        if (batteryDrainTimer < batteryDrainInterval) return;
        batteryDrainTimer -= batteryDrainInterval;
        batteryLevel = Mathf.Max(0f, batteryLevel - 1f);
        if (batteryLevel <= 0f) playerFlashlight.enabled = false;
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
        if (!hasFlashlight || playerFlashlight == null || !IsFlashlightSelected || batteryLevel <= 0f) return;
        playerFlashlight.enabled = !playerFlashlight.enabled;
    }

    readonly RaycastHit[] reach = new RaycastHit[16];

    int aimedFrame = -1;
    Interactable aimed;

    /// 조준 중인 Interactable. 프레임당 한 번만 스윕한다 - Update(E키)와 프롬프트 UI가
    /// 같은 프레임에 둘 다 물어봐도 레이캐스트는 한 번만 돈다.
    public Interactable Aimed
    {
        get
        {
            if (aimedFrame != Time.frameCount)
            {
                aimedFrame = Time.frameCount;
                aimed = AimedInteractable();
            }
            return aimed;
        }
    }

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

    public void GiveFlashlight()
    {
        hasFlashlight = true;
        QuickSlotManager.Instance?.AddItem(ItemType.Flashlight, "손전등");
    }

    public void GiveKeycard() => hasKeycard = true;
    public void ShowNotice(string text) => OnNotice?.Invoke(text);
}
