# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: 50% 페이즈 경계 `>= 0.5f` (정확히 50% = 1P)
- BACKLOG 작업 ID: T22
- 기획서 근거: boss.md 5장 "HP 50% 최초 하회 → PhaseTransition", 페이즈 전환 절(240행)
- 작성자: Claude
- 작성 일시: 2026-11-25
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `d60f4f9` (T27·T28 미커밋 상태에서 착수 — 현민 승인. **diff에 T27·T28 변경 포함**, 아래 0장)
- 커밋 상태: 현민 커밋 전 (T27·T28·T22 모두)
- 상태: ✅ T22 씬 테스트 통과·커밋(`86a0b10`) / ⚠️ T28 병합 누락 복구(12장) → 현민 컴파일·재확인 대기
- 이전 작업: T27 `GameClock` · T28 `GameClockDriver` (씬 테스트 통과, 미커밋, Gemini 리뷰 대기)

## 0. 같은 diff에 섞인 이전 작업 (T27·T28, 미커밋)
리뷰어는 기준 커밋 이후 diff에서 아래 파일을 **T27·T28** 로 보고, T22는 2장 두 파일만 본다.
- T27·T28 파일: `New/Core/GameClock.cs`, `New/Core/ClockChannel.cs`, `New/Gimmick/GameClockDriver.cs`,
  `CSH/0.Scripts/Hitstop/HitstopManager.cs`, `New/Editor/BossTestSceneBuilder.cs`, `DECISIONS.md`
- T28 리뷰 요청 (이전 HANDOFF 9장): `HitstopManager` `finally` 의 `_cts == ownCts` 판정이 교체·완료·OnDestroy 세 경로에서
  맞는지 / Driver 정적 `s_active` 와 이벤트 구독 해제 순서(도메인 리로드 끔 포함) / T27 정적 `GameClock` + `Reset` 의존,
  부분 프레임 정지 계산
- T28 열린 질문: Player 채널 `PlayerTimeScale` 동기화 유지 여부 (기본 유지)
- 에디터가 바꾼 파일 (현민 판단): `Test_Boss.unity`(+`.meta`), `Hovl Studio/.../Screen microcircuit.prefab`,
  `ProjectSettings/ShaderGraphSettings.asset`

## 1. 목표
보스 HP가 **정확히 50%일 때는 1페이즈 유지**, 50% 미만으로 처음 내려갈 때 2페이즈로 1회 전환 (boss.md "최초 하회").
체력바 PHASE 표시도 같은 기준을 쓴다.

