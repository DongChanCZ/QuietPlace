using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ProceedOnInput : MonoBehaviour
{
    public string nextSceneName;
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.15f;

    private bool listening;
    private bool triggered;

    // CutsceneCameraDolly.onDollyComplete 에 연결
    public void BeginListening()
    {
        listening = true;
    }

    void Update()
    {
        if (!listening || triggered) return;

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool entered = Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

        if (clicked || entered)
        {
            triggered = true;
            StartCoroutine(CutToNextScene());
        }
    }

    IEnumerator CutToNextScene()
    {
        if (fadeCanvasGroup != null)
        {
            float t = 0f;
            fadeCanvasGroup.alpha = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
