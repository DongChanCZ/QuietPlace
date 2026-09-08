using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주파수 대역 하나를 파형으로 보여준다.
///
/// 화면에 목표 파형(희미하게 정지)과 내 파형(밝게 흐름)을 겹쳐 놓고, 슬라이더를 움직여
/// 두 파형의 주기를 맞추게 한다. 채워지는 막대만 있을 때는 무엇을 맞추는지 알 수 없어서
/// 감으로 쓸어보는 수밖에 없었는데, 파형으로 보면 "더 촘촘하게/더 넓게"가 눈에 보인다.
///
/// FrequencyBand 는 손대지 않고 읽기만 한다(팀원 스크립트).
/// </summary>
[RequireComponent(typeof(FrequencyBand))]
public class FrequencyBandWave : MonoBehaviour
{
    [Header("목표 파형 (희미하게, 정지)")]
    [SerializeField] private FrequencyWaveView targetWave;

    [Header("내 파형 (밝게, 흐른다)")]
    [SerializeField] private FrequencyWaveView currentWave;

    [Header("맞았을 때 켜는 표시 (LOCK 램프 등)")]
    [SerializeField] private GameObject matchedIndicator;

    [Header("차수를 적어주는 라벨 (1차 화음 ...)")]
    [SerializeField] private TMPro.TMP_Text harmonicLabel;

    [Header("슬라이더 위 목표 자리 눈금")]
    [SerializeField] private RectTransform targetTick;
    [SerializeField] private float trackWidth = 660f;
    [SerializeField] private float trackCenterX;

    [Header("선 굵기 - 맞았을 때 / 아닐 때")]
    [SerializeField] private float matchedThickness = 7f;
    [SerializeField] private float normalThickness = 5f;

    [Header("맞기 전 흐리기 정도")]
    [Range(0f, 1f)]
    // 목표선도 같은 색이라 너무 흐리면 어느 쪽이 내 파형인지 헷갈린다.
    [SerializeField] private float unmatchedDim = 0.75f;

    private FrequencyBand _band;
    private bool _lastMatched;
    private bool _initialized;
    private float _lastTarget = -1f;

    void Awake()
    {
        _band = GetComponent<FrequencyBand>();
    }

    void OnEnable()
    {
        _initialized = false;
        _lastTarget = -1f;
    }

    void Update()
    {
        if (_band == null) return;

        // 목표는 판이 시작될 때 랜덤으로 정해지므로 바뀔 때만 다시 그린다.
        if (targetWave != null && !Mathf.Approximately(_lastTarget, _band.targetValue))
        {
            _lastTarget = _band.targetValue;
            targetWave.SetValue01(_band.targetValue);

            // 슬라이더 위 눈금도 같은 자리로 옮긴다.
            if (targetTick != null)
                targetTick.anchoredPosition = new Vector2(
                    trackCenterX + (_band.targetValue - 0.5f) * trackWidth,
                    targetTick.anchoredPosition.y);
        }

        if (currentWave != null && _band.slider != null)
        {
            currentWave.SetValue01(_band.slider.value);

            // 지금 몇 차 화음인지 숫자로도 보여준다. 파형만 보면 3차인지 4차인지 헷갈린다.
            if (harmonicLabel != null)
                harmonicLabel.text = currentWave.Harmonic.ToString("0.0") + "차";
        }

        if (_initialized && _band.IsMatched == _lastMatched) return;

        _initialized = true;
        _lastMatched = _band.IsMatched;

        // 맞으면 내 파형이 제 색으로 밝아지고 굵어지며 LOCK 이 켜진다.
        if (currentWave != null)
        {
            Color c = _band.bandColor;
            if (!_band.IsMatched) c *= unmatchedDim;
            c.a = 1f;
            currentWave.SetColor(c);
            currentWave.SetThickness(_band.IsMatched ? matchedThickness : normalThickness);
        }

        if (harmonicLabel != null)
            harmonicLabel.color = _band.IsMatched ? _band.bandColor : _band.bandColor * 0.6f;

        if (matchedIndicator != null) matchedIndicator.SetActive(_band.IsMatched);
    }
}
