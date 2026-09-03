using UnityEngine;

public class PulseHighlight : MonoBehaviour
{
    public Color emissionColor = Color.yellow;
    public float pulseSpeed = 2f;
    public float minIntensity = 0f;
    public float maxIntensity = 3f;

    private Material mat;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
        mat.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        mat.SetColor("_EmissionColor", emissionColor * intensity);
    }

    void OnDisable()
    {
        if (mat != null) mat.SetColor("_EmissionColor", Color.black);
    }
}
