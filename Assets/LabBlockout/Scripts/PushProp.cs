using UnityEngine;

/// A prop the player drags to one fixed spot - the dumpster that has to end up
/// under the guard-room window before it can be climbed.
///
/// It slides along a stored line instead of being pushed by physics. A
/// rigidbody shoved by a CharacterController drifts and spins, and the climb
/// into the window only works from a narrow band of positions, so "shove it
/// roughly there" would fail for reasons the player cannot see. One press, one
/// destination.
[DisallowMultipleComponent]
public sealed class PushProp : Interactable
{
    [Tooltip("최종 도착점(창문 밑). 여기 도착하면 넘어가기가 활성화된다.")]
    [SerializeField] Transform anchor;

    [Tooltip("한 번 더 밀어야 하는 경우의 중간 정차점(원래 있던 자리). 비워두면 예전처럼 " +
             "한 번만 밀어서 곧장 anchor 로 간다.")]
    [SerializeField] Transform intermediateAnchor;

    [SerializeField] string displayName = "쓰레기통";
    [SerializeField] float seconds = 1.4f;

    [Header("민 뒤 - 딱 붙어야만 넘어가기가 뜬다")]
    [SerializeField] float vaultInteractDistance = 1.2f;

    [Tooltip("창문 너머 도착 지점. TryVault 는 접근 각도에 따라 실패할 수 있어서, 미리 " +
             "검증해 둔 고정 지점으로 곧장 보낸다. 비워두면 예전처럼 TryVault 로 더듬어 찾는다.")]
    [SerializeField] Transform vaultLandingPoint;

    [Tooltip("넘어가는 도중 반드시 거치는 정점(쓰레기통 위/창틀 높이) - 직선이 아니라 " +
             "쓰레기통과 창문을 짚고 넘어가는 포물선으로 보이게 한다. 비워도 동작은 하지만 낮고 밋밋해진다.")]
    [SerializeField] Transform vaultPeakPoint;

    [Header("끄는 소리")]
    [SerializeField] AudioClip dragSound;
    [SerializeField] AudioSource dragSource;

    [Tooltip("클립 앞에서 이만큼만 재생한다(초). 0 이면 클립을 끝까지 재생한다.")]
    [SerializeField] float dragSoundSeconds = 1f;

    [Tooltip("밀기 시작하고 이만큼 지난 뒤 소리를 낸다(초)")]
    [SerializeField] float dragSoundDelay;

    enum Stage { AtStart, AtIntermediate, AtFinal }
    Stage stage = Stage.AtStart;

    /// 예전 이름 그대로 유지 - "넘어가기가 활성화되는 최종 자리에 도착했는가".
    public bool Moved => stage == Stage.AtFinal;

    Vector3 from;
    Vector3 to;
    float t = -1f;

    /// 클립 이름은 코드에 둔다 - 인스펙터 참조는 병합 때 빠지지만 이 문자열은 남는다.
    const string DragSoundName = "Dumpster dragging wheels_2";

    void Awake()
    {
        Debug.Assert(anchor, "PushProp is missing its anchor.");

        dragSound = AudioLibrary.Resolve(dragSound, DragSoundName);
        dragSource = AudioLibrary.EnsureSource(dragSource, transform, "DragSource", 0.85f, 2f, 25f, 0.9f);
    }

    /// 밀기 전엔 평소 조준거리(PlayerInteractor.interactionDistance)를 그대로 쓰지만,
    /// 밀고 난 뒤의 "넘어가기"는 스페이스바 시절과 달리 딱 붙어야만 나와야 한다 -
    /// 안 그러면 창문 앞 아무 데서나 E 하나로 훌쩍 넘어가 버린다.
    public override bool Available(IInteractor player)
    {
        if (anchor == null) return false;
        if (stage != Stage.AtFinal) return true;

        Transform playerT = (player as MonoBehaviour)?.transform;
        if (playerT == null) return false;
        return (playerT.position - transform.position).sqrMagnitude <= vaultInteractDistance * vaultInteractDistance;
    }

    public override string Prompt(IInteractor player)
    {
        if (anchor == null) return null;
        return stage == Stage.AtFinal ? "E  넘어가기" : "E  " + displayName + " 밀기";
    }

    public override void Interact(IInteractor player)
    {
        if (anchor == null) return;

        switch (stage)
        {
            case Stage.AtStart:
                // 중간 정차점이 없으면 예전처럼 한 번에 최종 자리로 곧장 민다.
                Transform firstDest = intermediateAnchor != null ? intermediateAnchor : anchor;
                stage = intermediateAnchor != null ? Stage.AtIntermediate : Stage.AtFinal;
                SlideTo(firstDest);
                PlayDrag();
                if (player != null) player.ShowNotice(displayName + "을 밀었습니다.");
                return;

            case Stage.AtIntermediate:
                stage = Stage.AtFinal;
                SlideTo(anchor);
                PlayDrag();
                if (player != null) player.ShowNotice(displayName + "을 창문 아래로 밀었습니다.");
                return;

            default: // AtFinal - 밀어서 창문 밑에 자리잡은 뒤에는 여기서 바로 넘어간다.
                PlayerController controller = (player as MonoBehaviour)?.GetComponent<PlayerController>();
                if (controller == null) return;

                if (vaultLandingPoint != null) controller.VaultToPoint(vaultLandingPoint, vaultPeakPoint);
                else controller.TryVault(); // 도착 지점을 안 잡아뒀으면 예전 방식대로 더듬어 찾는다
                return;
        }
    }

    void SlideTo(Transform dest)
    {
        from = transform.position;
        to = dest.position;
        t = 0f;
    }

    /// <summary>
    /// 끄는 소리. 클립이 미는 시간보다 길어서 다 틀면 쓰레기통이 멈춘 뒤에도 소리가 남는다.
    /// 그래서 앞부분만 잘라 쓴다.
    /// </summary>
    void PlayDrag()
    {
        if (dragSource == null || dragSound == null) return;
        StartCoroutine(DragRoutine());
    }

    System.Collections.IEnumerator DragRoutine()
    {
        if (dragSoundDelay > 0f) yield return new WaitForSeconds(dragSoundDelay);

        dragSource.clip = dragSound;
        dragSource.time = 0f;
        dragSource.Play();

        if (dragSoundSeconds <= 0f) yield break;

        yield return new WaitForSeconds(dragSoundSeconds);
        if (dragSource != null && dragSource.clip == dragSound) dragSource.Stop();
    }

    /// Skip the slide and land on the anchor. The walk checks drive Move() by
    /// hand, so a coroutine or an Update-driven slide never advances for them.
    public void MoveNow()
    {
        if (anchor == null) return;
        stage = Stage.AtFinal;
        t = -1f;
        transform.position = anchor.position;
        Physics.SyncTransforms();
    }

    void Update()
    {
        if (t < 0f) return;
        t += Time.deltaTime / Mathf.Max(0.01f, seconds);
        if (t >= 1f) { t = -1f; transform.position = to; }
        else transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
    }
}