## 2. 변경 파일
| 파일 | 계층 | 변경 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/BossController.cs` | Runtime 어댑터 (+33줄) | `public const float Phase2HpRatio = 0.5f`, `public static int PhaseForRatio(float hpRatio)` (`>=` → 1, 미만 → 2). `GetCurrentPhase` 가 이걸 호출. 에디터 전용 **F6** `DebugSetHpToPhaseBoundary()` — HP를 `maxHP × 0.5` 로 설정(DamageHandler·체력바 동기화, 전환 안 함) + `GameLog` 로 계산/현재 페이즈 출력 |
| `Assets/LHM/0_Script/Boss/New/BossHealthBar.cs` | Runtime UI (1줄) | 자체 `> 0.5f` 삭제 → `BossController.PhaseForRatio(currentHP / maxHP)` |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T22 ✅ → 🎮 |

## 3. 동작 변화와 리스크
- 동작 변화:
  - HP 비율이 **정확히 0.5** 일 때 이전엔 2P → 이제 1P. 그 외 값은 동일.
  - 예) maxHP 500: 250 → 1P 유지, 249(또는 그 미만) → 2P. 전환 1회 보장은 기존 `hasPhaseTransitioned` 그대로.
  - 체력바 PHASE 표시가 컨트롤러와 같은 함수 사용 → 경계에서 표시·실제 페이즈 불일치 없음.
- 설계 선택:
  - 기준값을 한 곳(`BossController.Phase2HpRatio`)에 둠 → 체력바가 따로 0.5를 갖지 않음 (DRY).
  - 50%는 기획서 고정 규칙(튜닝 값 아님)이라 Config SO 가 아닌 `const`. 튜닝 필요하면 질문 바람.
  - Core 파일 신설 대신 `BossController` 정적 함수 — BACKLOG 예상 파일 2개 범위 유지. T31 이후 FSM이 Core로 가면 그때 이동 검토.
  - 디버그 키 **F6**: BACKLOG가 F1(전환)·F2(히트스톱)·F3/F4(T31 Stunned·Groggy)·F5(그로기 +25)를 이미 예약.
- 리스크:
  - F6 은 상태 체크 없이 HP만 씀 (F1과 같은 방식). **2P 전환 후 F6** 을 누르면 체력바는 PHASE 1 표시, 컨트롤러는 2P 유지(전환은 1회만) → 디버그 전용 불일치, 실제 게임엔 회복 없음.
  - 체력바 **색상**(`UpdateHealthColor`)의 `> 0.5f` 는 페이즈가 아닌 색 구간이라 그대로 둠 → 정확히 50%에서 노랑 + PHASE 1. 맞추길 원하면 질문 바람.
  - 부동소수: `maxHP × 0.5 / maxHP` 는 정확히 0.5 (2의 거듭제곱 곱) → F6 경계 재현 안정적. 일반 타격 누적으로 정확히 250에 도달하는 경우도 정수 피해면 정확.

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 | 문제 없음으로 판단. `BossController`·`BossHealthBar` 둘 다 전역 네임스페이스, asmdef 없음(Assembly-CSharp). `Old_Stage2/BossController.cs` 는 전체 주석 → 이름 충돌 없음(grep). `GameLog.Info(string, Object)` 는 같은 파일 `NotifyPhaseChanged` 와 같은 형태, `using UnMachine` 있음. `damageHandler.SetHP(float)` 존재(`DamageHandler.cs:147`, 클램프 + `OnHPChanged`). 추가 줄 120자 이하(PowerShell 문자 수 확인) |
| unity-guard | 문제 없음. 새 코루틴·`async`·`Find*`·`?.` 없음. `Update` 추가분은 키 체크만(할당 없음, 문자열 보간은 F6 경로만). 디버그 코드 `#if UNITY_EDITOR`. 로그 `GameLog`. `Input.GetKeyDown` 은 기존 F1과 같은 구 입력 API(`activeInputHandler` Both) |
| spec-check | ✅ boss.md 5장 "HP 50% 최초 하회" — 50%는 하회 아님 → 1P, 미만 → 2P, 1회 / ✅ BACKLOG T22 "체력바 표시도 동일" / ➖ 반복 방지(T37)·드론 제한(T50)·거리(T37) 범위 밖, 미수정 |

## 5. 에디터 작업 (현민)
1. Unity 열기 → Console 컴파일 에러 0 확인. (새 파일 없음 → `.meta` 생성 없음)
2. 그 외 없음.

## 5-1. 씬 테스트 절차 (현민)
**준비**: 빌더로 만든 보스 테스트 씬(T28에서 쓴 씬)에서 Play. 보스 등장(Appearing) 끝나고 전투 시작까지 대기. **키 입력 전 Game 뷰 클릭.**
- 테스트 씬에는 `BossHealthBar` 가 없음 → 컨트롤러 페이즈는 **Console 로그**로 확인. PHASE 표시는 마지막 항목에서 `LHM.unity` 로 확인.

