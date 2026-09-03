#!/usr/bin/env bash
# QuietPlace 저장소에서 참조되지 않는 대용량 에셋을 뺀 사본을 만든다.
#
# 이 스크립트는 아무것도 지우지 않는다. 원본 저장소도 원격도 건드리지 않고,
# 새 폴더에 정리된 사본만 만든다. 실제 push 는 팀원 합의 후 사람이 한다.
#
# 사용법:  bash Tools/quietplace_lfs_cleanup.sh [원본경로] [출력경로]
set -euo pipefail

SRC="${1:-$HOME/Documents/QuietPlace}"
DST="${2:-$HOME/Desktop/QuietPlace_Clean}"

[ -d "$SRC/.git" ] || { echo "원본이 git 저장소가 아님: $SRC"; exit 1; }
[ -e "$DST" ] && { echo "출력 폴더가 이미 있음: $DST"; exit 1; }

# 씬·프리팹·머티리얼 GUID 대조에서 참조 0으로 확인된 것들.
# 근거와 측정치는 Docs/QuietPlace_LFS_Cleanup.md 참고.
DROP=(
  "Share"
  "Assets/Abandoned_Asylum"
  "Assets/ScifiOfficeLite"
  "Assets/MossyRocks"
  "Assets/MASH Virtual"
  "Assets/Urban American Assets"
  "Assets/_Legacy_Backup"
)
# 유니티가 다시 만드는 것들. 저장소에 들어갈 이유가 없다.
DROP+=( "Library" "Temp" "Obj" "Build" "Builds" "Logs" "UserSettings" ".claude" )

echo "원본 : $SRC"
echo "출력 : $DST"
echo
echo "빠지는 항목:"
for d in "${DROP[@]}"; do
  if [ -e "$SRC/$d" ]; then
    printf "  %8s  %s\n" "$(du -sh "$SRC/$d" 2>/dev/null | cut -f1)" "$d"
  fi
done
echo

# 오타 난 경로는 아무 경고 없이 그 폴더를 그대로 남긴다 - 정리하는 의미가
# 사라지므로 여기서 멈춘다. 유니티가 다시 만드는 폴더는 없어도 정상이다.
OPTIONAL=" Library Temp Obj Build Builds Logs UserSettings .claude "
missing=0
for d in "${DROP[@]}"; do
  if [ ! -e "$SRC/$d" ] && [[ "$OPTIONAL" != *" $d "* ]]; then
    echo "제외 대상이 원본에 없음 (경로 오타?): $d"; missing=1
  fi
done
[ "$missing" -eq 0 ] || { echo; echo "중단합니다. 경로를 고치고 다시 실행하세요."; exit 1; }

EX=()
for d in "${DROP[@]}"; do EX+=( --exclude="$d" ); done
# .meta 도 같이 빼야 유니티가 "빠진 폴더의 meta" 경고를 내지 않는다.
for d in "${DROP[@]}"; do EX+=( --exclude="$(basename "$d").meta" ); done

mkdir -p "$DST"
# --exclude=.git : 기존 히스토리를 가져오지 않는다. 옛 LFS 객체가 따라오면
# 정리하는 의미가 없다.
tar -C "$SRC" --exclude=.git "${EX[@]}" -cf - . | tar -C "$DST" -xf -

# Share 와 _Legacy_Backup 이 다시 들어오지 못하게 막는다.
{
  echo ""
  echo "# 공유용 압축본 - 저장소에 들어갈 물건이 아님 (LFS 789MB 잡아먹었음)"
  echo "/Share/"
  echo ""
  echo "# 옛 백업 - git 히스토리가 대신함 (LFS 360MB)"
  echo "/Assets/_Legacy_Backup/"
} >> "$DST/.gitignore"

cd "$DST"
git init -q
git lfs install --local >/dev/null 2>&1 || true
git add -A
git -c user.name="LabHorror" -c user.email="tkddnjs6838@gmail.com" \
    commit -qm "정리된 프로젝트: 참조되지 않는 대용량 에셋 팩과 공유용 압축본 제외"

echo
echo "완료: $DST"
echo "LFS 총량:"
git lfs ls-files -s 2>/dev/null | awk '
  { if (match($0, /\(([0-9.]+) (B|KB|MB|GB)\)/)) {
      s=substr($0, RSTART+1, RLENGTH-2); split(s, a, " "); u=a[2]
      m=(u=="GB"?1024:u=="MB"?1:u=="KB"?1/1024:1/1048576); t+=a[1]*m } }
  END { printf "  %.0f MB  (무료 한도 1024 MB)\n", t }'
echo
echo "다음 단계 - 팀원 합의 후 사람이 직접:"
echo "  1) GitHub 에서 새 저장소를 만든다 (기존 QuietPlace 는 그대로 둔다)"
echo "  2) cd \"$DST\""
echo "  3) git remote add origin <새 저장소 주소>"
echo "  4) git push -u origin master"
echo
echo "기존 저장소를 지우거나 force push 하지 않는다. 팀원 작업이 날아간다."
