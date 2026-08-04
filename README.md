# 쏙캡처 (SsokCapture)

윈도우용 화면 캡처 + 주석 도구. Snagit을 대신할 개인 도구로 만들었어요.
설치 없이 `쏙캡처.exe` 하나로 돌아가요 (.NET Framework는 윈도우 10/11 내장).

![버전](https://img.shields.io/badge/version-1.1.0-2D6EF0)

## 기능

- **캡처** : PrtScn 전역 단축키, 영역/전체 화면, 트레이 상주, 윈도우 시작 시 자동 실행
- **도구** : 자르기 · 선택 · 박스 · 숫자 박스 · 원 · 화살표(열린/채움) · 흐름 박스(자동 연결 순서도) · 텍스트 · 박스 글자 · 말풍선 · 모자이크 · 돋보기
- **스타일** : Adobe Color 17색 + 아웃라인 스와치, Pretendard 글꼴, 배경 프레임(여백+둥근 모서리+그림자)
- **편집** : 인라인 글자 입력, 선택 후 이동/크기 조절, Shift 정비율, Ctrl+휠 화면 확대 축소
- **내보내기** : 클립보드 복사(Ctrl+C), PNG/JPG 저장(Ctrl+S)

자세한 사용법은 [사용법.md](사용법.md), 버전 이력은 [변경기록.md](변경기록.md)를 보세요.

## 빌드

소스는 `src/SsokCapture.cs` 한 파일이에요. `빌드.bat`을 실행하면 윈도우 내장 C# 컴파일러(csc.exe)로 빌드돼요. 별도 설치가 필요 없어요.

```
빌드.bat
```

## 알아둘 것

- 서명되지 않은 개인 제작 프로그램이라 SmartScreen 경고가 뜰 수 있어요. "추가 정보 > 실행"으로 진행하면 돼요.
- Smart App Control이 켜진 PC에서는 실행이 막힐 수 있어요. `빌드.bat`으로 다시 빌드하면 대체로 풀려요. 자세한 내용은 사용법.md 참고.

## 크레딧

- 아이콘 : [Phosphor Icons](https://phosphoricons.com) (MIT)
- 글꼴 : [Pretendard](https://github.com/orioncactus/pretendard) (없으면 맑은 고딕으로 대체)
- 만듦 : 소울곰 + Claude (Claude Code)
