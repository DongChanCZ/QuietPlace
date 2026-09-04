using UnityEngine;

/// <summary>
/// 플레이어가 트리거 범위에 들어오면 SoundManager 에 등록된 효과음을 재생한다.
/// 입구에 다가가면 건물이 무너지는 소리처럼 "그 지점에 도달하면 소리가 난다" 연출용이다.
///
/// 통과한 뒤에 무너지는 연출이라면 이 스크립트가 아니라
/// HoldToSqueezeEntrance 의 collapseSound 칸을 쓰면 된다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlaySfxOnPlayerEnter : MonoBehaviour
{
    [Header("SoundManager 에 등록한 클립 이름 (오디오 파일 이름과 같다)")]
    [SerializeField] private string soundName = "collapse";

    [Header("체크하면 최초 1회만 울린다")]
    [SerializeField] private bool playOnce = true;

    private bool played;

    void Awake()
    {
        // 콜라이더가 트리거가 아니면 플레이어를 벽처럼 막아버리므로 강제로 켠다.
        // RequireComponent 가 붙어 있어도 나중에 사람이 제거할 수 있으니 확인한다.
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        else Debug.LogWarning($"[{nameof(PlaySfxOnPlayerEnter)}] Collider 가 없어 트리거로 동작하지 않는다.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (playOnce && played) return;

        // 이 프로젝트의 다른 트리거들(GlassShard, GameStartTrigger 등)과 같은 방식으로 판별한다
        if (!other.CompareTag("Player")) return;

        if (SoundManager.Instance == null)
        {
            Debug.LogWarning(
                $"[{nameof(PlaySfxOnPlayerEnter)}] SoundManager 가 없어 '{soundName}' 을 재생하지 못했다. " +
                "SoundManager 는 MainMenu 씬에만 있으므로, GameScene 부터 바로 실행하면 존재하지 않는다. " +
                "이 씬에도 SoundManager 오브젝트를 하나 두면 해결된다.", this);
            return;
        }

        played = true;
        SoundManager.Instance.PlaySFX(soundName);
    }

    void OnDrawGizmosSelected()
    {
        // 트리거 범위를 씬 뷰에서 눈으로 확인할 수 있게 표시한다
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
