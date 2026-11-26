# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> ⏰ 마감 모드 (AI_RULES 0장): 짧은 형식, Gemini 리뷰 생략. **커밋은 현민이 직접 한다.**

---

## T46 ✂️ P1 점프 내려찍기 (T45 통합) — 🎮 현민 확인 대기
- 근거: boss.md 6장 P1·4장·17장 #11, BACKLOG 축소 계획 6번 / 기준 커밋 `35b2fb2` (착수 시 기록)

### 변경 파일
| 파일 | 내용 |
|---|---|
| `Core/JumpSlamPhase.cs`, `Core/JumpSlamModel.cs` (새) | 도약 0.3 → 추적 1.2(그림자 x = 플레이어 x, 방 클램프) → 고정 0.4 → 낙하 0.2 → 착지. 남은 시간 이월 |
| `Core/ShockwaveModel.cs` (새) | 한 방향 이동, 앞 끝이 벽에 닿으면 소멸 |
| `Gimmick/Pattern/JumpSlamConfig.cs` (새) | 메뉴 `UnMachine/Config/Jump Slam (P1)`. 예고 0.5·틈 1.2·단계 시간·방 x(-20~20)·바닥 y 0·천장 30 +5·피해 8/4·속도 12·높이 1·폭 1·프리팹·Animator 트리거 이름 |
| `Gimmick/Pattern/JumpSlamPattern.cs` (새) | `IBossPattern`. 화면 밖(추적·고정) = 몸체 콜라이더 Off + `SetUntargetable`, 낙하 시작에 On. 착지: 몸체 bounds 범위 피해 8 + 좌우 충격파. 취소 시 바닥 복귀 |
| `Gimmick/Pattern/BossShockwave.cs`, `BossShadowMarker.cs`, `BossHitBox.cs` (새) | 충격파(ObjectPool, 1회 피해, Gizmo) / 그림자(추적 검정 → 고정 빨강) / 플레이어 박스 판정 |
| `Gimmick/BossFlowDriver.cs` | `_jumpSlam` 지정 시 P1 = JumpSlamPattern (목록 P1 무시). 전환·사망 때 충격파 회수. 인스펙터 `Debug Jump Slam Phase` |
| `BossController.cs` | `SetUntargetable(bool)` + TakeDamage 거부 1줄 (`IsInvulnerable`·자물쇠 변화 없음) |
| `Editor/BossTestSceneBuilder.cs` | `JumpSlamConfig` 자동 생성·Driver 연결 |

- 프리팹이 없으면 **임시 사각형**(그림자 검정 띠, 충격파 주황 1×1)으로 동작 → T54 전에도 테스트 가능.
- Animator 트리거(`JumpSlamLaunch`·`JumpSlamLand`)는 컨트롤러에 없으면 건너뜀 (로그 1줄). 웅크림 = Telegraph 0.5초(T39 하늘색 틴트), 별도 애니 없음.
- C2·C3 는 아직 임시 레거시(Stomp) — T56 에서 교체.

### 에디터 작업 (현민)
1. 테스트 씬 다시 생성(`UnMachine/Test Scene/Boss Arena`) → `JumpSlamConfig.asset` 자동 생성·연결. 기존 씬이면 Driver `Jump Slam` 칸에 `Create > UnMachine > Config > Jump Slam (P1)` 에셋 지정.
2. 실제 보스 룸에서 쓰면 Config 의 방 x·바닥 y·천장 y 를 그 룸에 맞춘다.

### 확인할 것
1. Driver `Force Unit` = P1: 하늘색 예고 → 보스가 위로 사라짐 → 바닥 그림자가 플레이어를 1.2초 따라오다 빨갛게 멈춤(0.4초) → 그 자리 착지. 벽 쪽으로 가도 그림자가 방 밖으로 안 나감.
2. 그림자 밖이면 착지 피해 0, 위면 8. 좌우 주황 충격파: 서 있으면 4, 점프로 넘으면 0. 벽에서 사라짐.
3. 화면 밖 동안 공격해도 피해·그로기 0, 체력바 자물쇠 안 뜸. 착지 후 Punishable 1.2초(라벨) 동안 ×1.2. F1(전환) 을 공중에서 누르면 보스가 바닥으로 내려오고 충격파 사라짐.

### 질문
- 도약 0.3초·낙하 0.2초·충격파 폭 1 은 〔임시값〕(기획서 없음). 웅크림 애니를 Telegraph 에 붙일지는 T54 때 결정.

### 커밋 제안
- `[feat] T46 P1 점프 내려찍기: JumpSlamModel·Pattern·충격파·그림자 + 화면 밖 피해 거부` —
  `Core/JumpSlamPhase.cs`, `Core/JumpSlamModel.cs`, `Core/ShockwaveModel.cs`, `Gimmick/Pattern/` 폴더 전체(5개 .cs),
  `Gimmick/BossFlowDriver.cs`, `BossController.cs`, `Editor/BossTestSceneBuilder.cs`, `8_MD/HANDOFF.md`, `BACKLOG.md`
  (+ 각 `.meta`, `Pattern` 폴더 `.meta`, 새로 생기는 `JumpSlamConfig.asset`). 아래 T38~T63 묶음과 같은 파일(Driver·Controller·Builder)을 건드리므로 그 묶음 먼저 또는 함께 커밋.

