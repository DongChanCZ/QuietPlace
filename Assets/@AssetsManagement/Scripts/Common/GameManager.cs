using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    bool gameOverTriggered;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ponytail: 데스씬(컷신 작업자 담당) 없이 바로 씬 리로드로 대체. 컷신 붙으면 여기서
    // 데스씬 재생 후 정식 게임오버 화면으로 넘기도록 교체.
    public void TriggerGameOver()
    {
        if (gameOverTriggered) return;
        gameOverTriggered = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
