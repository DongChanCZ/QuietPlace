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

        // 씬을 다시 불러오면 소지품이 전부 사라진다. 불러오기 전에 지금 들고 있는 것을
        // 남겨두면, 되살아났을 때 죽던 순간 그대로 들고 시작한다.
        GameCheckpoint.CaptureOnDeath();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
