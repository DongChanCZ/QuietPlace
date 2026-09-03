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
}