**조작 → 기대 결과**
| # | 조작 | 기대 결과 | 근거 |
|---|---|---|---|
| 1 | F6 | Console `[Debug] F6: 보스 HP 250/500 (경계) → 계산 페이즈 1, 현재 1` (maxHP가 500이 아니면 그 절반). `[Boss] PhaseChanged` 로그 **없음**. 보스는 1P 패턴·휴식 2초 계속 | 5장 "50% 최초 하회" (정확히 50%는 하회 아님) |
| 2 | 1번 직후 보스를 일반 공격 1회 적중 | `[Boss] PhaseChanged → 2` **1회**, 2P 전환 연출(기존 화면 효과) → 2P 패턴(드론 포함), 휴식 1초 | 5장, T22 "249 → 2P 전환 1회" |
| 3 | 2P에서 계속 공격 | `PhaseChanged` 추가로 안 나옴 | 전환 1회 |
| 4 | Play 재시작 → F1 | 기존과 같음: HP 49% → 즉시 2P 전환 + `PhaseChanged → 2` 1회 | 회귀 확인 |
| 5 | `LHM.unity` 에서 보스전 시작 → F6 | 체력바 `PHASE 1`, 슬라이더 절반, 텍스트 `250 / 500`. 이어서 공격 1회 → `PHASE 2` 로 바뀌고 깜빡임 3회 | T22 "체력바 표시도 동일" |

**경계 상황**
- 1번 상태에서 피격 무적 중 추가 타격 → HP 그대로, 1P 유지.
- F6 을 2번 연속 → 두 번 다 `계산 페이즈 1, 현재 1`, 전환 없음.
- (디버그 전용, 3장 리스크) 2P 전환 후 F6 → 로그 `계산 페이즈 1, 현재 2`, 컨트롤러 2P 유지. 정상.

**Console에 나오면 안 되는 것**: `NullReferenceException`(BossController·BossHealthBar·DamageHandler), `PhaseChanged → 2` 2회 이상, F6 직후 `PhaseChanged`.

## 5-2. 현민 씬 테스트 결과
- 1차 (2026-11-25): **1~5 통과** (현민 보고). 경계 상황 항목은 별도 보고 없음.

## 6. 기획서와 다르게 한 점
| 기획서 | 구현 | 이유 |
|---|---|---|
| BACKLOG T22 디버그 언급 없음 | 에디터 전용 F6 추가 | AI_RULES 6장 "체력 정확히 50%" 경계 확인용. F1은 49%라 경계 재현 불가 |

## 7. 결정 사항
- 페이즈 경계 단일 원본 = `BossController.PhaseForRatio` / `Phase2HpRatio` (const, Config 아님).
- 디버그 키 F6 = HP 경계 설정 (F1~F5 BACKLOG 예약).

## 8. 알려진 문제 / 미완료
- 체력바 색상 경계 `> 0.5f` 는 그대로 (3장).
- T33(PhaseTransition 상태)에서 전환 경로가 바뀌면 `TakeDamage`·F1 블록의 `GetCurrentPhase` 호출부는 그대로 재사용 가능.

## 9. 리뷰어에게 요청
- `PhaseForRatio` 를 `BossController` 정적 함수로 둔 것(Core 미분리)이 T22 범위에서 적절한지.
- F6 디버그가 `damageHandler.SetHP` 를 호출해 `OnHPChanged` 를 내보내는 것이 다른 구독자(SYS `BossHealthBarBridge` 등)에 부작용 없는지.
- (0장) T27·T28 리뷰 요청 항목.

## 10. 질문 (현민 답변 필요)
- 체력바 색상 경계도 `>= 0.5f` 로 맞출지 (기본: 그대로).
- (T28) Player 채널 동기화 유지 여부 (기본 유지).

## 11. 다음 할 일
1. 현민: 커밋 (T27·T28·T22) → Gemini 리뷰 요청
2. 새 세션에서 다음 작업 (BACKLOG 순서: T29)

## 12. T28 병합 누락 복구 (2026-11-25, 커밋 후 컴파일 에러)
- 증상: `GameClockDriver.cs(161,80): error CS1061: 'HitstopManager' does not contain a definition for 'RemainingTime'`
- 원인: 팀원 커밋 `b3471dc`(yunjiwoo "버그 수정")가 `HitstopManager` 를 별도로 수정 — `_version` 카운터로 Q25 ①(이전 루틴
  `finally` 가 새 히트스톱을 끊음)만 해결. 병합 때 T28 로컬 수정이 팀원 버전으로 대체되어 ② `RemainingTime`·③ 긴 쪽 유지가
  빠졌고, `86a0b10` 에 `HitstopManager.cs` 미포함.
