using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class MainEntranceCollapse : MonoBehaviour
{
    public GameObject[] intactPieces;
    public GameObject fallingFragments;
    public GameObject debris;
    public Collider permanentBlocker;
    public ParticleSystem dust;

    bool triggered;
    public bool Triggered => triggered;
    public bool IsConfigured => intactPieces != null && intactPieces.Length > 0 &&
                                fallingFragments != null && debris != null &&
                                permanentBlocker != null;

    void Awake()
    {
        fallingFragments.SetActive(false);
        debris.SetActive(false);
        permanentBlocker.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        // BlockoutWalkPlayer for the blockout walk-test scene, PlayerController for the real game -
        // a given scene only ever has one of the two.
        if (other.GetComponentInParent<BlockoutWalkPlayer>() != null ||
            other.GetComponentInParent<PlayerController>() != null)
            TriggerCollapse();
    }

    [ContextMenu("Trigger Entrance Collapse")]
    public void TriggerCollapse()
    {
        if (triggered || !IsConfigured) return;
        triggered = true;
        foreach (GameObject piece in intactPieces) if (piece != null) piece.SetActive(false);
        fallingFragments.SetActive(true);
        debris.SetActive(true);
        permanentBlocker.enabled = true;
        if (dust != null) dust.Play();
        NoiseManager.Instance?.Emit(new NoiseEvent(transform.position, 10f, 25f, NoiseType.Custom)); // 붕괴 순간 큰 소리 (Range는 실측 거리+벽감쇠 감안해 25로 확장)
        ReleaseFragments();
        StartCoroutine(ShakeCamera());
    }

    void ReleaseFragments()
    {
        foreach (Collider part in fallingFragments.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(part, permanentBlocker, true);
        foreach (Rigidbody body in fallingFragments.GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = false;
            body.AddForce(new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.2f, 0.35f),
                Random.Range(0.8f, 1.5f)), ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 2.5f, ForceMode.Impulse);
        }
    }

    IEnumerator ShakeCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) yield break;
        Vector3 origin = camera.transform.localPosition;
        for (float time = 0f; time < 0.45f; time += Time.deltaTime)
        {
            camera.transform.localPosition = origin + Random.insideUnitSphere * 0.025f;
            yield return null;
        }
        camera.transform.localPosition = origin;
    }
}

#if UNITY_EDITOR
public static class MainEntranceCollapseCheck
{
    [UnityEditor.MenuItem("Tools/Lab Blockout/Verify Main Entrance Collapse")]
    static void Verify()
    {
        MainEntranceCollapse collapse = Object.FindFirstObjectByType<MainEntranceCollapse>(
            FindObjectsInactive.Include);
        if (collapse == null || !collapse.IsConfigured)
            throw new System.Exception("Main entrance collapse is not configured.");
        Renderer door = collapse.intactPieces[0].GetComponentInChildren<Renderer>();
        BoxCollider blocker = collapse.permanentBlocker as BoxCollider;
        float blockerWidth = blocker == null ? 0f :
            blocker.size.x * Mathf.Abs(blocker.transform.lossyScale.x);
        int fragmentCount = collapse.fallingFragments.GetComponentsInChildren<Rigidbody>(true).Length;
        int debrisCount = collapse.debris.GetComponentsInChildren<Renderer>(true).Length;
        if (door == null || blockerWidth < door.bounds.size.x * 0.9f ||
            fragmentCount < 20 || debrisCount < 20)
            throw new System.Exception("The rubble blocker does not cover the entrance.");
        Debug.Log("PASS: wall and door fragments are configured and stone rubble blocks the entrance.");
    }
}
#endif
