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
    [SerializeField] Transform anchor;
    [SerializeField] string displayName = "쓰레기통";
    [SerializeField] float seconds = 1.4f;

    [Header("끄는 소리")]
    [SerializeField] AudioClip dragSound;
    [SerializeField] AudioSource dragSource;

    [Tooltip("클립 앞에서 이만큼만 재생한다(초). 0 이면 클립을 끝까지 재생한다.")]
    [SerializeField] float dragSoundSeconds = 1f;

    [Tooltip("밀기 시작하고 이만큼 지난 뒤 소리를 낸다(초)")]
    [SerializeField] float dragSoundDelay;

    public bool Moved { get; private set; }

    Vector3 from;
    Vector3 to;
    float t = -1f;

    void Awake()
    {
        Debug.Assert(anchor, "PushProp is missing its anchor.");
    }

    public override string Prompt(IInteractor player)
    {
        if (Moved || anchor == null) return null;
        return "E  " + displayName + " 밀기";
    }

    public override void Interact(IInteractor player)
    {
        if (Moved || anchor == null) return;
        Moved = true;
        from = transform.position;
        to = anchor.position;
        t = 0f;
        PlayDrag();
        if (player != null) player.ShowNotice(displayName + "을 창문 아래로 밀었습니다.");
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
        Moved = true;
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
