using UnityEngine;

/// HidingSpotQTE 의 숨기/나오기 이벤트에 얹어서 소음 등급을 처리한다.
///
/// HidingSpotQTE.cs 자체는 건드리지 않는다 - 이미 나와 있는 onHideSuccess/onUnhide
/// 이벤트만 구독하므로, 팀원이 그 스크립트를 다시 손봐도 여기와는 충돌하지 않는다.
[RequireComponent(typeof(HidingSpotQTE))]
public class HidingNoiseLink : MonoBehaviour
{
    [Header("문 여닫는 소리 (숨기/나오기 순간 한 번씩)")]
    [SerializeField] float doorNoiseLoudness = 2f;
    [SerializeField] float doorNoiseRange = 6f;

    HidingSpotQTE qte;

    void Awake()
    {
        qte = GetComponent<HidingSpotQTE>();
        qte.onHideSuccess.AddListener(HandleHide);
        qte.onUnhide.AddListener(HandleUnhide);
    }

    void HandleHide()
    {
        EmitDoorNoise();
        SetNoise(0f, 0f);
    }

    void HandleUnhide()
    {
        EmitDoorNoise();
        if (qte.playerController != null)
            SetNoise(qte.playerController.stats.noiseLevelBase, qte.playerController.stats.noiseRangeBase);
    }

    void EmitDoorNoise()
    {
        if (NoiseManager.Instance != null)
            NoiseManager.Instance.Emit(new NoiseEvent(transform.position, doorNoiseLoudness, doorNoiseRange, NoiseType.ObjectImpact));
    }

    void SetNoise(float level, float range)
    {
        if (qte.playerController == null) return;
        qte.playerController.stats.currentNoiseLevel = level;
        qte.playerController.stats.currentNoiseRange = range;
    }
}
