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

    /// ShowNotice 와 달리 정해진 시간 뒤 저절로 안 사라진다 - ClearPersistentNotice 를
    /// 부를 때까지 유지된다. 숨기처럼 "그 상태가 풀릴 때까지" 안내가 떠 있어야 하는 경우용.
    void ShowPersistentNotice(string text);
    void ClearPersistentNotice();
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

    /// 조준이 여러 대상에 걸렸을 때 누가 이기는가. 큰 값이 먼저다.
    ///
    /// 거리만으로 고르면 큰 기계 안에 놓인 작은 물건을 집을 수 없다. 실제로
    /// ITEM_2F_Wrench 는 PROP_2F_Switchboard 의 3.4x2.2x2.6m 짜리 박스 콜라이더에
    /// 완전히 파묻혀 있어서, 어느 방향에서 조준해도 배전반이 먼저 잡혀 집을 수가 없었다.
    /// 집어 가는 작은 물건은 뒤에 있는 기계를 이겨야 한다.
    public virtual int AimPriority => 0;

    /// <summary>
    /// 지금 이 대상을 쓸 수 있는가. false 면 조준 대상에서 아예 빠진다.
    ///
    /// AimPriority 만으로 고르면, 지금 쓸 수 없는 물건이 조준을 차지한 채 아무 안내도
    /// 내지 않아 뒤에 있는 진짜 대상이 가려진다. 실제로 두 가지가 그렇게 막혔다.
    ///   - 손에 든 변압기가 카메라 앞에 매달려 조준을 가져가 배전반에 꽂을 수 없었다.
    ///   - 볼트로 잠긴 고정판 안쪽의 고장난 변압기가 잡혀 판을 풀기 전에 빠져나왔다.
    /// Prompt 가 null 인 것과 뜻이 같지만, Prompt 는 문자열을 만들어 매 프레임
    /// 쓰레기를 남기므로 판정만 따로 둔다.
    /// </summary>
    public virtual bool Available(IInteractor player) => true;
}
