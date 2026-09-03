using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 방 안 모니터 화면의 마지막 줄을 깜빡이게 한다.
// 배경 이미지에 글씨가 그려져 있으므로, 글자 획 모양의 패치를 덮었다 걷었다 하는 방식.
// 패치가 보이면(alpha 1) 글자가 가려져 "꺼진" 것처럼 보인다.
public class BlinkingScreenLine : MonoBehaviour
{
    [SerializeField] private Image maskPatch;

    [Header("글자가 보이는 시간")]
    [SerializeField] private float visibleMin = 0.55f;
    [SerializeField] private float visibleMax = 1.35f;

    [Header("글자가 꺼져 있는 시간")]
    [SerializeField] private float hiddenMin = 0.06f;
    [SerializeField] private float hiddenMax = 0.16f;

    [Tooltip("가끔 두세 번 연속으로 깜빡이게 할 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float doubleBlinkChance = 0.35f;

    [SerializeField] private bool playOnEnable = true;

    private Coroutine _routine;

    void OnEnable()
    {
        SetHidden(false);
        if (playOnEnable) Begin();
    }

    void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    public void Begin()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Loop());
    }

    public void StopBlinking()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        SetHidden(false);
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            SetHidden(false);
            yield return new WaitForSeconds(Random.Range(visibleMin, visibleMax));

            SetHidden(true);
            yield return new WaitForSeconds(Random.Range(hiddenMin, hiddenMax));

            // 가끔 자잘하게 한 번 더 깜빡
            if (Random.value < doubleBlinkChance)
            {
                SetHidden(false);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
                SetHidden(true);
                yield return new WaitForSeconds(Random.Range(hiddenMin, hiddenMax));
            }
        }
    }

    private void SetHidden(bool hidden)
    {
        if (maskPatch == null) return;
        Color c = maskPatch.color;
        c.a = hidden ? 1f : 0f;
        maskPatch.color = c;
    }
}