- 수정: `Assets/CSH/0.Scripts/Hitstop/HitstopManager.cs` — **팀원 `_version` 구조는 그대로 두고** ②③만 다시 추가
  - `_endTime`(unscaledTime 기준) + `public float RemainingTime` (`_cts == null` 이면 0)
  - `TriggerHitstop`: 진행 중(`_cts != null`)이고 새 종료 시각 ≤ `_endTime` 이면 무시, 늦으면 새 요청(길이·배율)으로 교체
  - 진행 판정: 현재 루틴 `finally`(`version == _version`)와 `OnDestroy` 가 `_cts = null` → 다음 요청은 항상 통과
- 정적 점검: `using UnityEngine` 있음(`Mathf`·`Time`), 새 할당 없음, `UnityEngine.Object` `?.` 신규 없음. 기존
  `CustomTimeManager.Instance?.`(OnDestroy)는 팀원·기존 줄이라 미수정.
- 다시 확인할 씬 테스트 (T28 절차 기준, 빌더 테스트 씬):
  1. Play → Console 컴파일 에러·NRE 없음
  2. F2 → `GameClock` 인스펙터 HitstopManager 남은 시간 ~0.5→0, 플레이어 0.05배 슬로우 → 1배속 복귀
  3. 플레이어 0.8초 히트스톱(H) 직후 F2 → HitstopManager 남은 시간이 0.8초 기준으로 계속 감소 (0.5로 줄지 않음)
  4. 0.8초 히트스톱이 0.3초 이하 남았을 때 F2 → 0.5로 연장, 그 동안 0.05배
  5. 일반 타격(0.08초) 연타 → 끝나면 1배속 복귀 (배율이 낮은 채로 남지 않음)
- 커밋 제안: `[fix] T28 HitstopManager RemainingTime·긴 쪽 유지 복구 (b3471dc 병합 누락)` —
  `Assets/CSH/0.Scripts/Hitstop/HitstopManager.cs`, `Assets/LHM/8_MD/HANDOFF.md`
- 참고: CSH 파일을 팀원도 수정 중 → 이 커밋 전 팀원과 공유 권장 (같은 파일 동시 수정 충돌 방지).

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
1. T27·T28 (이전 HANDOFF 제안 그대로):
   - `[feat] T27 GameClock 시간 채널·히트스톱 (Core)` — `Assets/LHM/0_Script/Boss/New/Core/GameClock.cs`, `ClockChannel.cs` (+ `.meta` 함께)
   - `[feat] T28 GameClockDriver, HitstopManager 겹침 수정 (Q25)` — `Assets/LHM/0_Script/Boss/New/Gimmick/GameClockDriver.cs` (+ `.meta`), `Assets/CSH/0.Scripts/Hitstop/HitstopManager.cs`, `Assets/LHM/8_MD/DECISIONS.md`
   - `[chore] 테스트 씬 빌더: 매니저·카메라 리그·GameClock 포함` — `Assets/LHM/0_Script/Boss/New/Editor/BossTestSceneBuilder.cs`
2. T22: `[fix] T22 페이즈 경계 50% 포함 1P (>= 0.5f), 체력바 동일 기준` —
   `Assets/LHM/0_Script/Boss/New/BossController.cs`, `Assets/LHM/0_Script/Boss/New/BossHealthBar.cs`,
   `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md` (새 파일 없음 → `.meta` 없음)
- `HANDOFF.md`·`BACKLOG.md` 는 T27·T28·T22 내용이 한 파일에 섞여 있음 → 마지막 커밋(T22)에 넣거나 한 번에 커밋.
- `Test_Boss.unity`·`Screen microcircuit.prefab`·`ShaderGraphSettings.asset` 은 에디터 자동 변경 → 현민 판단 (`[asset]` 분리 또는 되돌리기).
