using UnityEngine;
using UnityEngine.SceneManagement;

// IntroTextSequencer.onIntroComplete 같은 UnityEvent에 연결해서 즉시 다음 씬으로 넘어간다.
// ProceedOnInput은 입력을 한 번 더 기다리는 용도라 별개로 둠.
public class LoadSceneOnEvent : MonoBehaviour
{
    public string nextSceneName;

    public void Load()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
