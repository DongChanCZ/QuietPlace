using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class FloatEvent : UnityEvent<float> { }

public class CutsceneCameraDolly : MonoBehaviour
{
    [Header("이동 지점")]
    public Transform closeupPoint;
    public Transform wideShotPoint;

    [Header("타이밍")]
    public float holdBeforeDolly = 2f;
    public float dollyDuration = 6f;

    [Header("이벤트")]
    public UnityEvent onDollyStart;
    public FloatEvent onDollyProgress;
    public UnityEvent onDollyComplete;

    void Start()
    {
        if (closeupPoint != null)
        {
            transform.position = closeupPoint.position;
            transform.rotation = closeupPoint.rotation;
        }
        StartCoroutine(RunDolly());
    }

    IEnumerator RunDolly()
    {
        yield return new WaitForSeconds(holdBeforeDolly);
        onDollyStart?.Invoke();

        float t = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        while (t < dollyDuration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dollyDuration));
            transform.position = Vector3.Lerp(startPos, wideShotPoint.position, ratio);
            transform.rotation = Quaternion.Slerp(startRot, wideShotPoint.rotation, ratio);
            onDollyProgress?.Invoke(ratio);
            yield return null;
        }

        transform.position = wideShotPoint.position;
        transform.rotation = wideShotPoint.rotation;
        onDollyComplete?.Invoke();
    }
}
