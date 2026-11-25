# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: BossController 이벤트 훅 (BattleStarted·PhaseChanged·Died·InvulnerableChanged)
- BACKLOG 작업 ID: T2
- 기획서 근거: boss.md 5장(상태), 6-1(등장), 10-1(페이즈 전환), 10-2(사망), 12장(무적 표시)
- 작성자: Claude
- 작성 일시: 2026-11-25
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `778fca9`
- 커밋 상태: 현민 커밋 전
- 상태: 🎮 현민 씬 테스트 대기
- 이전 작업: T1 — 현민 씬 테스트 통과, 커밋 `778fca9`

## 1. 목표
새 기믹 층(T3 Director, T21 체력바)이 보스 생애주기를 알 수 있게 BossController에 알림만 추가한다. 기존 FSM·코루틴 흐름은 바꾸지 않는다.

## 2. 변경 파일
| 파일 | 계층 | 변경 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/BossController.cs` | Runtime | +76줄, 삭제 0. `using UnMachine;`, 이벤트 4개 + `IsInvulnerable`, 호출 지점 4곳, private 헬퍼 3개(`NotifyPhaseChanged`, `ComputeInvulnerable`, `UpdateInvulnerableNotification`) |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T1 🏁 / T2 🎮 |

호출 지점:
| 이벤트 | 위치 | 시점 |
|---|---|---|
| `BattleStarted` | `AppearingSequence` 끝 | BGM 재생 후, `ChangeState(Idle)` 직전 |
| `PhaseChanged(int)` | `TakeDamage` 전환 블록, 에디터 F1 블록 | 정리(패턴 중단·발사체 회수·이펙트) 후, `ChangeState(Idle)` 직전 |
| `Died` | `ChangeState` | Dead 최초 진입 1회 (`_hasDied` 플래그) |
| `InvulnerableChanged(bool)` | `ChangeState` | 무적 표시 여부가 바뀔 때만. 현재 기준 = `Appearing` 상태 |

## 3. 동작 변화와 리스크
- 동작 변화: 게임플레이 변화 없음. Console에 `[Boss] …` GameLog 4종 추가.
- 리스크:
  - `IsInvulnerable` 은 **0.2초 피격 무적을 포함하지 않는다** (Q4: 자물쇠 안 띄움). 실제 피해 거부 여부와 다를 수 있음 → 이름 그대로 "표시용 무적"으로 쓸 것. T19에서 2페이즈 쉴드를 `ComputeInvulnerable()` 에 추가.
  - `_isInvulnerableNotified` 초기값 false → `Start` 에서 `ChangeState(Appearing)` 시 true 알림 1회. `Awake`~`Start` 사이 구독자는 이벤트를 받고, 그 뒤 구독자는 `IsInvulnerable` 을 먼저 읽어야 함(주석 명시).
  - 한 번의 피격으로 전환과 사망이 동시에 일어나면 `PhaseChanged(2)` → `Died` 순서로 둘 다 발생 (의도된 순서).
  - 에디터 F1 경로는 기존 코드대로 `ClearAllActiveProjectiles()` 를 호출하지 않음 (기존 차이 유지, 범위 밖).
  - 구독자는 `OnDisable/OnDestroy` 에서 해제 필요 (T3에서 준수).

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 | 문제 없음으로 판단. `using System;` 을 넣지 않고 `System.Action` 전체 이름 사용(UnityEngine `Object`/`Random` 모호성 회피). `GameLog.Info(string, Object)` 에 `this` 전달. 네임스페이스 `UnMachine` 에는 `GameLog` 만 있어 충돌 없음 |
| unity-guard | 문제 없음. 델리게이트에 `?.Invoke` 는 UnityEngine.Object 가 아니므로 허용. 새 코루틴·Find·할당 없음(문자열 보간은 이벤트 발생 시 1회, 매 프레임 아님) |
| spec-check | ✅ 10-1 "전환 1회만" = 기존 `hasPhaseTransitioned` 블록 안에서만 알림 / ✅ 10-2 사망 1회 / ✅ 6-1 "Appearing 동안 완전 무적" = `IsInvulnerable` true / ➖ 12장 자물쇠 표시는 T21 |

## 5. 에디터 작업 (현민)
없음.

## 5-1. 씬 테스트 절차 (현민)
준비: 보스가 있는 씬(`LHM.unity`) 열기. Console의 Collapse는 **끄기**(횟수 확인용).

| # | 조작 | 기대 결과 | 결과 (현민) |
|---|---|---|---|
| 1 | Play | 시작 직후 `[Boss] InvulnerableChanged → True` 1회 | ⬜ |
| 2 | 낙하·착지 대기 (약 3초) | `[Boss] BattleStarted` 1회 → 바로 `[Boss] InvulnerableChanged → False` 1회 (spec 6-1) | ⬜ |
| 3 | F1 | `[Boss] PhaseChanged → 2` 1회 + 기존 `[Debug] F1: 2페이즈 전환 완료` | ⬜ |
| 4 | F1 다시 여러 번 | `PhaseChanged` 추가 출력 없음 (spec 10-1 전환 1회) | ⬜ |
| 5 | Play 재시작 후 보스를 공격해 체력 50% 미만으로 | `[Boss] PhaseChanged → 2` 1회 | ⬜ |
| 6 | 보스 처치 | `[Boss] Died` 1회, 2초 뒤 보스 비활성 (spec 10-2) | ⬜ |

- 경계 상황:
  - 처치 테스트가 오래 걸리면 인스펙터에서 BossController `Max HP` 를 임시로 10 정도로 낮춰서 확인 → **테스트 후 500으로 되돌리기** (씬 저장 주의)
  - 5·6을 한 방에: Max HP를 낮춘 상태에서 한 번에 50% 아래 + 0 이하가 되면 `PhaseChanged → 2` 다음 `Died` 순서로 각 1회
  - 등장 중(1~2 사이) 공격 → 피해 0, 로그 변화 없음
- 디버그 표시: GameLog 로그 (Console)
- Console에 나오면 안 되는 것: `Died` 2회 이상, `PhaseChanged` 2회 이상, `NullReferenceException`, `CS0104` 모호성 에러

## 5-2. 현민 씬 테스트 결과
- 결과: 대기

## 6. 기획서와 다르게 한 점
| 기획서 | 구현 | 이유 |
|---|---|---|
| — | 없음 | — |

## 7. 결정 사항
- "표시용 무적"(`IsInvulnerable`)과 "피해 거부"는 분리. 피격 0.2초 무적은 표시 대상 아님 (BACKLOG Q4).
- 이벤트 판정 기준은 `ComputeInvulnerable()` 한 곳에만 둔다.

## 8. 알려진 문제 / 미완료
- 기존 HP 50% 경계 버그(`> 0.5f` → HP 250에서 2P)는 T22에서 수정. 이번 이벤트는 기존 판정을 그대로 따름.

## 9. 리뷰어에게 요청
- 호출 지점 순서(정리 후 → 알림 → Idle)가 T3 Director의 "이전 기믹 회수 후 다음 기믹 시작"에 맞는지
- `_hasDied` 와 기존 `ChangeState` Dead 가드의 중복 여부

## 10. 질문 (현민 답변 필요)
- 없음 (Q18은 T19 착수 전까지)

## 11. 다음 할 일
1. 현민 씬 테스트 → 커밋
2. 새 세션에서 `/task T3` (BossArenaDirector + IBossGimmick + BossArenaConfig)

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- 메시지: `[feat] T2 BossController 이벤트 훅 추가`
- 포함할 파일: `Assets/LHM/0_Script/Boss/New/BossController.cs`, `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md` (새 `.meta` 없음)

## 12. 리뷰 대응
(리뷰 반영 시 작성)

## 13. 규칙 변경 제안
- 없음

---

## 추가 작업: T26 테스트 씬 생성 툴 (T2와 같은 기준 커밋 `778fca9`, 별도 커밋 권장)

- 근거: 현민 요청 + AI_RULES 0장 2번 예외(2026-11-25 추가, DECISIONS 기록)
- 변경 파일: `Assets/LHM/0_Script/Boss/New/Editor/BossTestSceneBuilder.cs` (신규, Editor 폴더 → 에디터 전용), `AI_RULES.md`, `DECISIONS.md`, `BACKLOG.md`(T26 행)
- 동작: 메뉴 `UnMachine/Test Scene/Boss Arena` → 창에서 [테스트 씬 만들기] → `Assets/LHM/Test_Boss.unity` 새로 생성·저장
  - 바닥 윗면 y=0, 방 중앙 x=0. 40×30 방 = Ground·Ceiling·WallLeft·WallRight (BoxCollider2D + 내장 UISprite, `ground` 레이어)
  - Main Camera(직교, 크기 = 세로/2 + 여백 2), Global Light 2D(모든 Sorting Layer), 마커 3개(BossSpawn·PlayerSpawn·MirrorBottomCenter)
  - 플레이어 프리팹 기본값 `Assets/CSH/8.Prefabs/Player/Player.prefab`, 보스 프리팹은 지정 시 배치 + BossController/BossAttackPatterns 의 `player` 필드 연결
- 안전장치: 파일명 `Test_` 강제, 같은 경로 파일 있으면 중단(덮어쓰기 없음), 현재 씬 변경은 저장 여부를 물음, 기존 씬은 열거나 저장하지 않음
- 리스크:
  - **보스 프리팹이 프로젝트에 없음** → 지정 안 하면 보스 자리에 마커만 생김 (아래 에디터 작업 1)
  - 보스를 프리팹으로 만들면 씬 오브젝트 참조(체력바 Canvas 등)는 프리팹에서 빠짐 → 테스트 씬에서 체력바 없음(BossController는 null 체크함). 필요하면 체력바 Canvas를 수동 추가
  - 플레이어 프리팹이 다른 씬 매니저(UI·GameManager 등)를 기대하면 Play 시 에러 가능 → Console 확인
  - `Light2D` 는 타입 이름으로 찾음(URP 직접 참조 없음). 못 찾으면 경고 후 생략 → 화면이 어두우면 Global Light 2D 수동 추가
- 정적 점검: 컴파일 가능성 — `EditorSceneManager.NewScene/SaveScene/SaveCurrentModifiedScenesIfUserWantsTo`, `PrefabUtility.InstantiatePrefab`, `AssetDatabase.GetBuiltinExtraResource`, `SortingLayer.layers` 사용, 문제 없음으로 판단 / unity-guard — 런타임 코드 아님, Find 없음, 기존 씬 저장 없음 / spec-check — 방 40×30 ✅(boss.md 2장)

### 에디터 작업 (현민)
1. (보스도 배치하려면) `LHM.unity` 를 열고 Hierarchy 의 Boss 오브젝트를 `Assets/LHM/2_Prefabs/` 로 드래그 → Original Prefab → `Boss.prefab`. LHM.unity 는 저장 여부를 직접 판단
2. 메뉴 `UnMachine > Test Scene > Boss Arena` → 창에서 보스 프리팹 칸에 `Boss.prefab` 지정(플레이어는 자동) → [테스트 씬 만들기]
3. 생성된 `Test_Boss.unity` 가 열림. 처치 테스트용으로 Boss 의 `Max HP` 를 낮추려면 이 씬에서만 변경

### 씬 테스트 (현민)
| # | 조작 | 기대 결과 | 결과 |
|---|---|---|---|
| 1 | 메뉴 실행 → 만들기 | Console `[TestScene] 생성 완료: Assets/LHM/Test_Boss.unity (보스 배치, 플레이어 배치)`, 씬 뷰에 회색 방·마커 | ⬜ |
| 2 | 같은 이름으로 다시 만들기 | "이미 있다. 덮어쓰지 않는다" 대화상자, 파일 변화 없음 | ⬜ |
| 3 | Test_Boss Play | 플레이어가 바닥에 서고 좌우로 이동·점프, 벽·천장에 막힘, 화면이 어둡지 않음 | ⬜ |
| 4 | 그대로 대기 | 보스가 상공에서 낙하·착지 → T2 로그(`InvulnerableChanged`/`BattleStarted`) 출력, 보스가 플레이어를 향해 공격 | ⬜ |

- Console에 나오면 안 되는 것: `NullReferenceException`(특히 보스 `player` 관련), "Light2D 타입을 찾지 못해" 경고

### 커밋 제안
- `[docs] AI_RULES: 테스트 씬 생성 에디터 툴 허용` — `AI_RULES.md`, `DECISIONS.md`
- `[feat] T26 보스 테스트 씬 생성 툴` — `New/Editor/BossTestSceneBuilder.cs` + `Editor/` 폴더·`.cs` `.meta`, `BACKLOG.md`(T26 행)
- `[asset] 보스 테스트 씬·보스 프리팹` — `Test_Boss.unity`, `Boss.prefab` (+ `.meta`) — 현민이 만든 뒤

---

## 추가 작업: 보스 기획 v2 확정 (문서만, 기준 HEAD `21b72cd`)

- 근거: 현민 제공 수정안 v2 + 검토 결정 16건 (추천안 그대로 확정, 2026-11-25)
- 변경 파일: `boss.md` (v2 확정본으로 교체, 17장에 결정 기록), `README.md` (boss.md 상태), `DECISIONS.md` (1행)
- v1 원본 정리본: `boss_system.md` (같은 내용 사본) + git 히스토리
- 코드 변경 없음 → 정적 점검·씬 테스트 해당 없음
- **BACKLOG 영향 (아직 반영 안 함)**: T5~T13 이동(다른 보스), T16~T18·T25 폐기(아재 → 낙섬 QTE), Q4·Q8·Q10~Q12 무효, T14·T15 최소 폭 24, T19 그로기 진입 조건 변경, T22 거리 값 재정의, 새 작업(Telegraph·Punishable·Stunned·PhaseTransition 상태, 패턴 6종, 패턴 선택기, 그로기 게이지, `GameClock` 히트스톱) → `/breakdown` 필요
- 남은 확인: `짓밟기-Sheet` 의도(아트), 패링 구현 시점(CSH), 연섬 동작
- `git status` 의 `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings.asset` 변경은 이번 작업과 무관 (Unity 자동 변경, 현민 판단)

### 커밋 제안
- `[docs] 보스 기획 v2 확정` — `boss.md`, `README.md`, `DECISIONS.md`, `HANDOFF.md`
