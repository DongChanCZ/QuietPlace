using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주파수 대역이 목표에 맞았는지를 화면에 보여준다.
///
/// 퍼즐은 목표값을 랜덤으로 정하고 허용 오차가 좁다(기본 0.03). 화면에 아무 표시가 없으면
/// 플레이어가 목표를 찾을 방법이 없으므로, 슬라이더를 쓸어보다 맞는 순간 밝아지게 해서
/// "맞췄다"를 알려준다. 목표 위치를 미리 보여주지는 않는다 - 찾는 과정이 이 퍼즐의 내용이다.
/// </summary>
[RequireComponent(typeof(FrequencyBand))]
public class FrequencyBandFeedback : MonoBehaviour
{
    [Header("맞았을 때 켜는 표시 (LOCK 램프 등)")]
    [SerializeField] private GameObject matchedIndicator;

    [Header("색이 변할 대상 - 비우면 FrequencyBand 의 fillImage 를 쓴다")]
    [SerializeField] private Image tintTarget;

    [Range(0f, 1f)]
    [SerializeField] private float unmatchedDim = 0.32f;

    private FrequencyBand band;
    private Image image;
    private bool lastMatched;
    private bool initialized;

    void Awake()
    {
        band = GetComponent<FrequencyBand>();
    }

    void Update()
    {
        if (band == null) return;

        // fillImage 는 FrequencyBand.Start 에서 색이 설정되므로 그 뒤에 잡는다
        if (image == null)
        {
            image = tintTarget != null ? tintTarget : band.fillImage;
            if (image == null) return;
        }

        if (initialized && band.IsMatched == lastMatched) return;

        initialized = true;
        lastMatched = band.IsMatched;

        Color c = band.bandColor;
        if (!band.IsMatched) c *= unmatchedDim;
        c.a = 1f;
        image.color = c;

        if (matchedIndicator != null) matchedIndicator.SetActive(band.IsMatched);
    }
}
