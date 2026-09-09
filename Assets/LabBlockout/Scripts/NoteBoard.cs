using UnityEngine;

/// 벽에 붙은 안내문·메모지를 조준하고 E 를 눌러 전체화면으로 읽는다.
///
/// 팀원의 ReadableNote 는 거리만 보고 E 키를 직접 읽는다. 안전수칙 게시판과 그 위에
/// 겹쳐 붙인 메모지처럼 40cm 안에 두 장이 있으면 어느 쪽이 열릴지 알 수 없어서,
/// 조준으로 고르는 Interactable 로 바꿔 단다. 실제 표시는 FullscreenNoteViewer 가 한다.
[DisallowMultipleComponent]
public sealed class NoteBoard : Interactable
{
    [SerializeField] Sprite page;
    [SerializeField] string displayName = "쪽지";

    /// 게시판 액자보다 그 위에 붙은 메모지가 먼저 잡혀야 한다.
    [SerializeField] int aimPriority = 1;

    // 물건을 들고 있어도 벽에 붙은 글은 읽을 수 있어야 한다
    public override bool AllowedWhileHandsFull => true;

    public override int AimPriority => aimPriority;

    /// 이미 읽는 중이면 조준 대상에서 빠진다. 안 그러면 닫으려고 누른 E 가
    /// 다시 열기로 먹혀서 영영 못 닫는다.
    public override bool Available(IInteractor player)
    {
        return page != null && FullscreenNoteViewer.Instance != null && !FullscreenNoteViewer.Instance.IsOpen;
    }

    public override string Prompt(IInteractor player)
    {
        return "E  " + displayName + " 읽기";
    }

    public override void Interact(IInteractor player)
    {
        if (FullscreenNoteViewer.Instance == null || page == null) return;
        FullscreenNoteViewer.Instance.Show(page);
    }
}
