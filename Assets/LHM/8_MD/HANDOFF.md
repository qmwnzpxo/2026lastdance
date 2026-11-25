# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: `BossFlowDriver` + BossController 연결 (`useFlowV2`)
- BACKLOG 작업 ID: T31
- 기획서 근거: boss.md 5장, BACKLOG Q19 C
- 작성자: Claude
- 작성 일시: 2026-11-25
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `82d87de`
- 커밋 상태: 착수 (코드 변경 전)
- 상태: 🔨 계획 승인 대기
- 이전 작업: T29·T30 — 커밋됨(`82d87de`). 미추적 `.meta` 3개(`BossDamageScale`·`BossFlowSettings`·`BossFlowConfig`) 남음 → T31 커밋 제안에 포함

> 아래 0장~11-1장은 아직 T29·T30 기록이다 (T31 완료 시 덮어씀).

## 0. 이전 작업 리뷰 범위 (Gemini)
T27·T28·T22 는 기준 커밋 **이전**에 커밋됨 → 이번 diff(`ae589ab..`)에 안 보인다. 따로 리뷰할 때 범위:
- `git diff d60f4f9 ae589ab` — 팀원 커밋 `b3471dc`(HitstopManager `_version`)·`c2d158d`(벽 붙기)가 섞여 있음. T27·T28·T22 파일:
  `New/Core/GameClock.cs`, `ClockChannel.cs`, `New/Gimmick/GameClockDriver.cs`, `CSH/0.Scripts/Hitstop/HitstopManager.cs`,
  `New/Editor/BossTestSceneBuilder.cs`, `New/BossController.cs`, `New/BossHealthBar.cs`
- 리뷰 요청: `HitstopManager` — 팀원 `_version` 판정 + Claude `_endTime`·`RemainingTime`·긴 쪽 유지가 함께 맞는지 /
  Driver 정적 `s_active`·이벤트 해제 순서 / `GameClock` 정적 + `Reset` 의존·부분 프레임 정지 / T22 `PhaseForRatio` 위치·F6 디버그
- `ae589ab` 복구 후 현민 재확인(F2·H 겹침) 결과는 미보고.

## 1. 목표
- T29: 기본 루프 Idle → Telegraph(최소 0.4초) → Attacking(외부 종료 알림까지) → Punishable(패턴 값, 2P ×0.8) → Resting(1P 1.5 / 2P 0.8) → Idle.
- T30: 인터럽트 Stunned·Groggy·PhaseTransition·Dead, 우선순위 Dead > PhaseTransition > Groggy > Stunned > 기본 루프,
  Groggy 중 전환 요청 보류 → Groggy 끝난 직후 PhaseTransition, 상태별 피해 배율, 수치는 `BossFlowConfig` SO.
- 씬 연결은 T31, 전환 무적·연출은 T33·T34, QTE 로 Groggy 조기 종료는 T65.

## 2. 변경 파일
| 파일 | 계층 | 변경 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/Core/BossFlowState.cs` | Core (T29 신규 → T30 확장) | enum Idle·Telegraph·Attacking·Punishable·Resting + **끝에** Stunned·Groggy·PhaseTransition·Dead |
| `Assets/LHM/0_Script/Boss/New/Core/PatternTiming.cs` | Core (T29 신규) | `readonly struct` 예고·반격 틈 (1P 기준) |
| `Assets/LHM/0_Script/Boss/New/Core/PatternResult.cs` | Core (T29 신규) | `readonly struct` `Default` / `WithPunish(seconds)` (결과별 틈 덮어쓰기) |
| `Assets/LHM/0_Script/Boss/New/Core/BossFlowMachine.cs` | Core (T29 신규 → T30 확장) | 생성자 `(BossFlowSettings)`. 기본 루프 `Begin`·`NotifyAttackEnded`(→ `bool`)·`Tick`·`SetPhase`. 인터럽트 `EnterStunned`·`EnterGroggy`·`RequestPhaseTransition`·`EnterDead` (모두 `bool`). 이벤트 `StateChanged(prev, cur)`·`PhaseChanged(int)`. 읽기 `State`·`StateRemaining`·`Phase`·`IsPhaseTransitionPending`·`DamageMultiplier`. `internal static RequireNonNegativeFinite` (보스 Core 공용 검증) |
| `Assets/LHM/0_Script/Boss/New/Core/BossDamageScale.cs` | Core (T30 신규) | `readonly struct` 기본 1.0 / 취약(Punishable·Stunned) 1.2 / Groggy 1.5, `ForState(state)` |
| `Assets/LHM/0_Script/Boss/New/Core/BossFlowSettings.cs` | Core (T30 신규, **BACKLOG 외 추가 — 현민 승인**) | 수치 묶음 `readonly struct` (휴식×2, 최소 예고, 2P 틈 배율, Stunned, Groggy, 전환, 피해 배율) |
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowConfig.cs` | Runtime SO (T30 신규) | `[CreateAssetMenu("UnMachine/Config/Boss Flow")]` 필드 10개 + `ToSettings()` |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T29·T30 → 🎮 |

