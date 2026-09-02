using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerNoiseEmitter : MonoBehaviour
{
    public float emitInterval = 0.3f;
    private PlayerController player;
    private float timer;

    void Awake() => player = GetComponent<PlayerController>();

    void Update()
    {
        if (player.stats.currentNoiseLevel <= 0f) { timer = 0f; return; }
        timer += Time.deltaTime;
        if (timer < emitInterval) return;
        timer = 0f;

        NoiseManager.Instance.Emit(new NoiseEvent(
            transform.position, player.stats.currentNoiseLevel,
            player.stats.currentNoiseRange, NoiseType.Footstep));
    }
}
