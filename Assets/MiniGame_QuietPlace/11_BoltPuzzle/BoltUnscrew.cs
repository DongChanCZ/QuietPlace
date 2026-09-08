using UnityEngine;
using UnityEngine.Events;

public class BoltUnscrew : MonoBehaviour
{
    [Header("몽키스패너 아이콘 (첫 클릭 시 표시)")]
    public GameObject wrenchIcon;

    [Header("회전/이탈 연출")]
    public int requiredClicks = 3;
    public float rotationPerClick = 90f;
    public float popOutDistancePerClick = 0.05f;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip turnSound;
    public AudioClip popOutSound;

    [Header("빠지면서 떨어지는 힘")]
    public Vector3 popForce = new Vector3(0f, -1f, 0.5f);

    public UnityEvent onRemoved;

    private int clickCount;
    private bool removed;

    public bool Removed => removed;

    /// 클릭 없이 즉시 빠지게 한다. 볼트 하나를 다 풀면 나머지가 따라 풀리는 연출에
    /// BoltPuzzleManager 가 쓴다.
    public void ForcePopOut()
    {
        if (removed) return;
        PopOut();
    }

    void OnMouseDown()
    {
        if (removed) return;

        if (clickCount == 0 && wrenchIcon != null)
        {
            wrenchIcon.SetActive(true);
        }

        clickCount++;

        // 시계 반대 방향 회전 (+ 값 = CCW) + 카메라 쪽으로 살짝 빠져나오는 연출
        transform.Rotate(0f, 0f, rotationPerClick, Space.Self);
        transform.Translate(0f, 0f, -popOutDistancePerClick, Space.Self);

        PlaySound(turnSound);

        if (clickCount >= requiredClicks)
        {
            PopOut();
        }
    }

    void PopOut()
    {
        removed = true;
        PlaySound(popOutSound);

        if (wrenchIcon != null) wrenchIcon.SetActive(false);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.AddForce(popForce, ForceMode.Impulse);

        onRemoved?.Invoke();
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
