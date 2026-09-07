using UnityEngine;
using UnityEngine.UI;

// 손전등이 켜져 있는 동안만 배터리 잔량 아이콘을 보여준다.
// 잔량 구간(75~100/50~75/25~50/0~25/0)에 따라 스프라이트를 바꾸고,
// 보이는 동안 계속 알파를 천천히 오가며 깜빡인다.
public class BatteryUI : MonoBehaviour
{
    public PlayerInteractor player;
    public Sprite[] batterySprites; // [0]=가득(1) ... [4]=방전(5)

    [Header("Flicker")]
    public float flickerSpeed = 2f;
    public float minAlpha = 0.5f;
    public float maxAlpha = 1f;

    Image image;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void Update()
    {
        if (player == null || image == null) return;

        bool visible = player.FlashlightEnabled;
        image.enabled = visible;
        if (!visible) return;

        image.sprite = SpriteForLevel(player.BatteryLevel);
        float t = (Mathf.Sin(Time.time * flickerSpeed) + 1f) * 0.5f;
        Color c = image.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        image.color = c;
    }

    Sprite SpriteForLevel(float level)
    {
        if (level <= 0f) return batterySprites[4];
        if (level <= 25f) return batterySprites[3];
        if (level <= 50f) return batterySprites[2];
        if (level <= 75f) return batterySprites[1];
        return batterySprites[0];
    }
}