네임스페이스: Core `UnMachine.Core.Boss`, SO `UnMachine.Boss` (BACKLOG Q1).

## 3. 동작 변화와 리스크
- 동작 변화: 없음. 아직 아무 곳에서도 생성·호출하지 않음 (T31 `BossFlowDriver` 가 사용).
- 상태 규칙 (T30):
  | 요청 | 받아들이는 상태 | 결과 | 끝나면 |
  |---|---|---|---|
  | `EnterDead` | Dead 제외 전부 | Dead, 보류된 전환 취소 | 끝나지 않음. 이후 모든 요청 false, Tick 무동작 |
  | `RequestPhaseTransition` | Dead 제외, **1회만** | Groggy 중이면 보류(`IsPhaseTransitionPending`), 아니면 즉시 PhaseTransition | Phase = 2 → `PhaseChanged(2)` → Idle |
  | `EnterGroggy` | Dead·PhaseTransition·Groggy 제외 | Groggy | 보류 있으면 PhaseTransition, 없으면 Resting |
  | `EnterStunned` | 기본 루프 5개만 | Stunned | Resting |
- 설계 선택:
  - **늦은 공격 종료 알림 (T29에서 미룬 규칙)**: Telegraph·Attacking 중 인터럽트가 걸리면 그 패턴의 `NotifyAttackEnded` 는 무시하고 `false`. 다음 `Begin` 때 초기화. 그 외 순서가 틀린 호출은 여전히 `InvalidOperationException`.
  - PhaseTransition 끝: **페이즈를 먼저 2로** 바꾸고 Idle 진입 → Idle 핸들러에서 `Begin` 하면 2P 값 적용.
  - 0초 상태 건너뛰기 (T29 승인 규칙)를 새 상태에도 적용. 전환 0초여도 `PhaseChanged(2)` 는 발생 (상태 이벤트만 없음).
  - `DamageMultiplier` 는 PhaseTransition·Dead 에서도 1.0 — **무적은 이 클래스가 판단하지 않음** (T33 `ComputeInvulnerable`).
  - `SetPhase` 는 남겨 둠 (디버그·토글 Off 동기화). `SetPhase(2)` 를 해도 `RequestPhaseTransition` 1회 제한과는 별개.
  - 수치 생성자 인자가 8개로 늘어 `BossFlowSettings` struct 로 묶음 (Core 는 SO 참조 불가). T29 의 float 4개 생성자는 대체됨(호출처 없음).
  - 검증 함수 이름 `RequireDuration` → `RequireNonNegativeFinite` (배율에도 사용).
  - 〔임시값〕 PhaseTransition 1.5초 (기획서 값 없음, 레거시 0.5초는 연출에 짧음 — 현민 승인). 배율 3단계·상태명 PhaseTransition 도 boss.md 임시 주석 대상.
