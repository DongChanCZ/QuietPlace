using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 개발용 인트로 스킵. P 를 누르면 타자 화면 / 어두운 방 / 숲 이미지 / 팁 화면을
/// 건너뛰고 곧바로 인게임 씬으로 넘어간다.
///
/// 스킵 처리는 UNITY_EDITOR 또는 DEVELOPMENT_BUILD 에서만 컴파일된다.
/// 배포 빌드에서는 Update 자체가 없어서 플레이어는 이 기능을 쓸 수 없다.
/// 클래스는 항상 컴파일되므로 씬에 붙여둬도 배포 빌드에서 "Missing Mono Script" 가 되지 않는다.
/// </summary>
public class IntroSkip : MonoBehaviour
{
    [Header("스킵했을 때 이동할 씬 (빌드 목록에 등록되어 있어야 한다)")]
    [SerializeField] private string sceneName = "GameScene";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("스킵 키")]
    [SerializeField] private Key skipKey = Key.P;

    private bool _skipping;

    void Update()
    {
        if (_skipping) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current[skipKey].wasPressedThisFrame) return;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[{nameof(IntroSkip)}] sceneName 이 비어 있다.", this);
            return;
        }

        _skipping = true;
        Debug.Log($"[{nameof(IntroSkip)}] 인트로를 스킵하고 '{sceneName}' 으로 이동한다. (개발용)");
        SceneManager.LoadScene(sceneName);
    }
#endif
}
