# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: 패턴 선택기 Core (`PatternSelector`·`DistanceBandTracker`·`PatternTableConfig`)
- BACKLOG 작업 ID: T37
- 기획서 근거: boss.md 11장(거리 구간·후보 표·체류 보정·반복 방지), 6장 P6(대체 P5), 17장 #8·#10
- 작성자: Claude
- 작성 일시: 2026-11-26
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `9828d2b` (T37 착수 시 기록)
- 커밋 상태: 미커밋 (새 파일 7개 + docs)
- 상태: 🎮 현민 확인 대기 (컴파일·SO 에셋 생성만. 선택 동작은 T38 씬에서 확인)
- 이전 작업: T33 — 커밋됨(`9828d2b`). 1차 로그 확인 통과 항목 있음. Gemini 리뷰 전

## 0. 이전 작업 리뷰 범위 (Gemini)
- T33 은 `git diff 42cd5dd 9828d2b` (요청 사항은 `9828d2b` 의 HANDOFF 9장).
- T32 는 `git diff 4b49e82 42cd5dd`, T31 은 `git diff 82d87de 4b49e82`, T29·T30 은 `git diff ae589ab 82d87de`, T27·T28·T22 는 `git diff d60f4f9 ae589ab`.

## 1. 목표
- 거리 구간(근 x<5 / 중 5≤x<20 / 원 x≥20) × 페이즈별 가중치로 다음 선택 단위(P1~P6·C1~C4)를 고르는 **순수 로직**.
- 체류 보정(같은 구간 3초 이상 → 근:P1, 원:P2 ×2), 반복 방지(같은 단위 2연속이면 다음엔 제외, 콤보 = 단위 1), 사용 불가 대체(P6 → P5), `IRandom` 주입.
- 범위 밖: Driver 연결·PatternRegistry·디버그 표시·강제 선택(T38), 콤보 실행(T55~). **기존 파일은 하나도 안 건드림** → 게임 동작 변화 없음.

