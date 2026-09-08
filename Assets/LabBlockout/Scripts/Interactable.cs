using UnityEngine;

/// Whatever can carry a flashlight/keycard and receive a notice - both
/// BlockoutWalkPlayer (blockout walk-test) and PlayerInteractor (real game
/// player) implement this, so Interactable doesn't care which one is holding it.
public interface IInteractor
{
    bool HasFlashlight { get; }
    void GiveFlashlight();
    bool HasKeycard { get; }
    void GiveKeycard();
    void ShowNotice(string text);
}

/// Anything the player can point at and press E on.
///
/// This exists because the interaction was welded to FlashlightPickup and
/// nothing else could attach to it. Three things need it immediately - the
/// torch, keycards and doors - so the abstraction is carrying real weight
/// rather than being built for a future that may not arrive.
public abstract class Interactable : MonoBehaviour
{
    /// What the prompt says while the player is looking at this. Return null to
    /// show no prompt at all.
    public abstract string Prompt(IInteractor player);

    public abstract void Interact(IInteractor player);

    /// 두 손으로 무거운 물건(변압기 모듈 등)을 들고 있을 때도 쓸 수 있는가.
    ///
    /// 기본은 false 다. 손이 차 있으면 물건을 새로 집을 수 없어야 한다.
    /// 설치 슬롯이나 문·레버처럼 손을 비우지 않고도 조작할 수 있는 것만 true 로 덮어쓴다.
    public virtual bool AllowedWhileHandsFull => false;
}