---

## 기본 정보 (이전 묶음)
- 작업: T38 ✂️ 선택기 연결 + T39 ✂️ 색 예고 + T60·T61·T63 ✂️ 그로기 (전부 미커밋, 한 묶음)
- 근거: boss.md 11장·9장·7장·15장, BACKLOG "★ 1주 축소 계획" 1~5번
- 기준 커밋: `35b2fb2` (T38 착수 시 기록, 이후 작업도 같은 기준)
- 상태: 🎮 현민 확인 대기 (T29~T33·T37 도 이 씬에서 함께 확인)

## 변경 파일
| 파일 | 내용 |
|---|---|
| `Assets/LHM/0_Script/Boss/New/Gimmick/PatternRegistry.cs` (새) | 단위 → `IBossPattern` 등록·조회 |
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs` | 고정 순환(`_legacyCycle`) 삭제 → 선택기. `_patternTable`(필수)·단위→레거시 목록 `_unitPatterns`·드론 상한 1·고정 시드·강제 선택 단위·디버그(구간·체류·후보·최근 2개, 머리 위 라벨 4번째 줄). 콤보 = 첫 패턴만 + 경고 로그. 선택 실패 시 P5 로 대체 |
| `Assets/LHM/0_Script/Boss/New/Gimmick/LegacyPatternBridge.cs` | 레거시 자체 반복 방지 끔 (`useRepeatGuard: false`) |
| `Assets/LHM/0_Script/Boss/New/BossAttackPatterns.cs` | `ExecutePattern(..., bool useRepeatGuard = true)` 매개변수 1개 (기존 BT 호출은 그대로) |
| `Assets/LHM/0_Script/Boss/New/Editor/BossTestSceneBuilder.cs` | Driver 에 `PatternTableConfig` 연결 (없으면 `Assets/LHM/3_ScriptableObject/PatternTableConfig.asset` 새로 생성), `BossTelegraphView` 추가·연결 |
| (T39) `Core/TelegraphSignal.cs`, `Core/TelegraphColor.cs` (새) | 단위 → 색(빨강 깜빡 2회 / 노랑 / 하양) + `IsLit(경과, 길이)`. 콤보 = 첫 패턴 색 (C1 빨강, C2·C3 하양, C4 노랑) |
| (T39) `Gimmick/BossTelegraphView.cs` (새) | Telegraph 동안 보스 SpriteRenderer 전체 곱셈 틴트, 끝나면 원래 색. World 채널 따름 |
| (T39) `Gimmick/BossFlowDriver.cs` | `FlowStateChanged` 이벤트, `CurrentUnit`, `StateRemaining` 공개 |

| (T60) `Core/GroggyGauge.cs`, `Gimmick/GroggyConfig.cs` (새) | 최대 100, 마지막 증가 3초 뒤 초당 5 감소, 가득 차면 유지. Config: 벽 +25, **피해 1당 +0.5 〔임시값〕** (메뉴 `UnMachine/Config/Groggy`) |
| (T61) `Gimmick/BossGroggyDriver.cs` (새) | 피해·벽 충돌 → 게이지, 가득 → `EnterGroggy`(4초 ×1.5), Groggy 끝나면 0. Groggy·전환·사망 중엔 안 오름. 에디터 F5 = +25, 인스펙터 값·마지막 출처 |
| (T61) `BossController.cs` | `Damaged(float 배율 전 피해)` 이벤트 +3줄 |
| (T61) `BossAttackPatterns.cs` | `ChargeHitWall` 이벤트 (레거시 돌진 벽 충돌 지점 1줄) |
| (T61) `Gimmick/BossFlowDriver.cs` | `State`·`IsRunning`·`EnterGroggy()` 공개 |
| (T63) `Gimmick/BossGroggyBar.cs` (새) | Filled 이미지 fillAmount = 게이지, 전투 시작 때 표시·사망 때 숨김 |

- (T61) 벽 충돌 시 v2 Stunned(×1.2)는 들어가지 않음 — 레거시 돌진 자체 스턴을 그대로 씀 (축소안). 벽 충돌로 게이지가 가득 차면 Groggy 가 레거시 스턴을 끊고 들어감.
- (T39) "하양"은 곱셈 틴트로 안 보여서 **옅은 하늘색(0.6, 0.85, 1)** 기본값. View 인스펙터에서 3색 조정 가능.
- (T39) 보스 자식의 이펙트 스프라이트도 함께 물듦 (Awake 시점에 있던 것만). 거슬리면 View `Renderers` 에 본체만 지정.

- 리스크: **기존 씬의 Driver 는 `_patternTable` 이 비어 있어 비활성 → 보스가 기존 흐름(BT)으로 동작**하고 Console 에 Error 1줄. 아래 에디터 작업 1번으로 해결.
- 기존 씬 Driver 의 레거시 순환 값(`_legacyCycle`)은 사라지고 새 목록 기본값이 들어간다.

## 에디터 작업 (현민)
1. 컴파일 확인 → `UnMachine/Test Scene/Boss Arena` 로 Test_Boss 씬 **다시 생성** (PatternTableConfig 자동 연결).
   - 씬을 다시 만들기 싫으면: 보스의 `BossFlowDriver` → `Pattern Table` 칸에 `PatternTableConfig` 에셋 지정 (없으면 `Create > UnMachine > Config > Pattern Table`).

2. (T63) 그로기 바 UI: 보스 체력바 Canvas 아래에 빈 오브젝트 `GroggyBar`(+ `BossGroggyBar`) → 자식 `Root`(배경 Image, 얇게) → 그 자식 `Fill`(Image, Type = Filled / Horizontal / Origin Left). `BossGroggyBar` 에 Groggy(보스의 `BossGroggyDriver`)·Boss·Root·Fill 지정. **Root 는 GroggyBar 자신이 아닌 자식**으로.
   - 테스트 씬 툴은 `BossGroggyDriver`·`GroggyConfig` 까지만 자동 연결 (UI 는 수동).

## 확인할 것
1. 가까이 서 있으면 라벨 `Near` + Stomp(P3) 위주, 3초 넘게 서 있으면 후보 줄에 `P1:6 [체류×]`. 같은 단위 3연속 없음.
2. F1 → 2P: 후보에 C1~C4·P6 등장, 콤보 선택 시 경고 로그 `콤보 C… 미구현`. 드론이 살아 있으면 `[대체 P6]` (P6 → P5).
3. Driver `Force Unit` 켜고 단위 지정 → 그 패턴만 반복. `Use Fixed Seed` 켜고 같은 위치에서 재시작 → 같은 순서.
4. (T39) 라벨이 Telegraph 일 때만 색: P2·P3 빨강 2번 깜빡, P4·P5·P6 노랑, P1(임시 Stomp) 하늘색. Attacking 되면 원래 색. Force Unit 으로 단위별 확인.
5. (T61) 보스를 때리면 `BossGroggyDriver` 값 상승(피해 × 0.5), 3초 가만히 → 초당 5 감소. F5 4번 → 100 → 라벨 Groggy 4초·피해 로그 ×1.5 → 값 0. P2 벽 충돌 → +25(마지막 출처 "벽 충돌"). Groggy 중 때려도 값 안 오름. (T63) 바가 값대로 움직임.

## 질문
- (T37 이어짐) 반복 방지로 후보가 없으면 반복 허용 / P6→P5 대체 시 확률 합산 / 페이즈 전환 때 기록 유지 — 기본값대로 진행 중.

## 커밋 제안
- `[feat] T38 선택기 연결: PatternRegistry + 레거시 영구 매핑, 레거시 반복 방지 끔` —
  `Assets/LHM/0_Script/Boss/New/Gimmick/PatternRegistry.cs`(+ `.meta`), `Gimmick/BossFlowDriver.cs`,
  `Gimmick/LegacyPatternBridge.cs`, `BossAttackPatterns.cs`, `Editor/BossTestSceneBuilder.cs`,
  `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` (+ 새로 생긴 `PatternTableConfig.asset`·`Test_Boss.unity` 와 `.meta`)
- `[feat] T39 예고 색: TelegraphSignal + BossTelegraphView (스프라이트 틴트)` —
  `Core/TelegraphSignal.cs`, `Core/TelegraphColor.cs`, `Gimmick/BossTelegraphView.cs` (+ 각 `.meta`),
  `Gimmick/BossFlowDriver.cs`, `Editor/BossTestSceneBuilder.cs` (T38 과 한 커밋으로 묶어도 됨)
- `[feat] T60·T61·T63 그로기 게이지 (피해 비례·벽 충돌) + Groggy 진입 + 게이지 바` —
  `Core/GroggyGauge.cs`, `Gimmick/GroggyConfig.cs`, `Gimmick/BossGroggyDriver.cs`, `Gimmick/BossGroggyBar.cs` (+ 각 `.meta`),
  `BossController.cs`, `BossAttackPatterns.cs`, `Gimmick/BossFlowDriver.cs`, `Editor/BossTestSceneBuilder.cs`
  (+ 새로 생기는 `GroggyConfig.asset`·`.meta`)
- 문서 변경(AI_RULES·CLAUDE·BACKLOG 축소 계획)이 아직 미커밋이면 같이 또는 먼저 커밋.

## 질문 (그로기)
- 피해 1당 +0.5 면 게이지 100 = 피해 200 (HP 500 의 40%). 너무 빠르거나 느리면 `GroggyConfig` 에서 조정.

## 다음 할 일
- T55 콤보 계획·실행기 (로직) → T56 (C2 = P1 → Burst, C3 = P1×3).
