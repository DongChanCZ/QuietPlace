using UnityEngine;

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
    public abstract string Prompt(BlockoutWalkPlayer player);

    public abstract void Interact(BlockoutWalkPlayer player);
}
