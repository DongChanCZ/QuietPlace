using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class TipScreenSequencer : MonoBehaviour
{
    [Header("텍스트 팁 이미지 (던지기/숨기 안내)")]
    [SerializeField] private CanvasGroup imageGroup;

    [Header("타이밍")]
    [Range(0.3f, 1.5f)]
    [SerializeField] private float fadeInDuration = 0.6f;
    [Range(0.3f, 1.5f)]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Tooltip("페이드인이 끝나기 전에는 클릭을 받지 않음")]
    [SerializeField] private bool waitForFadeInBeforeClick = true;

    [Tooltip("이 시간 동안은 눌러도 안 넘어감 (팁을 읽을 시간 확보)")]
    [Range(0f, 10f)]
    [SerializeField] private float minDisplayDuration = 2.5f;

    [Header("완료 이벤트 (클릭 후 입구 앞으로 진행)")]
    [SerializeField] private UnityEvent onTipDismissed;

    private bool _canDismiss;
    private bool _dismissed;

    private void Awake()
    {
        if (imageGroup != null) imageGroup.alpha = 0f;
    }

    private void Start()
    {
        StartCoroutine(PlaySequence());
    }

    private void Update()
    {
        if (_dismissed || !_canDismiss) return;

        // anyKey 는 에디터에서 포커스가 바뀔 때 눌리지도 않은 입력을 보고해서
        // 팁이 뜨자마자 사라지는 문제가 있어, 진행용 키만 직접 확인한다
        bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool keyPressed = Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame
             || Keyboard.current.enterKey.wasPressedThisFrame
             || Keyboard.current.numpadEnterKey.wasPressedThisFrame
             || Keyboard.current.eKey.wasPressedThisFrame);

        if (mouseClicked || keyPressed)
        {
            _dismissed = true;
            StartCoroutine(DismissAndComplete());
        }
    }

    private IEnumerator PlaySequence()
    {
        if (waitForFadeInBeforeClick)
            yield return Fade(0f, 1f, fadeInDuration);
        else
            StartCoroutine(Fade(0f, 1f, fadeInDuration));

        if (minDisplayDuration > 0f) yield return new WaitForSeconds(minDisplayDuration);
        _canDismiss = true;
    }

    private IEnumerator DismissAndComplete()
    {
        _canDismiss = false;
        yield return Fade(1f, 0f, fadeOutDuration);
        onTipDismissed?.Invoke();
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (imageGroup == null) yield break;

        float elapsed = 0f;
        imageGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            imageGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        imageGroup.alpha = to;
    }
}