## 2. 변경 파일 (전부 새 파일)
| 파일 | 계층 | 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/Core/SelectionUnit.cs` | Core | enum P1~P6, C1~C4 (직렬화 값 — 끝에만 추가) |
| `Assets/LHM/0_Script/Boss/New/Core/DistanceBand.cs` | Core | enum Near / Mid / Far |
| `Assets/LHM/0_Script/Boss/New/Core/PatternWeight.cs` | Core | readonly struct (단위, 가중치 ≥ 0) |
| `Assets/LHM/0_Script/Boss/New/Core/DistanceBandTracker.cs` | Core | `Classify(|dx|)`, `Update(|dx|, dt)` → `Band`·`DwellSeconds`(구간 바뀐 프레임 0), `Reset()` |
| `Assets/LHM/0_Script/Boss/New/Core/PatternTable.cs` | Core | 페이즈(1·2) × 구간 6개 목록 + 체류 3초·×2. 생성 시 검증(빈/전부 0/단위 중복 → `ArgumentException`)·복사 |
| `Assets/LHM/0_Script/Boss/New/Core/PatternSelector.cs` | Core | `TrySelect(phase, band, dwell, out unit)`, `ResetHistory()`, 디버그용 `LastCandidates`·`LastDwellBonusApplied`·`LastWasSubstituted`·`LastSourceUnit`·`LastRepeatBanRelaxed`·`TryGetBannedUnit` |
| `Assets/LHM/0_Script/Boss/New/Gimmick/PatternTableConfig.cs` | Runtime (SO) | 11장 표 초기값, 거리 5/20, 체류 3/×2, `ToTable()`, `CreateDistanceBandTracker()`. 메뉴 `UnMachine/Config/Pattern Table` |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T37 → 🎮 |

- 파일 수 7개: BACKLOG 4개 → "파일 하나에 public 타입 하나" 때문에 enum·struct 분리 (현민 승인 2026-11-26).
- 테스트 씬 툴(`BossTestSceneBuilder`) **수정 없음**: 씬에 붙일 컴포넌트가 없다. T38 에서 선택기 연결과 함께 갱신(필요하면 `PatternTableConfig` 에셋 자동 생성 포함).

## 3. 동작 변화와 리스크
- **게임 동작 변화 없음** (아무도 호출하지 않음).
- `TrySelect` 순서: 표 가중치 → 체류 보정(원본 단위 기준) → 사용 불가면 대체(P6 → P5, 대체도 불가면 제외) → 반복 방지(**대체 후 단위** 기준) → `Value01 × 합계` 누적 추첨 → 기록.
  - 반복 금지 때문에 후보가 0개면 반복 방지를 풀고 다시 고름 (`LastRepeatBanRelaxed`). 그래도 0개(전부 사용 불가)면 `false`, 기록 안 바뀜.
  - 같은 구간에 P5 가 이미 있고 P6 이 P5 로 대체되면 후보 목록에 P5 가 두 줄 → 사실상 확률 합산 (2P 중거리: P5 3 + 1).
- 결정성: 같은 시드 `SystemRandom(seed)` + 같은 입력 순서 → 같은 선택 순서. 체류 시간은 어댑터가 World dt 로 넣음.
- 할당: 후보 리스트 재사용. `TrySelect` 는 패턴마다 1회 (매 프레임 아님). `Update(dx, dt)` 는 할당 없음.
- 리스크:
  - SO 인스펙터에서 목록을 비우거나 가중치를 전부 0 으로 두면 `ToTable()` 이 예외 → T38 어댑터에서 `GameLog.Error` 후 비활성 처리 예정.
  - 1페이즈 표에 콤보·P6 를 넣어도 막지 않음 (표 그대로 사용).

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 | 문제 없음으로 판단. asmdef 없음(전부 Assembly-CSharp) → `BossFlowMachine.RequireNonNegativeFinite`(internal) 접근 가능. `UnMachine.Core.Boss` 에서 상위 `UnMachine.Core.IRandom` 해석. 같은 이름 타입 기존에 없음(Grep). `Array.Empty`·`?? throw`·`out var` 계열 C# 9 범위. SO 의 `[Serializable] private struct Entry` + 배열 필드 초기값 → 새 에셋 생성 시 기본값 들어감. 120자 초과 줄 없음(PowerShell 글자 수) |
| unity-guard | 문제 없음. Core 파일 `UnityEngine` 참조 0. `UnityEngine.Random`·`Time`·`Debug.Log`·코루틴·async 없음. `public` 필드 없음(`[SerializeField] private`). 문자열 보간은 예외 메시지·`ToString` 만 |
| spec-check | ✅ 11장 거리 5/20 (경계값은 바깥 구간: 5 → 중, 20 → 원) / ✅ 1P 표 P3(5) P1(3) P2(1) · P5(4) P1(3) P2(3) · P2(5) P4(3) P5(2) / ✅ 2P 표 11장 값 그대로 (〔임시값〕 표시) / ✅ 체류 3초 이상 ×2, 근→P1·원→P2 / ✅ 반복 방지 2연속 → 제외, 콤보 = 단위 1 (17장 #10) / ✅ P6 → P5 대체 (6장 P6, BACKLOG T37) |

## 5. 에디터 작업 (현민)
1. Unity Console 컴파일 에러 0 확인 → 새 `.cs` 7개의 `.meta` 생성됨.
2. (선택, T38 전에 미리) Project 창 원하는 Config 폴더에서 우클릭 → `Create > UnMachine > Config > Pattern Table` → `PatternTableConfig.asset`.
   - T38 테스트 씬 툴이 에셋이 없으면 만들게 할 예정이라 지금 안 만들어도 된다.

## 5-1. 확인 절차 (현민)
> 선택기 동작(시드 재현, 체류 ×2 표시, 3연속 0회)은 BACKLOG 완료 기준대로 **T38 씬**에서 확인한다. 이번엔 아래만.

| # | 조작 | 기대 결과 | 근거 |
|---|---|---|---|
| 1 | 에디터 포커스 → 컴파일 | Console 에러 0, 경고 중 새 파일 이름 없음 | — |
| 2 | 위 2번으로 에셋 생성 → 인스펙터 | 거리 5 / 20, 체류 3 / 2, 1페이즈 3목록·2페이즈 3목록이 11장 표와 같음 (단위 드롭다운 P1~C4) | 11장 |
| 3 | 원거리 하한을 3 으로 입력 | 근거리 상한(5)으로 자동 보정 | OnValidate |
| 4 | 기존 Test_Boss 씬 플레이 (선택) | T33 과 같은 동작 (변화 없음) | 회귀 |

**Console 에 나오면 안 되는 것**: `CS` 컴파일 에러, `The type or namespace name 'SelectionUnit'…`, SO 직렬화 경고(`Serialization depth`).

## 5-2. 현민 확인 결과
- (대기)

## 6. 기획서와 다르게 한 점
| 기획서 / BACKLOG | 구현 | 이유 |
|---|---|---|
| BACKLOG 파일 4개 | 7개 (enum·struct 분리) | AI_RULES 3장 "파일 하나에 public 타입 하나", 현민 승인 |
| (명시 없음) 반복 방지로 후보가 0개 | 반복 허용 | 보스가 멈추지 않게. v1 "2연속 허용, 3번째 교체"와 충돌 없음 (후보 ≥ 2 면 3연속 0회 보장) |
| (명시 없음) 반복 방지 판정 단위 | 대체 후 단위 | P6 → P5 대체가 끼어도 같은 패턴 3연속 금지 |

## 7. 결정 사항
- Core 는 SO 를 모른다: `PatternTableConfig.ToTable()` → `PatternTable`, `CreateDistanceBandTracker()` → `DistanceBandTracker`.
- 사용 가능 판정은 생성 시 `Func<SelectionUnit, bool>` 한 번 주입 (T38·T50 에서 드론 수로 P6 판정).
- 체류 보정·대체 규칙은 기획서 고정 규칙이라 Core 상수 매핑 (`TryGetDwellEscapeUnit`, `TryGetSubstitute`), 수치(3초·×2·5·20)는 SO.

## 8. 알려진 문제 / 미완료
- 선택기 연결·디버그 표시·강제 선택 = T38, 콤보 실행 = T55~, P6 드론 판정 = T50.
- (T33 에서 이어짐) 전환 연출 T34, 자물쇠 T21, Animator 8~12 T36.

## 9. 리뷰어에게 요청
- 반복 방지를 **대체 후 단위**로 세는 것, 후보 0개면 반복을 허용하는 것.
- 체류 보정을 대체 전 원본 단위에 붙이는 것 (P1·P2 는 대체 대상이 아니라 현재 차이 없음).
- `DistanceBandTracker` 가 구간이 바뀐 프레임의 dt 를 체류에 넣지 않는 것.
- `PatternTable` 검증 범위(빈 목록·전부 0·단위 중복)가 적절한지.
- (0장) T33 이전 범위.

## 9-1. 규칙 변경 제안 (현민 판단, T31 에서 이어짐)
- AI_RULES 0장 2번 예외에 추가: "테스트 씬 툴은 필요한 **Config SO 가 프로젝트에 없을 때만** 기본값으로 **새 `.asset`** 을 만들 수 있다 (기존 에셋 수정·덮어쓰기 금지)."
- 작업 절차에 추가: "씬 테스트가 필요한 작업은 `BossTestSceneBuilder`(테스트 씬 툴)도 함께 갱신해, 메뉴 한 번으로 그 작업을 테스트할 수 있는 새 Test_* 씬이 나오게 한다." (현민 요청 2026-11-26)

## 10. 질문 (현민 답변 필요)
- (T37) 반복 방지로 후보가 하나도 안 남으면 반복 허용 — 괜찮은지 (기본: 허용).
- (T37) 2P 중거리처럼 P6 가 P5 로 대체되면 P5 확률이 합산됨 (3+1) — 괜찮은지 (기본: 합산).
- (T37) 페이즈 전환 때 반복 방지 기록을 지울지 (기본: 유지 — 1P 마지막 2개가 2P 첫 선택에 영향).
- (T33) AirBurst·Bounce 펠릿이 전환 때 남으면 추적·회수를 추가할지 (기본: 그대로).
- (T33) 2P 기믹 시작(`PhaseChanged`)이 전환 끝(1.5초 뒤)이어도 괜찮은지 (기본: 끝날 때).
- (T32) 정지 거리 1.5 유지? 기획서 4장 표에 넣을지 (기본: Config 1.5 유지).
- (T32) 2P Resting 이동도 걷기(속도 3)로 충분한지 (기본: 걷기).
- (T22) 체력바 색상 경계도 `>= 0.5f` 로 맞출지 (기본: 그대로).
- (T28) Player 채널 동기화 유지 여부 (기본 유지).
- (T28) `GameClockDriver` 의 `Time.unscaledDeltaTime` 상한 여부 (기본: 그대로).
- (T31) 패턴이 Stunned·Groggy 로 끊길 때 이미 쏜 발사체를 남길지 지울지 (기본: 그대로).

## 11. 다음 할 일
1. 현민: 컴파일 확인 → (선택) 에셋 생성·인스펙터 확인 → 커밋 → Gemini 리뷰
2. 다음 작업: T38 ✂️ (선택기 연결 + 레거시 영구 매핑 + 레거시 자체 반복 방지 끔, 디버그는 Driver 인스펙터만) — BACKLOG "1주 축소 계획"(2026-11-26) 기준

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- `[feat] T37 패턴 선택기 Core: 거리 구간·체류 보정·반복 방지·P6 대체 + PatternTableConfig` —
  `Assets/LHM/0_Script/Boss/New/Core/SelectionUnit.cs`,
  `Assets/LHM/0_Script/Boss/New/Core/DistanceBand.cs`,
  `Assets/LHM/0_Script/Boss/New/Core/PatternWeight.cs`,
  `Assets/LHM/0_Script/Boss/New/Core/DistanceBandTracker.cs`,
  `Assets/LHM/0_Script/Boss/New/Core/PatternTable.cs`,
  `Assets/LHM/0_Script/Boss/New/Core/PatternSelector.cs`,
  `Assets/LHM/0_Script/Boss/New/Gimmick/PatternTableConfig.cs`,
  `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md`
- **새 `.cs` 7개의 `.meta` 함께 커밋** (에디터 열면 Unity가 생성). 에셋을 만들었으면 `PatternTableConfig.asset` + `.meta` 도.
