# 자리바꾸기 (Seat Shuffler)

교실 학생 자리를 조건에 맞춰 배정해 주는 데스크톱 앱. 명단을 넣고, 분단/행/열과
짝 조건을 정한 뒤 버튼 한 번이면 자리 배치가 끝난다. 확정 기록을 누적해 다음 배정
때 같은 자리·같은 짝을 피한다.

- **Avalonia UI (.NET 8)** — 리눅스에서 개발하고 Windows 실행 파일로 배포
- **단일 exe 배포** — 사용자는 `.NET 설치 없이` `자리바꾸기.exe` 하나만 받아 더블클릭

## 사용자용

[Releases](../../releases)에서 `자리바꾸기.exe`를 내려받아 실행하면 끝. 별도 설치 불필요.

## 기능

세 개의 탭으로 구성된다.

**① 학생 명단**
- 학번 · 이름 · 성별(남/녀) 관리
- 입력: 수동 행 추가, 엑셀(.xlsx)/CSV 업로드, 클립보드 붙여넣기(탭·쉼표 인식)
- 성별 토큰 자동 인식: `남/여/녀/M/F/1/2/male/female` 등 → 인식 실패 시 미지정
- 명단은 자동 저장되어 다음 실행 때 복원

**② 배정**
- 분단·행·열 지정. **열 1 = 단독석(짝 없음), 열 2 이상 = 같은 행 인접 2칸이 짝**
- 짝 조건: 동성짝 / 이성짝 (열 2 이상일 때만)
- 회피 옵션: 이전 확정 기록과 **같은 자리 회피**, **같은 짝 회피**
- `배정`을 누를 때마다 다른 후보 생성 → 마음에 드는 결과에서 `확정`
- 조건을 모두 만족할 수 없으면 자동으로 완화하고 그 사실을 배너로 알림
  (완화 순서: 같은 자리 → 같은 짝 → 성별 짝)
- 성별 색 힌트(남=파랑, 녀=분홍)

**③ 기록**
- 확정된 배치가 시각·라벨·구성과 함께 누적
- 선택하면 좌석 미리보기, 라벨 수정·삭제 가능
- **확정된 전체 기록**이 다음 배정의 회피 기준

> 성별 미지정 학생: 동성짝에서는 누구와도 짝 가능(와일드카드), 이성짝에서는
> 엄격 쌍에 부적격이라 완화를 유발할 수 있다.

### 데이터 저장 위치
명단·기록은 `%APPDATA%\SeatShuffler`(윈도우) / `~/.config/SeatShuffler`(리눅스·맥)에
`roster.json`, `history.json`으로 저장된다.

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

| 영역 | 선택 |
|------|------|
| UI | Avalonia 11.3 (XAML, 크로스플랫폼) + DataGrid |
| MVVM | CommunityToolkit.Mvvm |
| 엑셀 | ClosedXML (.xlsx 읽기) |
| 영속 | System.Text.Json (source-gen) |
| 런타임 | .NET 8 (LTS) |
| 배포 | win-x64 self-contained single-file |

> ⚠️ **트리밍(`PublishTrimmed`)을 켜지 말 것.** ClosedXML(OpenXML)과
> 직렬화가 리플렉션에 의존하므로 트리밍 시 깨진다. 현재 배포 설정은 트리밍 OFF.

## 라이선스

MIT — [LICENSE](LICENSE) 참고.
