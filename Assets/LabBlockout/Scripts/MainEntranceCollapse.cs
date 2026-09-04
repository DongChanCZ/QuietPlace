using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class MainEntranceCollapse : MonoBehaviour
{
    public GameObject[] intactPieces;
    public GameObject fallingFragments;
    public GameObject debris;
    public Collider permanentBlocker;
    public ParticleSystem dust;

    // Opening the front door is what brings the entrance down. The walk-in
    // trigger stays as a fallback for the real game scene, where the player may
    // never press E on anything.
    [SerializeField] SlidingDoor triggerDoor;

    // 문을 열면 무너지는 것으로 충분하다. 가까이 걸어가기만 해도 무너지면
    // 플레이어가 문을 열어볼 기회 자체가 없다. 실제 게임 씬에서 E 를 쓰지
    // 않는 구성이면 이걸 켜서 걷기 트리거를 되살릴 수 있다.
    [SerializeField] bool collapseOnWalkIn;

    bool triggered;
    NavMeshObstacle blockerObstacle; // permanentBlocker와 같은 오브젝트에 있으면 자동으로 찾아서 같이 켜고 끈다 (몬스터가 잔해에 너무 붙지 않게)
    public bool Triggered => triggered;
    public bool IsConfigured => intactPieces != null && intactPieces.Length > 0 &&
                                fallingFragments != null && debris != null &&
                                permanentBlocker != null;

    void Awake()
    {
        fallingFragments.SetActive(false);
        debris.SetActive(false);
        permanentBlocker.enabled = false;
        blockerObstacle = permanentBlocker.GetComponent<NavMeshObstacle>();
        if (blockerObstacle != null) blockerObstacle.enabled = false;
    }

    void OnEnable()
    {
        if (triggerDoor != null) triggerDoor.Opened += TriggerCollapse;
    }

    void OnDisable()
    {
        if (triggerDoor != null) triggerDoor.Opened -= TriggerCollapse;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!collapseOnWalkIn) return;
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
        if (blockerObstacle != null) blockerObstacle.enabled = true;
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
