using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// 건물 전원. 배전반 퓨즈 미니게임을 통과하면 Restore() 가 불려 불이 들어온다.
///
/// 맵 프리팹 안의 FuseSlot / FuseRepairManager 가 조명 오브젝트를 직접 참조할 수 없으므로
/// (프리팹 -> 씬 참조 제한) 다른 매니저들과 같은 싱글톤으로 둔다.
///
/// 천장등 리그가 아직 맵에 없어도 뭔가 보여야 하므로 환경광을 올리는 경로를 함께 둔다.
/// 나중에 꺼둔 천장등을 만들면 lightsToTurnOn 에 끌어다 넣으면 그만이다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class BuildingPower : MonoBehaviour
{
    public static BuildingPower Instance { get; private set; }

    [Header("전원이 들어오면 켤 조명 (꺼둔 천장등을 여기에 넣는다)")]
    [SerializeField] private Light[] lightsToTurnOn;

    [Header("전원이 들어오면 활성화할 오브젝트")]
    [SerializeField] private GameObject[] objectsToActivate;

    [Header("전원이 들어오면 끌 오브젝트 (비상 경고등 등)")]
    [SerializeField] private GameObject[] objectsToDeactivate;

    [Header("전원이 들어오면 초록으로 바뀔 표시등")]
    [SerializeField] private IndicatorLed[] indicators;

    [Header("차단기 레버 - 화면 미니게임에서 올린 것을 실물에도 반영한다")]
    [SerializeField] private BreakerLever breaker;

    [Header("환경광 - 조명이 하나도 없어도 밝아지는 것이 보이게")]
    [SerializeField] private bool raiseAmbient = true;
    [SerializeField] private Color poweredAmbient = new Color(0.30f, 0.31f, 0.34f); // Flat 모드일 때
    [SerializeField] private float poweredAmbientIntensity = 1.1f;                  // Skybox 모드일 때
    [SerializeField] private float fadeSeconds = 1.4f;

    [Header("사운드 - SoundManager 에 등록된 이름 (비우면 재생 안 함)")]
    [SerializeField] private string powerOnSoundName = "";

    [Header("전원 복구 시 추가로 할 일")]
    public UnityEvent onPowerRestored;

    public bool Powered { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Restore()
    {
        if (Powered) return;
        Powered = true;

        if (lightsToTurnOn != null)
        {
            foreach (Light light in lightsToTurnOn)
            {
                if (light == null) continue;
                light.gameObject.SetActive(true);
                light.enabled = true;
            }
        }

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

        if (raiseAmbient) StartCoroutine(FadeAmbient());

        onPowerRestored?.Invoke();
    }

    // 한 프레임에 확 밝아지면 조명이 켜진 게 아니라 화면이 튄 것처럼 보인다.
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
