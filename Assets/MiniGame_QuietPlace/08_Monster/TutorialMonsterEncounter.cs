using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TutorialMonsterEncounter : MonoBehaviour
{
    [Header("이동")]
    public Transform approachPoint;
    public Transform retreatPoint;
    public float moveSpeed = 4f;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip footstepLoop;
    public AudioClip strangeSound;
    public AudioClip listenSound;
    public float listenPauseDuration = 2f;

    [Header("QTE 연동")]
    public HidingSpotQTE hidingSpot;
    public float qteLeadTime = 1.5f;

    public UnityEvent onEncounterComplete;

    public void BeginEncounter()
    {
        StartCoroutine(RunEncounter());
    }

    IEnumerator RunEncounter()
    {
        PlayFootsteps();

        if (hidingSpot != null)
        {
            StartCoroutine(TriggerQteBeforeArrival());
        }

        yield return StartCoroutine(MoveTo(approachPoint));
        StopFootsteps();

        if (strangeSound != null)
        {
            audioSource.PlayOneShot(strangeSound);
            yield return new WaitForSeconds(strangeSound.length);
        }

        if (listenSound != null)
        {
            audioSource.PlayOneShot(listenSound);
        }
        yield return new WaitForSeconds(listenPauseDuration);

        PlayFootsteps();
        yield return StartCoroutine(MoveTo(retreatPoint));
        StopFootsteps();

        onEncounterComplete?.Invoke();
    }

    IEnumerator TriggerQteBeforeArrival()
    {
        float travelDistance = Vector3.Distance(transform.position, approachPoint.position);
        float travelTime = travelDistance / moveSpeed;
        float waitBeforeQte = Mathf.Max(0f, travelTime - qteLeadTime);

        yield return new WaitForSeconds(waitBeforeQte);
        hidingSpot.BeginQte();
    }

    IEnumerator MoveTo(Transform target)
    {
        if (target == null) yield break;

        while (Vector3.Distance(transform.position, target.position) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);
            yield return null;
        }
    }

    void PlayFootsteps()
    {
        if (audioSource == null || footstepLoop == null) return;
        audioSource.clip = footstepLoop;
        audioSource.loop = true;
        audioSource.Play();
    }

    void StopFootsteps()
    {
        if (audioSource != null) audioSource.Stop();
    }
}
