# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: 최소 공용 기반 — IRandom·SystemRandom·GameLog·UniTask 미관측 예외 등록
- BACKLOG 작업 ID: T1
- 기획서 근거: 해당 없음 (AI_RULES 2·3·5장, BACKLOG 0절 Q1·Q2)
- 작성자: Claude
- 작성 일시: 2026-11-25
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `7533420`
- 커밋 상태: 현민 커밋 전
- 상태: 🎮 현민 씬 테스트 대기

## 1. 목표
보스 기믹(T2~) 새 코드가 AI_RULES를 지키는 데 필요한 최소 기반을 만든다: Core용 난수 인터페이스(시드 고정 재현), 빌드에서 빠지는 공용 로그, `.Forget()` 예외 수집.

## 2. 변경 파일
| 파일 | 계층 | 변경 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/Core/IRandom.cs` | Core | 신규. `Next(int,int)`, `Range(float,float)`, `Value01` |
| `Assets/LHM/0_Script/Boss/New/Core/SystemRandom.cs` | Core | 신규. `System.Random` 래퍼, 시드 생성자 / 시간 시드 생성자, 잘못된 범위는 `ArgumentOutOfRangeException` |
| `Assets/LHM/0_Script/Boss/New/Gimmick/GameLog.cs` | Runtime | 신규. `Info/Warn/Error/LogException` (`[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`), `[RuntimeInitializeOnLoadMethod]`로 `UniTaskScheduler.UnobservedTaskException` 등록 |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T1 상태 🎮 |

## 3. 동작 변화와 리스크
- 동작 변화: Play 시작 시 Console에 `[GameLog] 초기화: UniTask 미관측 예외 → GameLog 등록` 1회. 기존 코드 동작 변화 없음 (아무도 아직 사용하지 않음).
- 리스크:
  - BACKLOG 예상 파일의 `UniTaskExceptionBootstrap.cs`는 만들지 않고 GameLog 안 정적 초기화로 합침 → 씬 배치 불필요(에디터 작업 0). 파일 4 → 3.
  - 릴리스 빌드에서는 예외 핸들러를 **등록하지 않음**(`#if UNITY_EDITOR || DEVELOPMENT_BUILD`). 등록하면 GameLog 호출이 제거돼 예외가 조용히 사라지기 때문. 릴리스는 UniTask 기본 동작(Debug.LogException) 유지.
  - asmdef 없음 → Assembly-CSharp에 포함. UniTask 참조는 기존 `BossContext.cs`가 이미 `Cysharp.Threading.Tasks`를 쓰므로 자동 참조되는 것으로 판단.
  - 다른 스크립트가 이미 `UniTaskScheduler.UnobservedTaskException` 을 구독하면 둘 다 호출됨 (Assets 검색 결과 구독처 없음).

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 (API·using·asmdef) | 문제 없음으로 판단. `UniTaskScheduler.UnobservedTaskException`(event `Action<Exception>`), `RuntimeInitializeLoadType.SubsystemRegistration`(2019.2+) 사용. `Debug`/`Object` 는 별칭으로 System 쪽과 모호성 제거. Conditional 메서드는 모두 void. 릴리스 빌드에선 `using Cysharp.Threading.Tasks` 미사용 경고 가능(에러 아님) |
| unity-guard | 문제 없음. Core 2개는 `UnityEngine` 참조 0 (`Random` = `System.Random`), async void·코루틴·Find·Time 없음 |
| spec-check | 해당 없음 (기획서 수치 없음) |

## 5. 에디터 작업 (현민)
없음. Unity를 열면 `Core/`, `Gimmick/` 폴더와 `.cs` 의 `.meta` 가 자동 생성된다.

## 5-1. 씬 테스트 절차 (현민)
| # | 조작 | 기대 결과 | 결과 (현민) |
|---|---|---|---|
| 1 | Unity를 열고 컴파일 완료까지 대기 | Console에 컴파일 에러 0 | ⬜ |
| 2 | 아무 씬(예: `LHM.unity`) Play | Console에 `[GameLog] 초기화: UniTask 미관측 예외 → GameLog 등록` **1회** | ⬜ |
| 3 | Play 정지 → 다시 Play | 다시 1회만 (중복 2줄 아님) | ⬜ |

- 경계 상황: Enter Play Mode Options에서 Domain Reload를 꺼둔 경우에도 3번이 1줄인지 (중복 구독 방지 확인)
- 디버그 표시: 해당 없음 (IRandom은 T9·T17에서 Gizmo/팝업으로 재현성 확인)
- Console에 나오면 안 되는 것: `CS0246`(UniTask 못 찾음), `CS0104`(Debug/Object 모호), 같은 초기화 로그 2줄

## 5-2. 현민 씬 테스트 결과
- 결과: 대기

## 6. 기획서와 다르게 한 점
| 기획서 / BACKLOG | 구현 | 이유 |
|---|---|---|
| BACKLOG T1 예상 파일 `Gimmick/UniTaskExceptionBootstrap.cs` | GameLog 내부 정적 초기화로 합침 | 씬 배치 없이 동작, 파일 수 감소 |

## 7. 결정 사항
- 네임스페이스: 공용 Core 타입 = `UnMachine.Core` (IRandom·SystemRandom), 공용 로그 = `UnMachine` (GameLog). 보스 전용 코드는 Q1대로 `UnMachine.Core.Boss` / `UnMachine.Boss` 사용 예정. 공용 기반은 보스 외에서도 쓰므로 `.Boss` 를 붙이지 않음.

## 8. 알려진 문제 / 미완료
- 기존 코드의 `Debug.Log` 는 그대로 (범위 밖).
- GameClock 미도입 (Q3: 어댑터에서 `Time.deltaTime` + guard-allow).

## 9. 리뷰어에게 요청
- 릴리스 빌드에서 핸들러 미등록 판단이 타당한지
- 네임스페이스 결정(7번)

## 10. 질문 (현민 답변 필요)
- 없음 (Q18은 T19 착수 전까지 답변 필요)

## 11. 다음 할 일
1. 현민 씬 테스트 → 커밋
2. 새 세션에서 `/task T2` (BossController 이벤트 훅)

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- 메시지: `[feat] T1 공용 기반: IRandom·SystemRandom·GameLog`
- 포함할 파일: `Assets/LHM/0_Script/Boss/New/Core/IRandom.cs`, `Core/SystemRandom.cs`, `Gimmick/GameLog.cs` + `Core/`·`Gimmick/` 폴더와 각 `.cs` 의 `.meta` (에디터를 연 뒤 생성됨), `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md`
- 나누면 좋은 단위: BACKLOG 승인(⬜→✅) 변경은 `[docs] BACKLOG 전체 승인` 으로 먼저 따로 커밋해도 됨

## 12. 리뷰 대응
(리뷰 반영 시 작성)

## 13. 규칙 변경 제안
- AI_RULES 5장 "UniTask `.Forget()` 예외는 부트스트랩에서 1회 등록"에 "릴리스 빌드에서는 등록하지 않는다(GameLog가 제거되어 예외가 사라짐)"를 덧붙이는 것을 제안.
