using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// 건물 전원.
///
/// 발전기실(변압기 모듈)과 배전반실(퓨즈) 두 곳을 다 고쳐야 불이 들어온다. 어느 한쪽만
/// 끝났을 때 켜지면 "고쳤는데 전기가 안 들어와서 배전반을 보러 간다"는 흐름이 무너지므로
/// 두 신호를 각각 받아 둘 다 들어왔을 때만 복구한다.
///
/// 맵 프리팹 안의 매니저들이 조명 오브젝트를 직접 참조할 수 없으므로(프리팹 -> 씬 참조 제한)
/// 다른 매니저들과 같은 싱글톤으로 둔다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class BuildingPower : MonoBehaviour
{
    public static BuildingPower Instance { get; private set; }

    [Header("씬의 모든 조명을 자동으로 모아 쓸지")]
    [Tooltip("켜면 시작할 때 건물 조명을 전부 끄고, 전원이 들어올 때 한꺼번에 켠다.")]
    [SerializeField] private bool autoCollectSceneLights = true;

    [Header("자동 수집에서 제외할 조명 (태양광, 손전등 등)")]
    [SerializeField] private Light[] ignoredLights;

    [Header("자동 수집에서 제외할 이름 조각")]
    [SerializeField] private string[] ignoredNameParts = { "Directional", "Flashlight", "Player" };

    [Header("전원이 들어오면 켤 조명 (직접 지정 - 자동 수집과 합쳐진다)")]
    [SerializeField] private Light[] lightsToTurnOn;

    [Header("전원이 들어오면 활성화할 오브젝트")]
    [SerializeField] private GameObject[] objectsToActivate;

    [Header("전원이 들어오면 끌 오브젝트 (비상 경고등 등)")]
    [SerializeField] private GameObject[] objectsToDeactivate;

    [Header("전원이 들어오면 초록으로 바뀔 표시등")]
    [SerializeField] private IndicatorLed[] indicators;

    [Header("차단기 레버 - 화면 미니게임에서 올린 것을 실물에도 반영한다")]
    [SerializeField] private BreakerLever breaker;

    [Header("조명이 하나씩 켜지는 간격(초) - 0이면 동시에")]
    [SerializeField] private float lightStagger = 0.06f;

    [Header("환경광")]
    [SerializeField] private bool raiseAmbient = true;
    [SerializeField] private Color poweredAmbient = new Color(0.30f, 0.31f, 0.34f);
    [SerializeField] private float poweredAmbientIntensity = 1.1f;
    [SerializeField] private float fadeSeconds = 1.4f;

    [Header("사운드 - SoundManager 에 등록된 이름 (비우면 재생 안 함)")]
    [SerializeField] private string powerOnSoundName = "";

    [Header("전원 복구 시 추가로 할 일")]
    public UnityEvent onPowerRestored;

    public bool Powered { get; private set; }
    public bool TransformerFixed { get; private set; }
    public bool FuseInstalled { get; private set; }

    private readonly List<Light> _managed = new List<Light>();
    private readonly List<float> _managedIntensity = new List<float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CollectLights();
        SetLightsEnabled(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// 관리할 조명 목록을 만든다. 씬을 훑는 건 Awake 한 번뿐이라 런타임 비용이 없다.
    private void CollectLights()
    {
        if (lightsToTurnOn != null)
            foreach (Light l in lightsToTurnOn) Add(l);

        if (!autoCollectSceneLights) return;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;   // 달빛/태양광은 전원과 무관하다
            if (IsIgnored(l)) continue;
            Add(l);
        }
    }

    private void Add(Light l)
    {
        if (l == null || _managed.Contains(l)) return;
        _managed.Add(l);
        _managedIntensity.Add(l.intensity);
    }

    private bool IsIgnored(Light l)
    {
        if (ignoredLights != null)
            foreach (Light x in ignoredLights) if (x == l) return true;

        if (ignoredNameParts == null) return false;

        // 손전등처럼 플레이어에 붙은 조명은 전원과 상관없이 켜져 있어야 한다.
        // 이름이 PLAYER_FLASHLIGHT 처럼 대문자일 수 있으므로 대소문자를 무시한다.
        string self = l.name.ToLowerInvariant();
        string root = l.transform.root != null ? l.transform.root.name.ToLowerInvariant() : "";

        foreach (string part in ignoredNameParts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            string p = part.ToLowerInvariant();
            if (self.Contains(p)) return true;
            if (root.Contains(p)) return true;
        }
        return false;
    }

    private void SetLightsEnabled(bool on)
    {
        for (int i = 0; i < _managed.Count; i++)
        {
            Light l = _managed[i];
            if (l == null) continue;
            l.enabled = on;
            if (on) l.intensity = _managedIntensity[i];
        }
    }

    /// <summary>발전기실 변압기 수리가 끝났을 때.</summary>
    public void MarkTransformerFixed()
    {
        TransformerFixed = true;
        TryRestore();
    }

    /// <summary>배전반실 퓨즈 교체가 끝났을 때.</summary>
    public void MarkFuseInstalled()
    {
        FuseInstalled = true;
        TryRestore();
    }

    /// <summary>둘 다 끝났으면 전원을 넣는다.</summary>
    public void TryRestore()
    {
        if (Powered || !TransformerFixed || !FuseInstalled) return;
        Restore();
    }

    /// <summary>조건을 무시하고 전원을 넣는다(디버그/컷신용).</summary>
    public void Restore()
    {
        if (Powered) return;
        Powered = true;

        if (objectsToActivate != null)
            foreach (GameObject go in objectsToActivate)
                if (go != null) go.SetActive(true);

        if (objectsToDeactivate != null)
            foreach (GameObject go in objectsToDeactivate)
                if (go != null) go.SetActive(false);

        if (indicators != null)
            foreach (IndicatorLed led in indicators)
                if (led != null) led.TurnOn();

        if (breaker != null) breaker.ForceOn();

        if (!string.IsNullOrEmpty(powerOnSoundName) && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(powerOnSoundName);

        StartCoroutine(TurnLightsOn());
        if (raiseAmbient) StartCoroutine(FadeAmbient());

        onPowerRestored?.Invoke();
    }

    // 전부 같은 프레임에 켜지면 스위치 하나 누른 것처럼 심심하다.
    // 조금씩 시차를 두면 형광등이 차례로 들어오는 느낌이 난다.
    private IEnumerator TurnLightsOn()
    {
        for (int i = 0; i < _managed.Count; i++)
        {
            Light l = _managed[i];
            if (l == null) continue;

            l.gameObject.SetActive(true);
            l.enabled = true;
            l.intensity = _managedIntensity[i];

            if (lightStagger > 0f) yield return new WaitForSeconds(lightStagger);
        }
    }

    // 씬의 환경광 모드에 따라 만질 값이 다르다. Skybox 모드에서 ambientLight 를 바꿔도
    // 아무 일도 일어나지 않으므로 그때는 세기(ambientIntensity)를 올린다.
    private IEnumerator FadeAmbient()
    {
        bool flat = RenderSettings.ambientMode == AmbientMode.Flat;

        Color fromColor = RenderSettings.ambientLight;
        float fromIntensity = RenderSettings.ambientIntensity;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            if (flat) RenderSettings.ambientLight = Color.Lerp(fromColor, poweredAmbient, e);
            else RenderSettings.ambientIntensity = Mathf.Lerp(fromIntensity, poweredAmbientIntensity, e);

            yield return null;
        }
    }
}
