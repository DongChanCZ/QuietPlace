using UnityEngine;

/// <summary>
/// "이 물건은 만질 수 있다"를 알려주는 반짝임.
///
/// 두 가지를 같이 쓴다.
///  - 오브젝트 자체가 은근히 발광하며 맥박처럼 밝아졌다 어두워진다(URP Lit 의 _EmissionColor).
///  - 위에 작은 반짝이 하나가 카메라를 보고 떠서 깜빡인다.
///
/// Update 를 스스로 돌지 않는다. 맵에 수십 개가 붙을 물건이라 각자 거리 계산을 하면 낭비이고,
/// 무엇보다 플레이어를 찾으려고 Find 를 써야 한다(CLAUDE.md 금지). InteractableGlowManager 가
/// 플레이어 참조를 하나 들고 등록된 것들을 한 번에 돌려준다.
/// </summary>
[DisallowMultipleComponent]
public class InteractableGlow : MonoBehaviour
{
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("발광시킬 렌더러 - 비우면 자신과 자식에서 모아온다")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("색과 세기")]
    [SerializeField] private Color glowColor = new Color(0.45f, 0.8f, 1f);
    [SerializeField] private float intensity = 1.4f;

    [Header("맥박 - 초당 몇 번 밝아지는가")]
    [SerializeField] private float pulseSpeed = 1.1f;

    [Header("가장 어두울 때의 비율")]
    [Range(0f, 1f)]
    [SerializeField] private float minPulse = 0.3f;

    [Header("이 거리 안에서만 보인다 (0 이면 항상)")]
    [SerializeField] private float visibleDistance = 12f;
    [SerializeField] private float fadeMargin = 3f;

    [Header("위에 뜨는 반짝이")]
    [SerializeField] private bool showSparkle = true;
    [SerializeField] private Vector3 sparkleOffset = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private float sparkleSize = 0.11f;

    /// 상호작용이 끝난 물건은 더 이상 반짝이지 않아야 한다.
    public bool Consumed { get; private set; }

    private Material[] _materials;      // 인스턴스 사본. 공유 머티리얼을 건드리면 다른 오브젝트까지 빛난다
    private Transform _sparkle;
    private Material _sparkleMaterial;
    private float _phase;

    void OnEnable()
    {
        if (_materials == null) Prepare();
        InteractableGlowManager.Register(this);
    }

    void OnDisable()
    {
        InteractableGlowManager.Unregister(this);
        Apply(0f, 0f);
    }

    void OnDestroy()
    {
        if (_sparkleMaterial != null) Destroy(_sparkleMaterial);
        if (_materials == null) return;
        for (int i = 0; i < _materials.Length; i++)
            if (_materials[i] != null) Destroy(_materials[i]);
    }

    private void Prepare()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        _materials = new Material[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null) continue;

            // r.material 은 이 렌더러만의 사본을 만든다. 이게 없으면 같은 머티리얼을 쓰는
            // 맵 전체가 같이 빛난다.
            Material m = r.material;
            if (m == null || !m.HasProperty(EmissionColorId)) continue;

            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _materials[i] = m;
        }

        _phase = Random.Range(0f, 10f);   // 전부 같은 박자로 깜빡이면 인공적으로 보인다
    }

    /// <summary>매니저가 프레임마다 부른다.</summary>
    public void Tick(Vector3 playerPosition, Transform eyes, float time)
    {
        if (Consumed) { Apply(0f, 0f); return; }

        float visibility = 1f;
        if (visibleDistance > 0f)
        {
            float d = Vector3.Distance(transform.position, playerPosition);
            visibility = 1f - Mathf.InverseLerp(visibleDistance, visibleDistance + fadeMargin, d);
        }

        float pulse = minPulse + (1f - minPulse) * (0.5f + 0.5f * Mathf.Sin((time + _phase) * pulseSpeed * Mathf.PI * 2f));
        Apply(visibility, pulse);

        if (_sparkle == null || eyes == null) return;

        // 항상 카메라를 마주보게 세운다
        _sparkle.rotation = Quaternion.LookRotation(_sparkle.position - eyes.position, eyes.up);
        float s = sparkleSize * (0.7f + 0.6f * pulse) * visibility;
        _sparkle.localScale = new Vector3(s, s, s);
    }

    private void Apply(float visibility, float pulse)
    {
        if (_materials != null)
        {
            Color e = glowColor * (intensity * pulse * visibility);
            for (int i = 0; i < _materials.Length; i++)
                if (_materials[i] != null) _materials[i].SetColor(EmissionColorId, e);
        }

        if (_sparkleMaterial != null)
        {
            Color c = glowColor;
            c.a = 1f;
            _sparkleMaterial.SetColor("_BaseColor", c * (pulse * visibility));
        }
    }

    /// <summary>줍거나 다 쓴 물건은 이걸 불러서 반짝임을 끈다.</summary>
    public void Consume()
    {
        Consumed = true;
        Apply(0f, 0f);
        if (_sparkle != null) _sparkle.gameObject.SetActive(false);
    }

    /// <summary>다시 집을 수 있게 된 물건은 반짝임을 되살린다(내려놓았을 때 등).</summary>
    public void Restore()
    {
        Consumed = false;
        if (_sparkle != null) _sparkle.gameObject.SetActive(true);
    }

    /// <summary>매니저가 공용 머티리얼/메시를 넘겨주며 반짝이를 달아준다.</summary>
    public void AttachSparkle(Material sharedSparkleMaterial, Mesh quad)
    {
        if (!showSparkle || _sparkle != null || sharedSparkleMaterial == null || quad == null) return;

        GameObject go = new GameObject("Sparkle");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = sparkleOffset;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = quad;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        _sparkleMaterial = new Material(sharedSparkleMaterial);
        mr.material = _sparkleMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        _sparkle = go.transform;
    }
}
