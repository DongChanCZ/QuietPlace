using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ImageCrossFade : MonoBehaviour
{
    [Header("Images (통이미지 2장)")]
    [SerializeField] private CanvasGroup imageGroupA; // 첫 번째 이미지의 CanvasGroup
    [SerializeField] private CanvasGroup imageGroupB; // 두 번째 이미지의 CanvasGroup (의자 돌아간 사진)

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1.0f; // 의자가 돌아가는 데 걸리는 시간 (초)
    [SerializeField] private float startDelay = 0.5f;   // 연출 시작 전 대기 시간

    void Start()
    {
        if (imageGroupA == null || imageGroupB == null)
        {
            Debug.LogError("CanvasGroup을 인스펙터에 연결해주세요!");
            return;
        }

        // 초기 상태 설정: A만 보이고 B는 투명하게
        imageGroupA.alpha = 1f;
        imageGroupB.alpha = 0f;

        // 페이드 코루틴 시작
        StartCoroutine(CrossFadeRoutine());
    }

    IEnumerator CrossFadeRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;

            // 부드러운 전환을 위해 SmoothStep 적용
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // A는 점점 투명하게, B는 점점 선명하게
            imageGroupA.alpha = 1f - smoothT;
            imageGroupB.alpha = smoothT;

            yield return null;
        }

        // 최종 상태 확정
        imageGroupA.alpha = 0f;
        imageGroupB.alpha = 1f;
    }
}
