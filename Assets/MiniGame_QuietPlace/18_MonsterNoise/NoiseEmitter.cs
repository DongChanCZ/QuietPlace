using UnityEngine;

// 발소리, 문, 미니게임 실수 등 소음이 발생하는 지점에 붙여서 사용
public class NoiseEmitter : MonoBehaviour
{
    public MonsterNoiseReactor reactor;

    [Tooltip("1=걷기, 2=문 열기, 3=물건 던짐, 4=(미정), 5=무겁고 파괴적인 소리")]
    [Range(1, 5)]
    public int noiseTier = 2;

    public void Emit()
    {
        if (reactor != null) reactor.ReportNoise(noiseTier, transform.position);
    }
}
