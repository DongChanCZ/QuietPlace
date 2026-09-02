using System.Collections;
using UnityEngine;
using TMPro;

// 모니터의 마지막 줄만 불규칙하게 깜빡이게 함 (앞줄들은 그대로 유지)
public class BlinkingText : MonoBehaviour
{
    [SerializeField] private TMP_Text target;

    [Header("깜빡임 타이밍 (불규칙)")]
    [SerializeField] private float onMin = 0.28f;
    [SerializeField] private float onMax = 0.65f;
    [SerializeField] private float offMin = 0.07f;
    [SerializeField] private float offMax = 0.20f;

    private string _prefix = string.Empty;
    private string _blinkPart = string.Empty;
    private Coroutine _routine;

    public void SetTarget(TMP_Text text)
    {
        target = text;
    }

    // prefix: 계속 켜져 있을 앞 줄들 / blinkPart: 깜빡일 마지막 줄
    public void Begin(string prefix, string blinkPart)
    {
        _prefix = prefix;
        _blinkPart = blinkPart;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(BlinkLoop());
    }

    public void StopBlinking(bool leaveVisible = true)
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        Render(leaveVisible);
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            Render(true);
            yield return new WaitForSeconds(Random.Range(onMin, onMax));

            Render(false);
            yield return new WaitForSeconds(Random.Range(offMin, offMax));
        }
    }

    private void Render(bool visible)
    {
        if (target == null) return;

        // 안 보일 때도 줄 자리는 유지되도록 알파만 0으로 (레이아웃 흔들림 방지)
        string tail = visible ? _blinkPart : "<alpha=#00>" + _blinkPart + "<alpha=#FF>";
        target.text = string.IsNullOrEmpty(_prefix) ? tail : _prefix + "\n" + tail;
    }
}
