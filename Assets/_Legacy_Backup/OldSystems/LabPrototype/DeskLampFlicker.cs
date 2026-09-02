using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeskLampFlicker : MonoBehaviour
{
    [SerializeField] private Light lamp;
    [SerializeField] private float baseIntensity = 1.65f;
    [SerializeField] private float noiseAmount = 0.28f;
    [SerializeField] private float noiseSpeed = 13f;
    [SerializeField] private float rareDimChance = 0.018f;

    private float seed;
    private float dimTimer;

    private void Awake()
    {
        if (lamp == null)
            lamp = GetComponent<Light>();

        seed = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        if (lamp == null)
            return;

        if (dimTimer <= 0f && Random.value < rareDimChance * Time.deltaTime * 60f)
            dimTimer = Random.Range(0.035f, 0.11f);

        dimTimer -= Time.deltaTime;
        float noise = Mathf.PerlinNoise(seed, Time.time * noiseSpeed);
        float intensity = baseIntensity + (noise - 0.5f) * noiseAmount;

        if (dimTimer > 0f)
            intensity *= 0.18f;

        lamp.intensity = Mathf.Max(0.08f, intensity);
    }
}
