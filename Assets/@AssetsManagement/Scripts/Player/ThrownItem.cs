using UnityEngine;

// 퀵슬롯에서 던진 오브젝트에 붙는다. 착지(첫 충돌) 지점에 링 이펙트를 띄우고 소리를 낸다 - 몬스터를
// 유인하는 미끼 용도라 loudNoiseCommitThreshold(8)를 확실히 넘기는 세기로 낸다.
public sealed class ThrownItem : MonoBehaviour
{
    const float ImpactNoiseLevel = 10f;

    GameObject landingRingPrefab;
    AudioClip impactClip;
    PlayerInteractor owner;
    float impactNoiseRange = 8f; // Init 이 아이템별 값을 안 넣어주면 이 기본값을 쓴다
    bool landed;

    public void Init(GameObject landingRingPrefab)
    {
        this.landingRingPrefab = landingRingPrefab;
    }

    /// <summary>
    /// 착지 소리는 던진 사람(PlayerInteractor)이 울려준다.
    ///
    /// 이 오브젝트는 닿는 순간 파괴되므로 자기 AudioSource 로는 소리를 끝까지 낼 수 없다.
    /// PlayClipAtPoint 로도 되지만 그건 클립을 끝까지 재생해서 자를 수 없다 - 벽돌 소리가
    /// 14초라 착지 후에도 한참 울린다. 그래서 앞부분만 잘라 쓸 수 있는 쪽에 맡긴다.
    ///
    /// noiseRange 는 아이템별로 다르다(유리병 깨지는 소리가 벽돌보다 멀리까지 들려야
    /// 한다) - PlayerInteractor.ImpactNoiseRangeFor 가 종류별 값을 정해 넘겨준다.
    /// </summary>
    public void Init(GameObject landingRingPrefab, PlayerInteractor owner, AudioClip impactClip, float noiseRange)
    {
        this.landingRingPrefab = landingRingPrefab;
        this.owner = owner;
        this.impactClip = impactClip;
        this.impactNoiseRange = noiseRange;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (landed) return;
        landed = true;

        ContactPoint contact = collision.GetContact(0);

        if (landingRingPrefab != null)
        {
            GameObject ring = Instantiate(landingRingPrefab, contact.point, Quaternion.identity);
            ring.GetComponent<WorldRingEffect>().Init(contact.point, contact.normal);
        }

        // 소리는 떨어진 자리에서 나야 한다. 미끼로 쓰는 물건이라 플레이어가
        // "저쪽에서 소리가 났다" 를 귀로 확인할 수 있어야 한다.
        if (owner != null) owner.PlayThrowImpact(impactClip, contact.point);

        NoiseManager.Instance?.Emit(new NoiseEvent(contact.point, ImpactNoiseLevel, impactNoiseRange, NoiseType.ObjectImpact));

        Destroy(gameObject);
    }
}
