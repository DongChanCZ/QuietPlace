using UnityEngine;

/// 캐비닛·책상 밑 같은 은신처의 E 조작구.
///
/// 실제 숨기/나오기는 팀원의 HidingSpotQTE 가 다 한다. 이 스크립트는 두 가지만 한다.
///  - 조준했을 때 화면에 뜨는 키 안내문
///  - E 한 번으로 숨고, 숨은 상태에서 E 로 나오기
///
/// 캐비닛에는 SlidingDoor 도 붙어 있는데, Interactable 이 한 오브젝트에 둘이면
/// PlayerInteractor 가 어느 쪽을 잡을지 알 수 없다. 그래서 문은 컴포넌트를 꺼 두고
/// (꺼진 Interactable 은 조준에서 빠진다) 여기서 SetOpenImmediate 로 직접 여닫는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(HidingSpotQTE))]
public sealed class HidingSpot : Interactable
{
    [SerializeField] string displayName = "캐비닛";

    /// 여닫을 문. 책상 밑처럼 문이 없는 자리는 비워둔다.
    [SerializeField] SlidingDoor door;

    /// 숨은 동안 시점을 이만큼 내린다(플레이어 로컬 단위, 월드로는 x1.5).
    /// 책상 밑처럼 천판이 낮은 자리는 그냥 숨으면 머리가 천판 위로 튀어나온다.
    /// 캐비닛은 2.08m 여유가 있어서 0 이면 된다.
    [SerializeField] float hiddenEyeDrop = 0f;

    HidingSpotQTE spot;

    void Awake()
    {
        spot = GetComponent<HidingSpotQTE>();
        // 문이 E 를 두고 다투지 않게 한다. 컴포넌트를 꺼도 SetOpenImmediate 는 그대로 쓴다.
        if (door != null) door.enabled = false;
    }

    // 물건을 들고 있어도 숨을 수는 있어야 한다
    public override bool AllowedWhileHandsFull => true;

    public override string Prompt(IInteractor player)
    {
        return spot.IsPlayerHidden ? "E  나오기" : "E  " + displayName + "에 숨기";
    }

    public override void Interact(IInteractor player)
    {
        if (spot.IsPlayerHidden)
        {
            if (door != null) door.SetOpenImmediate(true);
            spot.Unhide();
            SetEyeDrop(0f);
            if (door != null) door.SetOpenImmediate(false);
            if (player != null)
            {
                player.ClearPersistentNotice();
                player.ShowNotice(displayName + "에서 나왔습니다.");
            }
            return;
        }

        if (door != null) door.SetOpenImmediate(true);
        spot.Hide();
        SetEyeDrop(hiddenEyeDrop);
        if (door != null) door.SetOpenImmediate(false);   // 들어간 뒤 문을 닫아 준다
        // 일반 안내(ShowNotice)와 달리 숨어있는 동안 계속 떠 있어야 한다 - 나올 때까지 시간이 지나도 안 사라짐.
        if (player != null) player.ShowPersistentNotice("숨었습니다.  E 로 나오기");
    }

    /// PlayerController 는 HidingSpotQTE 가 Start 에서 찾아 들고 있다. 여기서 다시 찾지 않는다.
    void SetEyeDrop(float drop)
    {
        if (spot.playerController != null) spot.playerController.HiddenEyeDrop = drop;
    }
}
