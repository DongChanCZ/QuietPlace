using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    public Light targetLight;
    public float baseIntensity = 1.5f;
    public float flickerAmount = 0.15f;
    public float flickerSpeed = 3f;

    private float seed;

    void Start()
    {
        seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (targetLight == null) return;
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, seed);
        targetLight.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount;
    }
}
