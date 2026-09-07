using UnityEngine;
using UnityEngine.UI;

// 조준 중인 Interactable의 프롬프트를 화면 중앙(크로스헤어 아래)에 띄운다.
// 문(SlidingDoor) 열기/닫기류 프롬프트는 제외 - 상호작용 자체는 그대로 E로 가능.
public class InteractionPromptUI : MonoBehaviour
{
    public PlayerInteractor player;

    Text label;

    void Awake()
    {
        label = GetComponent<Text>();
    }

    void Update()
    {
        if (player == null || label == null) return;

        Interactable target = player.Aimed;
        string prompt = target != null && !(target is SlidingDoor) ? target.Prompt(player) : null;

        label.text = prompt ?? string.Empty;
        label.enabled = !string.IsNullOrEmpty(prompt);
    }
}
