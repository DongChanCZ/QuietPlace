using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlashlightPickup : Interactable
{
    [SerializeField] string displayName = "손전등";
    public string DisplayName => displayName;
    /// 집어 가는 물건은 뒤/속에 있는 기계보다 먼저 잡혀야 한다.
    public override int AimPriority => 10;

    /// 이미 손전등이 있으면 조준 대상이 아니다. 안내문도 안 뜨는 채로 조준을
    /// 차지하면 뒤에 있는 것을 만질 수 없다.
    public override bool Available(IInteractor player) => player == null || !player.HasFlashlight;


    public override string Prompt(IInteractor player)
    {
        return player != null && player.HasFlashlight ? null : "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        Collect(player);
    }

    public void Collect(IInteractor player)
    {
        if (player == null || player.HasFlashlight) return;
        player.GiveFlashlight();
        gameObject.SetActive(false);
    }
}
