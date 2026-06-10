# 자리바꾸기 (Seat Shuffler)

교실 학생 자리를 조건에 맞춰 배정해 주는 데스크톱 앱. 명단을 넣고, 분단/행/열과
짝 조건을 정한 뒤 버튼 한 번이면 자리 배치가 끝난다. 확정 기록을 누적해 다음 배정
때 같은 자리·같은 짝을 피한다.

- **Avalonia UI (.NET 8)** — 리눅스에서 개발하고 Windows 실행 파일로 배포
- **단일 exe 배포** — 사용자는 `.NET 설치 없이` `자리바꾸기.exe` 하나만 받아 더블클릭

## 사용자용

[Releases](../../releases)에서 `자리바꾸기.exe`를 내려받아 실행하면 끝. 별도 설치 불필요.

📖 **자세한 사용법은 [사용 설명서(MANUAL.md)](MANUAL.md)** 를 참고하세요.

## 기능

일곱 개의 탭으로 구성된다.

- **학생 명단** — 학번·이름·성별 관리 (수동 / 엑셀·CSV / 클립보드 붙여넣기, 성별 자동 인식)
- **좌석 설정** — 좌석 *공간* 정의 — 분단·행·열, 빈자리(비움), 남녀 자리 구역
- **제약** — 자리 고정·회피, 짝 거리(가깝게/멀리), 앞자리 등. PIN 잠금, 우선순위 드래그
- **배정** — 동성/이성짝·회피 옵션 → 배정/확정, 수동 교체, 자동 완화 배너
- **기록** — 확정 배치 누적 — 다음 배정의 같은 자리·같은 짝 회피 기준
- **꾸미기** — 스킨 9종·배경/셀 이미지·교탁 기준 → 자리표 **PNG 출력**(인쇄)
- **정보** — 데이터 저장 위치·안내

그 밖에:

- **열 1 = 단독석**, **열 2 이상 = 같은 행 인접 2칸이 짝**.
- 조건이 충돌하면 우선순위가 낮은 제약부터 **자동 완화 + 배너 알림** (배정은 항상 완성).
- 명단·기록·설정은 자동 저장되어 다음 실행 때 복원.

### 데이터 저장 위치
`%APPDATA%\SeatShuffler`(윈도우) / `~/.config/SeatShuffler`(리눅스·맥)에
`roster.json`(명단), `history.json`(기록), `constraints.json`(제약·좌석·꾸미기 설정)으로 저장된다.

> 꾸미기 배경·셀 이미지는 *경로만* 저장된다(원본 파일은 그대로 둘 것). 자세한 내용은 설명서 참고.

## 개발

```bash
# 실행 (리눅스/맥/윈도우 공통)
dotnet run

# Windows 단일 exe 빌드 (self-contained, 런타임 포함)
dotnet publish -c Release -r win-x64
# → bin/Release/net8.0/win-x64/publish/자리바꾸기.exe
```

> Windows 빌드/배포는 **Teacher App Portal** 파이프라인에서 담당한다.
> 위 명령은 로컬 검증용.

## 스택

- **UI** — Avalonia 11.3 (XAML, 크로스플랫폼) + DataGrid
- **MVVM** — CommunityToolkit.Mvvm
- **엑셀** — ClosedXML (.xlsx 읽기)
- **영속** — System.Text.Json (source-gen)
- **런타임** — .NET 8 (LTS)
- **배포** — win-x64 self-contained single-file

> ⚠️ **트리밍(`PublishTrimmed`)을 켜지 말 것.** ClosedXML(OpenXML)과
> 직렬화가 리플렉션에 의존하므로 트리밍 시 깨진다. 현재 배포 설정은 트리밍 OFF.

## 라이선스

MIT — [LICENSE](LICENSE) 참고.
