# QuietPlace 저장소 LFS 용량 초과 — 원인과 정리 계획

측정일 2026-09-03 · 저장소 `DongChanCZ/QuietPlace` · 측정 위치 `C:\Users\MBC-501-14\Documents\QuietPlace`

## 1. 원인

**파일 크기 제한이 아니라 Git LFS 용량 초과입니다.**

| 항목 | 값 |
|---|---|
| 저장소 LFS 총량 | **4,728 MB** (LFS 파일 1,696개) |
| GitHub 무료 한도 | 저장 1,024 MB / 월 전송 1,024 MB |
| 초과 배율 | **4.6배** |

팀원이 clone·pull 할 때 나오는 전형적인 메시지:

```
batch response: This repository is over its data quota.
Account responsible for LFS bandwidth should purchase more data packs
to restore access.
```

`.gitattributes` 가 `*.fbx`, `*.blend`, `*.png`, `*.zip` 등을 전부 LFS로 잡고 있어서,
큰 바이너리는 전부 LFS 용량으로 들어갑니다. 100 MB 파일 제한에는 걸리지 않습니다
(LFS는 파일당 2 GB까지 허용). 순수하게 총량 문제입니다.

## 2. 무엇이 용량을 먹고 있나

씬·프리팹·머티리얼의 GUID를 전수 대조해 "실제로 참조되는가"를 확인했습니다.

| 폴더 | LFS 용량 | 씬이 참조하는 에셋 | 판정 |
|---|---:|---:|---|
| `Assets/Abandoned_Asylum` | 1,920 MB | 0 / 1,011 | 삭제 가능 |
| `Share` | 789 MB | — | 삭제 (유니티 에셋 아님) |
| `Assets/SciFi Warehouse Kit` | 497 MB | 사용 중 | 유지 (텍스처 466 MB) |
| `Assets/_Legacy_Backup` | 360 MB | 0 / 164 | 삭제 가능 |
| `Assets/@Model` | 335 MB | 사용 중 | 유지 (팀원 캐릭터) |
| `Assets/LabBlockout` | 263 MB | 사용 중 | 유지 (맵·오브젝트) |
| `Assets/ScifiOfficeLite` | 244 MB | 0 / 300 | 삭제 가능 |
| `Assets/MossyRocks` | 138 MB | 0 / 20 | 삭제 가능 |
| `Assets/Fbx` | 61 MB | 2 / 6 | 일부 중복 |
| `Assets/@Animation` | 57 MB | 사용 중 | 유지 |
| `Assets/Screenshots` | 43 MB | — | 유지 (작음) |
| `Assets/MASH Virtual` | 14 MB | 0 / 59 | 삭제 가능 |
| `Assets/Urban American Assets` | 5 MB | 0 / 12 | 삭제 가능 |

가장 큰 단일 파일 5개:

| 크기 | 파일 |
|---:|---|
| 299 MB | `Share/LabHorror_Kakao_20260901_v01.zip` |
| 276 MB | `Share/LabHorror_Kakao_20260901_v02.zip` |
| 131 MB | `Assets/_Legacy_Backup/.../BlueprintV3Structure.blend` |
| 95 MB | `Assets/_Legacy_Backup/.../BlueprintV3Structure.fbx` |
| 62 MB | `Assets/_Legacy_Backup/.../Grungy_Industrial_Door.fbx` |

`Share/` 의 zip 두 개는 카카오톡 공유용으로 만든 압축파일입니다. 유니티 에셋이
아니고 저장소에 있을 이유가 없는데 575 MB — 무료 한도의 절반을 혼자 먹고 있습니다.

## 3. 주의 — 그냥 지우면 용량이 안 줄어듭니다

두 가지를 구분해야 합니다.

1. **새 커밋으로 파일을 삭제** → 작업 트리에서만 사라집니다. LFS 사용량 변화 없음.
2. **히스토리를 다시 써서 force push** → 포인터는 사라지지만, GitHub에 남은
   LFS 객체는 자동으로 지워지지 않습니다. 사용량이 그대로일 수 있습니다.

GitHub에서 LFS 사용량을 확실히 되돌리는 방법은 사실상 셋뿐입니다.

- **저장소를 새로 만들어 정리된 내용만 push** (확실, 무료)
- GitHub 지원팀에 미참조 LFS 객체 삭제 요청 (무료, 시간 소요, 보장 없음)
- **데이터 팩 구매** — 월 $5 에 50 GB (즉시, 정리 불필요)

## 4. 정리했을 때의 예상치

참조 0인 것만 다 빼면:

```
Share                       789 MB
Assets/Abandoned_Asylum   1,920 MB
Assets/ScifiOfficeLite      244 MB
Assets/MossyRocks           138 MB
Assets/_Legacy_Backup       360 MB
Assets/MASH Virtual          14 MB
Assets/Urban American         5 MB
                          ---------
                          3,470 MB 감소
```

4,728 − 3,470 = **약 1,258 MB**. 무료 한도 1,024 MB를 아직 234 MB 넘습니다.

여기서 더 줄이려면:

| 추가 대상 | 절감 | 비고 |
|---|---:|---|
| `Assets/@Model/LabHorror_v01/` | 161 MB | LabHorror 사본이 팀원 폴더에 중복 — 팀원 확인 필요 |
| `Assets/Fbx/Wooden Office Desk.fbx` | 39 MB | `LabBlockout/Models/Environment/` 에 같은 파일 존재 |
| `SciFi Warehouse Kit/Demo/Audio` | 16 MB | 데모용 |

셋 다 빼면 약 **1,042 MB** — 여전히 아슬아슬합니다.
키트 텍스처(`SciFi Warehouse Kit/Art/Textures` 466 MB, 84장)를 4K→2K로 줄이면
넉넉히 들어가지만 별도 작업입니다.

**솔직한 결론:** 정리만으로 무료 한도에 맞추는 건 빠듯합니다.
정리(3.5 GB 감소) + 월 $5 데이터 팩 조합이 가장 현실적입니다.

## 5. 재발 방지

정리하든 데이터 팩을 사든, 다음 두 가지는 반드시 넣어야 같은 일이 안 생깁니다.

`.gitignore` 에 추가:

```
# 공유용 압축본 — 저장소에 들어갈 물건이 아님
/Share/

# 옛 백업 — 히스토리가 대신함
/Assets/_Legacy_Backup/
```

`.gitattributes` 는 지금 `*.zip` 까지 LFS로 잡고 있습니다. 압축파일을 저장소에
올릴 일이 없다면 그 줄은 빼는 편이 안전합니다.

## 6. 실행 방법

`Tools/quietplace_lfs_cleanup.sh` 가 **정리된 사본을 새 폴더에 만들어 줍니다.**
기존 저장소와 원격은 건드리지 않습니다. 팀원 합의 후 팀장이 새 저장소를 만들어
push 하면 됩니다.

스크립트는 아무것도 삭제하지 않습니다. 읽고 복사만 합니다.
