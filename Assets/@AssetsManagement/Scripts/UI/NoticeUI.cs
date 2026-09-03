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

    void Awake()
    {
        box = GetComponent<Image>();
    }

    void OnEnable()
    {
        if (player != null) player.OnNotice += HandleNotice;
        SetVisible(false);
    }

    void OnDisable()
    {
        if (player != null) player.OnNotice -= HandleNotice;
    }

    void HandleNotice(string text)
    {
        if (noticeText != null) noticeText.text = text;
        hideAt = Time.time + displaySeconds;
        SetVisible(true);
    }

    void Update()
    {
        if (Time.time >= hideAt) SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (noticeText != null) noticeText.enabled = visible;
        if (box != null) box.enabled = visible;
    }
}
