# 자리바꾸기 (Seat Shuffler)

교실 학생 자리를 무작위로 섞어주는 데스크톱 앱. 학생 이름을 넣고 버튼 한 번이면 자리 배치가 끝난다.

- **Avalonia UI (.NET 8)** — 리눅스에서 개발하고 Windows 실행 파일로 배포
- **단일 exe 배포** — 사용자는 `.NET 설치 없이` `자리바꾸기.exe` 하나만 받아 더블클릭

## 사용자용

[Releases](../../releases)에서 `자리바꾸기.exe`를 내려받아 실행하면 끝. 별도 설치 불필요.

## 기능

- 학생 이름 입력 (한 줄에 한 명, 쉼표/탭 구분도 인식)
- 열 수 지정 (1~12)
- Fisher–Yates 셔플로 균등 무작위 배치
- 자리 카드 그리드 표시

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
| UI | Avalonia 11.3 (XAML, 크로스플랫폼) |
| MVVM | CommunityToolkit.Mvvm |
| 런타임 | .NET 8 (LTS) |
| 배포 | win-x64 self-contained single-file |

## 라이선스

MIT — [LICENSE](LICENSE) 참고.