- 리스크:
  - 인터럽트로 끊긴 패턴의 알림이 **다음 `Begin` 이후에** 도착하면 새 패턴 종료로 처리됨 → T31 어댑터는 인터럽트 시 패턴 CTS 를 취소하고, 취소된 패턴은 알림을 보내지 않아야 함.
  - `StateChanged` 구독자가 여럿이고 그중 하나가 핸들러에서 `Begin`·인터럽트를 호출하면 다른 구독자가 이벤트를 역순으로 받을 수 있음 → 상태를 바꾸는 호출은 Driver 한 곳에서만.
  - `default(BossFlowSettings)` (모두 0) 를 넘기면 예외 없이 모든 시간 상태가 건너뛰어지고 배율 0 → 어댑터는 Config 누락 시 `GameLog.Error` 후 꺼져야 함 (T31).
  - Groggy 조기 종료(QTE Fail) API 는 없음 → T65 에서 추가 (내부 `EnterAfter(Groggy)` 경로 재사용 예정).
  - 작업 중 에디터가 열려 T29 `.cs` 4개의 `.meta` 가 생성되고 `Test_Boss.unity` 가 수정됨 (Claude 가 건드리지 않음) → 커밋 여부 현민 판단.

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 | 문제 없음으로 판단. C# 9 범위. `readonly struct` 생성자에서 get 전용 자동 프로퍼티 전부 대입. `ArgumentOutOfRangeException(string, object, string)` 오버로드 존재. struct → 같은 어셈블리 `internal static` 호출 (asmdef 없음). `UnMachine.Boss` 안에서 `using UnMachine.Core.Boss` (같은 이름 타입 없음). `MinAttribute`·`CreateAssetMenu` 유효. `RequireDuration` 잔여 참조 0, `new BossFlowMachine(` 호출 0 (Grep). 모든 줄 120자 이하(PowerShell 확인) |
| unity-guard | 문제 없음. Core 6파일은 `using System` 만 또는 using 없음 (Unity·`Time`·`Random` 없음). 코루틴·`async`·할당 없음 (`Tick` 루프 할당 0, 예외 문자열은 오류 경로만). SO 는 `[SerializeField] private` + `Min(0)`, public 필드 없음. 이벤트 구독·해제는 T31 어댑터 책임 |
| spec-check | ✅ 4장 배율 1.0 / 1.2(Punishable·Stunned) / 1.5(Groggy) / ✅ 5장 Stunned 1.5 → Resting, Groggy 4 → Resting, PhaseTransition → Idle(2P), Dead 최우선 / ✅ 5장·8장 Groggy 중 전환 보류, 1회만 / ✅ 5장·8장 휴식 1.5·0.8, 2P 틈 ×0.8 / ✅ 9장 예고 최소 0.4 / ⚠️ PhaseTransition 시간 기획서 없음 → 〔임시값〕 1.5 / ➖ QTE·게이지(T60·T65), 무적·연출(T33·T34) |

## 5. 에디터 작업 (현민)
1. Unity Console 컴파일 에러 0 확인. 새 `.meta` 3개 추가 생성 확인 (`BossDamageScale`·`BossFlowSettings`·`BossFlowConfig`; T29 4개는 이미 생김).
2. Project 창에서 보스 Config 를 둘 폴더(예: `Assets/LHM/` 아래 보스 데이터 폴더) 우클릭 → `Create > UnMachine > Config > Boss Flow` → 이름 `BossFlowConfig`.
3. 생성된 에셋 인스펙터 값 확인: 휴식 1.5 / 0.8, 최소 예고 0.4, 2P 틈 배율 0.8, Stunned 1.5, Groggy 4, 전환 1.5, 배율 1 / 1.2 / 1.5.
   (T31 에서 `BossFlowDriver` 에 이 에셋을 연결한다)

## 5-1. 씬 테스트 절차 (현민)
**이번 작업은 씬에서 볼 것이 없다** (Core + SO, 호출하는 곳 없음). 컴파일 에러 0 + 에셋 생성·초기값 확인이면 이번 단계 끝.
T31 씬 테스트에서 확인할 항목 (BACKLOG T29·T30 완료 기준):
| # | T31 에서 조작 | 기대 결과 | 근거 |
|---|---|---|---|
| 1 | 토글 On → 착지 후 관찰 | 라벨 Idle → Telegraph → Attacking → Punishable → Resting 반복 | 5장 흐름 |
| 2 | 예고 0.2초 패턴 | Telegraph 0.4초 유지 | 9장 |
| 3 | 반격 틈 0 패턴 | Punishable 라벨 없이 Attacking → Resting | T29 승인 |
| 4 | F3 | Stunned 1.5초 → Resting, 그동안 배율 표시 1.2 | 5장, 4장 |
| 5 | F4 | Groggy 4초 → Resting, 배율 1.5 | 5장, 4장 |
| 6 | F4 → Groggy 중 F1 | Groggy 4초 다 끝난 뒤 PhaseTransition 1.5초 → 2P Idle, `PhaseChanged(2)` 1회 | 5장·8장 보류 |
| 7 | 2P 에서 휴식·틈 | Resting 0.8초, 틈 = 패턴 값 ×0.8 | 8장 |
| 8 | Attacking 중 F3 | 패턴 중단 → Stunned. Console 에 `InvalidOperationException` 없음 | 늦은 알림 규칙 |
| 9 | Groggy 중 F3 / PhaseTransition 중 F4 | 무시 (상태 그대로) | 우선순위 |

