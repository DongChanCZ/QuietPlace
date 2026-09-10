using UnityEngine;
using UnityEngine.UI;

// PlayerInteractor의 OnNotice 이벤트를 받아 화면 하단에 잠깐 띄운다.
// 자기 자신은 항상 켜둔 채 Text.enabled만 토글한다 (PlayerStateVisibility와 같은 이유:
// SetActive(false)로 자기 자신을 끄면 그 순간부터 Update가 안 불려서 다시 못 켬).
public class NoticeUI : MonoBehaviour
{
    public PlayerInteractor player;
    public Text noticeText;
    public float displaySeconds = 2.8f;

    private Image box;
    private float hideAt;
    private bool persistent;

    void Awake()
    {
        box = GetComponent<Image>();
    }

    void OnEnable()
    {
        if (player != null)
        {
            player.OnNotice += HandleNotice;
            player.OnPersistentNotice += HandlePersistentNotice;
            player.OnClearPersistentNotice += HandleClearPersistentNotice;
        }
        SetVisible(false);
    }

    void OnDisable()
    {
        if (player != null)
        {
            player.OnNotice -= HandleNotice;
            player.OnPersistentNotice -= HandlePersistentNotice;
            player.OnClearPersistentNotice -= HandleClearPersistentNotice;
        }
    }

    void HandleNotice(string text)
    {
        if (noticeText != null) noticeText.text = text;
        hideAt = Time.time + displaySeconds;
        persistent = false;
        SetVisible(true);
    }

    // 숨기처럼 상태가 풀릴 때까지 떠 있어야 하는 안내 - 시간이 지나도 저절로 안 사라진다.
    void HandlePersistentNotice(string text)
    {
        if (noticeText != null) noticeText.text = text;
        persistent = true;
        SetVisible(true);
    }

    void HandleClearPersistentNotice()
    {
        if (!persistent) return;
        persistent = false;
        SetVisible(false);
    }

    void Update()
    {
        if (!persistent && Time.time >= hideAt) SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (noticeText != null) noticeText.enabled = visible;
        if (box != null) box.enabled = visible;
    }
}
