using UnityEngine;

/// <summary>
/// 문 옆 키패드. E 를 누르면 주파수 맞추기(해킹) 미니게임이 뜨고, 통과하면 그 문이 열린다.
///
/// 문은 Access.Sealed 로 잠가두면 손으로는 절대 열리지 않는다. 해킹을 통과하면
/// SlidingDoor.ForceOpen 이 잠금을 풀고 문을 스르륵 열어준다.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeypadTerminal : Interactable
{
    [SerializeField] SlidingDoor door;
    [SerializeField] string displayName = "키패드";

    [Header("한 번 열면 다시 해킹하지 않는다")]
    [SerializeField] bool oneShot = true;

    [Header("사운드")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip unlockSound;

    public bool Unlocked { get; private set; }

    void Awake()
    {
        Debug.Assert(door, "KeypadTerminal is missing the door it unlocks.");
    }

    public override string Prompt(IInteractor player)
    {
        if (Unlocked && oneShot) return null;
        return "E  " + displayName + " 해킹";
    }

    public override void Interact(IInteractor player)
    {
        if (Unlocked && oneShot) return;

        if (FrequencyHackManager.Instance == null)
        {
            // 매니저가 없는 씬(프리뷰 등)에서는 그냥 열어준다. 진행이 막히지 않게.
            Debug.LogWarning($"[{nameof(KeypadTerminal)}] FrequencyHackManager 가 없어 미니게임 없이 연다.", this);
            OnHacked();
            return;
        }

        FrequencyHackManager.Instance.Begin(this, player);
    }

    /// <summary>미니게임을 통과했을 때 매니저가 부른다.</summary>
    public void OnHacked()
    {
        if (Unlocked && oneShot) return;

        Unlocked = true;

        if (door != null) door.ForceOpen();
        if (audioSource != null && unlockSound != null) audioSource.PlayOneShot(unlockSound);
    }
}