**경계 상황**: F1 두 번 → 전환 1회만. 사망 직후 F3·F4·F1 → 전부 무시.
**Console에 나오면 안 되는 것**: `InvalidOperationException`(Begin/NotifyAttackEnded), `ArgumentOutOfRangeException`(dt·Config 값).

## 5-2. 현민 씬 테스트 결과
- (현민 확인 필요 — 이번엔 컴파일·에셋 생성 확인만)

## 6. 기획서와 다르게 한 점
| 기획서 / BACKLOG | 구현 | 이유 |
|---|---|---|
| T30 예상 파일 3개 | `Core/BossFlowSettings.cs` 추가 (+ `BossFlowState.cs` 수정) | Core 가 SO 를 참조할 수 없음 + 생성자 인자 8개 (현민 승인) |
| PhaseTransition 시간 없음 | 〔임시값〕 1.5초 | 현민 승인 |
| (명시 없음) PhaseTransition·Dead 피해 배율 | 기본 1.0, 무적은 T33 | 4장 표에 해당 상태 없음, 무적은 BossController 담당(T33) |

## 7. 결정 사항
- 반격 틈 0 → Punishable 건너뜀, 모든 0초 시간 상태 건너뜀 (T29 승인).
- 인터럽트로 끊긴 패턴의 늦은 `NotifyAttackEnded` = 무시(false), 그 외 순서 오류 = 예외.
- PhaseTransition 〔임시값〕 1.5초.
- 페이즈 전환 종료 시 페이즈 변경 → Idle 순서.

## 8. 알려진 문제 / 미완료
- Groggy 조기 종료(QTE) = T65. 게이지 = T60. 무적·자물쇠 = T33. 전환 연출 = T34. 어댑터·라벨·F3/F4 = T31.

## 9. 리뷰어에게 요청
- `Tick` 이월 루프 + 0초 건너뛰기 + 핸들러 안 `Begin`/인터럽트 호출 조합에서 무한 루프·상태 꼬임이 없는지.
- 우선순위 표(3장)와 `IsBaseLoop`·`EnterGroggy` 조건이 일치하는지, 보류된 전환이 Dead 에서 취소되는지.
- `_isAttackInterrupted` 규칙이 T31 패턴 취소 흐름에 충분한지.
- `RequireNonNegativeFinite` 를 `BossFlowMachine` 의 `internal static` 으로 둔 것 (보스 Core 4개 타입이 사용).
- (0장) T27·T28·T22 리뷰는 범위 `d60f4f9..ae589ab`.

## 10. 질문 (현민 답변 필요)
- (T22) 체력바 색상 경계도 `>= 0.5f` 로 맞출지 (기본: 그대로).
- (T28) Player 채널 동기화 유지 여부 (기본 유지).

## 11. 다음 할 일
1. 현민: 컴파일 확인 → `BossFlowConfig` 에셋 생성 → 커밋 → Gemini 리뷰 (T29·T30 + 0장)
2. 다음 작업: T31 (`BossFlowDriver` + BossController 연결, 씬에서 T29·T30 확인) — 새 세션 권장

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- 한 번에: `[feat] T29·T30 BossFlowMachine 기본 루프 + 인터럽트 상태, BossFlowConfig` —
  `Assets/LHM/0_Script/Boss/New/Core/BossFlowMachine.cs`, `BossFlowState.cs`, `PatternTiming.cs`, `PatternResult.cs`,
  `BossDamageScale.cs`, `BossFlowSettings.cs`, `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowConfig.cs`
  (**`.meta` 7개 함께 커밋**), `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md`
- 새로 만든 `BossFlowConfig.asset`(+ `.meta`) 은 `[asset] 보스 Flow Config 에셋` 으로 별도 커밋 권장.
- T29·T30 은 같은 파일(`BossFlowMachine.cs`·`BossFlowState.cs`)을 고쳐서 나눠 커밋하기 어려움 → 한 번에 권장.
- `Assets/LHM/Test_Boss.unity` 는 에디터가 바꾼 것 → 현민 판단 (`[asset]` 분리 또는 되돌리기).
