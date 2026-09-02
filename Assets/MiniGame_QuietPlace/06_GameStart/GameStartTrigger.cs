using UnityEngine;

public class GameStartTrigger : MonoBehaviour
{
    public FlashlightController flashlight;
    public TutorialMonsterEncounter monsterEncounter;
    public AudioSource audioSource;
    public AudioClip monsterRunningSound;

    private bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player")) return;
        triggered = true;

        if (flashlight != null) flashlight.TurnOn();

        if (audioSource != null && monsterRunningSound != null)
        {
            audioSource.PlayOneShot(monsterRunningSound);
        }

        if (monsterEncounter != null) monsterEncounter.BeginEncounter();
    }
}
