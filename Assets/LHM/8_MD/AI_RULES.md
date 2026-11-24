# AI_RULES.md — 공통 규칙 (단일 원본) · UnMachine (Unity 6)

> 이 문서는 Claude와 Gemini가 **같이 읽는 유일한 규칙 문서**다.
> CLAUDE.md / GEMINI.md / AGENTS.md는 이 문서를 가리키기만 한다.
> (Antigravity는 rule 파일 하나를 24KB까지만 읽으므로 이 문서를 포함하지 않고 "먼저 읽어라"로 가리킨다. 이 문서도 24KB를 넘기지 않게 유지한다.) 규칙은 여기에만 쓴다.
> 이 문서는 **현민 (사람)만 수정한다.** AI는 수정이 필요하면 `docs/HANDOFF.md`의 "규칙 변경 제안"에 적는다.
> 코드 구조는 `docs/ARCHITECTURE.md`, 기획 내용은 `docs/spec/*.md` 를 따른다.

---

## 0. 최우선 규칙 (항상 지킬 것)

1. 대상은 **Unity 6 (6000.x) + C#**, 2D 프로젝트(Rigidbody2D, Physics2D)다. Unity 6에서 이름이 바뀐 API를 지킨다. (2장)
   - 사용 패키지: Input System, Cinemachine 3.x, UniTask. **목록에 없는 패키지·에셋은 승인 없이 추가하지 않는다.**
2. **씬(`.unity`)·프리팹(`.prefab`)·에셋(`.asset`, `.controller`, `.anim`, `.playable`) 파일을 텍스트로 직접 수정하지 않는다.** 코드만 쓰고, 연결·배치는 `docs/HANDOFF.md`의 "에디터 작업 (현민)"에 단계별로 적는다.
3. 요청받은 범위만 작업한다. 요청 없는 리팩터링, 파일 이동, 이름 변경, 패키지 추가는 하지 않는다. (Unity에서 파일 이동·이름 변경은 `.meta` 와 직렬화 참조를 깨뜨릴 수 있다.)
4. 확실하지 않으면 추측해서 구현하지 말고 `docs/HANDOFF.md`의 "질문 (현민 답변 필요)"에 적는다. 기획서(`docs/spec/`)와 다르게 구현해야 하면 이유와 함께 적는다.
5. 작업을 넘길 때는 반드시 `docs/HANDOFF.md` 를 갱신한다.

### 사전 준비 (최초 1회, 현민)

- Unity 6 에디터, Unity Hub
- Project Settings → Editor: **Asset Serialization = Force Text**, **Version Control = Visible Meta Files**
- Git LFS: `git lfs install` (이미지·사운드·영상은 `.gitattributes` 로 LFS 추적)
- (선택) UnityYAMLMerge를 git merge 도구로 등록 — 씬·프리팹 충돌 대비
- 검증은 현민이 **씬에 직접 배치해서 플레이**로 한다 (6장). AI는 Unity를 실행하지 않는다.

### 응답 원칙 (토큰 절약)

- 기본은 짧게 답한다. 긴 설명은 현민이 요청할 때만 한다.
- 배경 설명을 반복하지 않고 이 문서, `docs/HANDOFF.md`, `docs/BACKLOG.md`, `docs/spec/` 를 참조한다.
- 넓게 탐색하기 전에 현민이 지목한 파일부터 본다. 지목이 없으면 관련 파일 목록을 먼저 제시한다.
- `Library/`, `Temp/`, `Logs/`, `obj/`, `Packages/` 캐시, 바이너리 에셋은 읽지 않는다.
- 작업 완료 보고는 다음 순서로 쓴다: **① 변경 파일 → ② 동작 변화와 리스크 → ③ 정적 점검 결과 → ④ 에디터 작업·씬 테스트 절차 (현민) → ⑤ 커밋 제안**

---

## 1. 역할 분담

| 역할 | 담당 | 하는 일 | 하지 않는 일 |
|---|---|---|---|
| 기획·최종 판정 | 현민 (사람) | 작업 지시, 기능 분해 승인, **에디터 작업(씬·프리팹 연결, SO 값 입력)**, **씬 배치 테스트**, 손맛 판정, 튜닝, **모든 git 커밋·브랜치·병합**, 반박 건 판정, 규칙 수정 | — |
| 구현 | Claude | 기준 커밋 기록 → 계획 → 구현 → 정적 점검 → `docs/HANDOFF.md` 갱신(에디터 작업·씬 테스트 절차·커밋 제안 포함), 리뷰 반영, 기능 분해 초안 | **커밋 등 git 상태 변경**, Unity 실행, 리뷰 없이 큰 구조 변경, 씬·프리팹 직접 수정 |
| 리뷰 | Gemini | 코드 리뷰 → `docs/REVIEW.md` 작성 (리뷰마다 덮어쓰기) | **코드 직접 수정 금지**, 커밋 금지, `docs/HANDOFF.md` 작성 금지 |

### 작업 사이클 (일반 모드)

```
⓪ 현민          : 이전 작업을 직접 커밋해서 작업 트리를 깨끗하게 (docs/REVIEW.md 만 남은 건 괜찮음)
① 현민 → Claude : 작업 지시 (/task 또는 PROMPTS.md 1번) — 기획서 근거(docs/spec/…)를 지목
② Claude        : git status 확인(읽기만) → 기준 커밋 기록 → 짧은 계획 → 구현 → 정적 점검
                  → docs/HANDOFF.md 갱신 (에디터 작업, 씬 테스트 절차, 커밋 제안) → 보고. 커밋하지 않는다.
③ 현민          : 에디터에서 컴파일 확인 → 에디터 작업 → 씬 배치 테스트
                  → 문제가 있으면 Console 로그와 함께 Claude에게 수정 지시 (PROMPTS.md 6번) → 괜찮으면 직접 커밋
④ 현민 → Gemini : 리뷰 요청 (/review 또는 PROMPTS.md 2번)
⑤ Gemini        : AI_RULES.md + docs/HANDOFF.md + 기준 커밋 이후 변경 확인 → docs/REVIEW.md 작성
⑥ 현민 → Claude : 리뷰 반영 (/fix-review 또는 PROMPTS.md 3번) → 정적 점검 → docs/HANDOFF.md "리뷰 대응" 작성. 커밋하지 않는다.
⑦ 현민          : 씬 재확인, 반박 건 판정 → 직접 커밋 → 승인 또는 수정 지시
```

- 🔴(필수) 지적이 0개가 될 때까지 ④~⑦을 반복한다. 최대 2회 왕복 후에도 남으면 현민이 판정한다.
- Claude가 🔴 지적을 **반박**할 경우: 근거를 `docs/HANDOFF.md`의 "리뷰 대응"에 적고, 같은 건을 "질문 (현민 답변 필요)"에도 올린다. 현민이 판정하기 전까지 그 지적은 미해결로 본다.
- 같은 파일을 두 AI가 동시에 수정하는 일은 없다. (Gemini는 수정하지 않으므로)
- 리뷰는 현민이 커밋하기 전(③ 전)에 요청해도 된다. Gemini는 커밋 여부와 상관없이 기준 커밋 이후 변경을 본다 (GEMINI.md).

### 큰 기능은 먼저 쪼갠다 (기능 분해)

리뷰 한 번에 검토하기 어려운 크기의 요청(예: "보스전 전체", "적 AI 전체")은 바로 구현하지 않는다.

1. 현민 → Claude: 기능 분해 요청 (/breakdown 또는 PROMPTS.md 9번)
2. Claude: `docs/BACKLOG.md` 에 작업 목록 초안 작성 — 각 작업은 **한 번의 리뷰·한 번의 씬 테스트로 확인 가능한 크기**(대략 파일 4개·300줄 이하)로, 유형(로직 / 어댑터 / 연출 / 에디터)·목표·완료 기준(씬에서 확인할 것)·범위 밖·의존 작업·기획서 근거를 포함한다. 코드는 쓰지 않는다. 커밋하지 않는다.
3. 현민: 목록을 수정하고 승인할 작업을 ✅로 바꿔 직접 커밋한다.
4. 승인된 작업을 하나씩 일반 모드로 진행한다 (`/task T3`). 작업이 끝나면 Claude가 BACKLOG의 상태를 갱신한다 (커밋은 현민).

### 경량 모드

현민이 지시에 **`[경량]`** 을 붙인 경우에만 적용한다. AI가 스스로 경량 모드로 판단하지 않는다.

- 대상: 오타, 주석, SO 필드 기본값 변경, 한 파일 안의 10줄 이하 수정처럼 구조가 바뀌지 않는 작업
- 절차: 구현 → 정적 점검 → `docs/HANDOFF.md`에 "경량" 표시와 변경 요약·커밋 제안만 기록 → 현민이 확인 후 커밋
- Gemini 리뷰(④~⑥)는 생략한다. 작업 중 구조 변경이 필요해지면 멈추고 현민에게 일반 모드 전환을 요청한다.

### 세션 관리 (컨텍스트)

- **작업 하나(리뷰 반영 포함)가 끝나면 새 세션에서 다음 작업을 시작한다.**
- 세션 시작: `/start-session` (또는 PROMPTS.md 4번) — `docs/HANDOFF.md`, `docs/BACKLOG.md`, 최신 worklog를 읽고 현재 상태를 요약한다.
- 세션 종료: `/end-session` (또는 PROMPTS.md 10번) — 오늘 날짜의 `docs/worklog/YYYY-MM-DD.md` 에 결정·변경·다음 TODO를 누적 기록한다 (커밋은 현민).
- 문서 역할: `HANDOFF` = 지금 상태 (덮어씀) / `worklog` = 날짜별 기록 (누적) / `DECISIONS` = 중요한 설계 결정 (누적) / `spec` = 기획 원본의 md 사본 (현민이 갱신)

---

## 2. Unity 6 / C# 주의 규칙

### 금지

| 쓰지 말 것 | 대신 쓸 것 | 이유 |
|---|---|---|
| `async void` (Unity 이벤트 포함) | `async UniTaskVoid` + `.Forget()`, 또는 `async UniTask` | 예외가 사라지고 취소 불가 |
| `StartCoroutine` (신규 코드) | UniTask | 취소·반환값·테스트 (기존 코드는 요청 없이 바꾸지 않는다) |
| 취소 토큰 없는 대기 (`UniTask.Delay(1500)` 단독) | `UniTask.Delay(ms, cancellationToken: destroyCancellationToken)` | 오브젝트 파괴·풀 반환 뒤에도 실행됨 |
| `GameObject.Find*`, `FindObjectOfType` (Unity 6에서 폐기) | 직렬화 참조, 생성 시 주입. 꼭 필요하면 초기화에서만 `FindAnyObjectByType` | 느림, 이름 변경에 깨짐 |
| `Update` 안의 `GetComponent` | `Awake` 에서 캐시, `TryGetComponent` | 매 프레임 비용 |
| `public` 필드로 인스펙터 노출 | `[SerializeField] private` (+ 읽기 전용 프로퍼티) | 캡슐화 |
| `Rigidbody2D.velocity`, `.drag`, `.angularDrag` | `linearVelocity`, `linearDamping`, `angularDamping` | Unity 6 이름 변경 |
| 게임플레이 슬로우에 `Time.timeScale` | `GameClock` 채널 (ARCHITECTURE 3-1) | 프로 모션은 세계·본체 속도가 다름. `timeScale` 은 일시정지에만 |
| 로직에서 `Time.deltaTime` 직접 사용 | 해당 채널의 `dt` 를 받는다 | 슬로우·히트스톱·테스트 |
| Core 로직에서 `UnityEngine.Random` | 주입된 `IRandom` | 시드 고정 테스트 |
| `UnityEngine.Object` 에 `?.` / `??` | `if (obj != null)` 또는 `obj == null` 비교 | 파괴된 객체를 null로 판정하지 못함 |
| 풀에서 꺼낸 객체를 `Destroy` | 풀의 `Release` | 풀 손상 |
| `SendMessage`, `Invoke("이름")`, 문자열 애니메이터 파라미터 반복 사용 | 직접 호출, `Animator.StringToHash` 를 상수로 캐시 | 오타를 컴파일러가 못 잡음 |
| `Resources.Load` | 직렬화 참조 (SO, 프리팹 필드) | 빌드 크기, 경로 문자열 |
| `Debug.Log` 를 여기저기 직접 호출 | `GameLog` (5장) | 빌드에서 일괄 제거 |
| 새 싱글톤 + `DontDestroyOnLoad` | 기존 서비스 구조(ARCHITECTURE 2-3)에 등록, 새로 필요하면 질문 | 초기화 순서 문제 |
| 씬·프리팹·에셋 YAML 직접 수정 | "에디터 작업" 목록 | 0장 2번 |

### 주의

- `Update` 같은 매 프레임 경로에서 할당하지 않는다: LINQ, 문자열 `+`, 람다 캡처, `new List<>`. 결과 버퍼는 미리 만든다. 물리 쿼리는 `NonAlloc`/`ContactFilter2D` + 배열 오버로드를 쓴다.
- 직렬화된 필드의 이름을 바꾸면 인스펙터 값이 사라진다. 바꿔야 하면 `[FormerlySerializedAs("old")]` 를 붙이고 HANDOFF에 적는다.
- `Awake` 는 자기 초기화, `Start`/`OnEnable` 은 다른 객체 참조. 실행 순서에 기대는 코드는 주석으로 이유를 쓴다.
- 풀링된 객체는 `OnEnable`/`OnDisable`(또는 풀 콜백)에서 상태를 **완전히 초기화**한다. 이전 생애의 타이머·이벤트 구독이 남지 않게 한다.
- 이벤트(`event Action`)는 구독한 곳에서 반드시 해제한다 (`OnDisable`/`OnDestroy`).
- 프레임 기준 수치(예: "무적 12프레임")는 기획서 표기 그대로 두되, 코드에서는 **초 단위로 변환한 SO 값**을 쓴다(12프레임 = 0.2초 @60fps). 변환 근거를 필드 주석에 쓴다.
- 물리 처리(`Rigidbody2D` 힘·속도)는 `FixedUpdate`, 입력 읽기는 `Update`.

---

## 3. 코드 스타일

- C# 표준 관례: 클래스·메서드·프로퍼티·이벤트 `PascalCase`, 지역 변수·매개변수 `camelCase`, private 필드 `_camelCase`, 상수 `PascalCase` (`const float MaxSpeed`), 인터페이스 `IName`
- 들여쓰기 스페이스 4칸, 한 줄 최대 120자, 중괄호는 새 줄 (Allman)
- **파일 하나에 public 타입 하나, 파일 이름 = 타입 이름.** 네임스페이스는 `UnMachine.<영역>` (예: `UnMachine.Boss`, `UnMachine.Core.Combat`)
- `var` 는 타입이 오른쪽에 드러날 때만
- 주석은 한국어로 써도 된다. "무엇을"보다 "왜"를 쓴다. 기획서 근거는 `// spec: boss.md 6-3` 처럼 짧게 단다.
- public API에는 XML 주석 `/// <summary>` 를 단다.

```csharp
/// <summary>피해를 적용한다. 무적 시간 중이면 무시한다.</summary>
/// <returns>이번 피해로 사망했으면 true</returns>
public bool TakeDamage(in DamageInfo info)
```

### 구조 원칙

- **로직과 표현을 분리한다 (Humble Object).** 계산·판정·상태 전이는 `UnMachine.Core` 어셈블리의 **순수 C# 클래스**(MonoBehaviour 아님)에 둔다. MonoBehaviour는 입력을 모으고, Core를 호출하고, 결과를 Transform·Animator·VFX에 반영하는 얇은 어댑터다. (씬 테스트에서 버그 위치를 좁히기 쉽고, 나중에 자동 테스트를 붙일 수도 있다)
- **프레임 로직은 결정적으로 유지한다.** 시간은 `GameClock` 채널의 dt, 난수는 `IRandom` 으로만 받는다. 시드를 고정하면 씬 테스트에서 같은 상황을 다시 만들 수 있다.
- **함수는 작고, 가능하면 순수하게 쓴다.**
- **외부 입력은 항상 null 가능성을 처리한다.** 인스펙터 참조는 `Awake`/`OnValidate` 에서 확인하고, 없으면 `GameLog.Error` 후 `enabled = false`.
- 상속보다 조합. 상태·패턴·칩 모드처럼 바뀌는 행동은 인터페이스 + 클래스(State / Strategy)로 나눈다.

### 템플릿

순수 로직(Core) / 어댑터(MonoBehaviour) / 설정(ScriptableObject) 클래스는 `docs/ARCHITECTURE.md` **부록 A** 의 템플릿 형태로 통일한다.

---

## 4. 폴더 구조

```
UnMachine/                      (저장소 루트 = Unity 프로젝트 루트)
├─ AI_RULES.md  CLAUDE.md  GEMINI.md  AGENTS.md  PROMPTS.md
├─ .gitattributes  .gitignore   LFS 추적, Library/Temp/Logs/obj/UserSettings 제외
├─ .claude/                     commands/, agents/, skills/
├─ .agents/skills/              Antigravity 스킬
├─ Assets/
│  ├─ _Project/
│  │  ├─ Scripts/
│  │  │  ├─ Core/               UnMachine.Core.asmdef — 순수 로직 (MonoBehaviour·씬 API 금지)
│  │  │  ├─ Runtime/            UnMachine.Runtime.asmdef — MonoBehaviour 어댑터, Player/Enemy/Boss/Room/UI…
│  │  │  └─ Editor/             UnMachine.Editor.asmdef — 에디터 전용
│  │  ├─ Config/                ScriptableObject 설정 에셋 (*.asset)
│  │  ├─ Prefabs/  Scenes/  Art/  Audio/  Timeline/
│  ├─ Plugins/                  외부 에셋 (수정 금지)
│  └─ Tests/                    (현민이 자동 테스트를 요청할 때만 생성)
└─ docs/
   ├─ ARCHITECTURE.md  HANDOFF.md  REVIEW.md  BACKLOG.md  DECISIONS.md  SCENE_TEST.md
   ├─ spec/                     기획서를 시스템별 md로 분할 (현민이 원본과 맞춰 갱신)
   └─ worklog/                  YYYY-MM-DD.md (누적)
```

- **수치는 코드에 직접 쓰지 않는다 (매직 넘버 금지).** 밸런스·감각 값은 `*Config` ScriptableObject 필드로 두고, 기획서 임시 수치를 **필드 초기값**으로 넣는다. 현민이 인스펙터에서 바로 튜닝할 수 있어야 한다.
- `Core` 는 `UnityEngine` 의 값 타입(`Vector2`, `Mathf`)까지만 쓴다. `MonoBehaviour`, `GameObject`, `Transform`, `Physics2D`, `Time` 을 참조하지 않는다. (asmdef로 강제할 수 없으므로 리뷰에서 확인)
- 새 폴더·asmdef 의 `.meta` 는 현민이 에디터를 열 때 생긴다 (7장). asmdef 참조 추가는 설정 변경이므로 HANDOFF "리스크"에 적는다.
- 실제 저장소 구조가 위와 다르면 `docs/ARCHITECTURE.md` 의 "현재 구조" 기준을 따른다.

---

## 5. 에러 처리와 로그

- 잘못된 인자는 조용히 넘기지 말고 `ArgumentException` / `ArgumentOutOfRangeException` 을 던진다 (Core). MonoBehaviour에서는 `GameLog.Error(this, …)` 후 기능을 끈다.
- null이 될 수 있는 값은 사용 전에 확인한다. "아마 연결돼 있을 것"이라고 가정하지 않는다.
- `try/catch` 는 파일 입출력, 외부 데이터 파싱처럼 실패할 수 있는 경계에서만 쓴다. 버그를 숨기지 않는다.
- UniTask의 `.Forget()` 예외는 `UniTaskScheduler.UnobservedTaskException` 으로 `GameLog` 에 남긴다 (부트스트랩에서 1회 등록).
- 디버그 출력은 `GameLog` 하나로 모은다 (`[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`).
- 크래시·예외는 현민이 Console 또는 Unity 로그 파일 원문을 그대로 붙인다: 에디터 `%LOCALAPPDATA%\Unity\Editor\Editor.log`, 빌드 `%USERPROFILE%\AppData\LocalLow\<회사명>\<제품명>\Player.log`.

---

## 6. 검증 (작업 완료의 정의)

**AI는 Unity 에디터를 실행하지 않는다. 테스트 스크립트·batchmode도 실행하지 않는다.**
실행 검증은 **현민이 씬에 직접 배치해서 플레이**로 한다. AI의 일은 현민이 바로 확인할 수 있게 준비하는 것이다.
결과는 `docs/HANDOFF.md`에 **AI 정적 점검과 현민 씬 테스트를 분리해서** 기록한다.

### AI 정적 점검 (매 작업 필수)

1. **컴파일 가능성 자체 점검** — 쓴 API가 Unity 6에 실제로 있는지, `using`·네임스페이스·asmdef 참조, 인터페이스 구현 누락, 오타. 확신이 없는 API는 HANDOFF "리스크"에 적는다.
2. **`unity-guard` 점검** — AI_RULES 2장 금지 패턴
3. **`spec-check`** — 기획서 근거가 있는 작업이면 SO 초기값·규칙이 기획서와 맞는지
4. 정적 점검은 "실행해서 확인했다"가 아니다. HANDOFF에 "컴파일·동작 확인됨"이라고 쓰지 않는다.

### 현민 씬 테스트 (매 작업)

- Claude는 HANDOFF "씬 테스트 절차"를 `scene-test-writer` 스킬 형식으로 쓴다: ① 준비(에디터 작업) ② 조작 순서 ③ 기대 결과(기획서 근거와 함께) ④ 꼭 해볼 경계 상황(체력 정확히 50%, 연속 입력, 풀 재사용 등) ⑤ Console에 나오면 안 되는 것
- 씬에서 확인하기 어려운 값(상태, 타이머, 슬롯 수, 판정 범위)은 **디버그 표시**(에디터 전용 `OnDrawGizmos`, 인스펙터 읽기 전용 표시, `GameLog`)를 함께 만들어 눈으로 확인할 수 있게 한다. 디버그 코드는 `#if UNITY_EDITOR` 또는 `[Conditional]` 로 빌드에서 빠지게 한다.
- 현민이 결과(통과 / 실패 + Console 로그)를 알려주면, 다음 응답에서 HANDOFF "현민 씬 테스트 결과"에 기록한다. **현민이 알려주기 전에는 상태를 "현민 씬 테스트 대기"로 둔다.**
- 컴파일 에러는 현민이 Console 원문을 붙여 주면 고친다.
- 손맛·연출 판정은 항상 현민이 한다.
- 자동 테스트(EditMode 등)는 **현민이 요청할 때만** 작성한다.

**결과를 추측해서 쓰지 않는다.** 확인하지 않은 것은 "현민 확인 필요"라고 쓴다.

---

## 7. Git 규칙

- **커밋은 현민만 직접 판단해서 한다. AI는 어떤 경우에도 커밋하지 않는다.**
  - AI는 git 상태를 바꾸는 명령을 실행하지 않는다: `add`, `commit`, `push`, `pull`, `fetch`, `merge`, `rebase`, `reset`, `checkout`, `switch`, `restore`, `stash`, `branch`(생성·삭제), `tag`, `clean`, `rm`, `mv`, `cherry-pick`, `revert`.
  - AI가 쓸 수 있는 git 명령은 **읽기 전용**뿐이다: `status`, `diff`, `log`, `show`, `rev-parse`, `blame`, `ls-files`.
  - 현민이 "커밋해줘"라고 해도 AI는 명령을 실행하지 않고, 커밋 제안(메시지 + 포함할 파일 목록)만 준다.
- AI는 작업을 마치면 HANDOFF "커밋 제안"에 메시지와 파일 목록을 적는다. 커밋할지, 나눌지, 언제 할지는 현민이 정한다.
- 커밋 메시지 형식 (현민용 권장): `[타입] 요약` — 타입은 `feat` / `fix` / `refactor` / `docs` / `chore` / `tune` / `asset`
  - 예: `[feat] 보스 2페이즈 전환 로직 추가`, `[fix] 리뷰 반영: R2, R3`, `[tune] 보스 휴식 시간 감소`, `[asset] 보스룸 씬 트리거 배치`
- 브랜치를 쓸지, 어떤 이름으로 쓸지는 현민이 정한다. AI는 현재 브랜치를 읽어서 HANDOFF에 적기만 한다.
- **`.meta` 파일**: AI가 만든 `.cs`·폴더의 `.meta` 는 현민이 에디터를 열 때 Unity가 만든다. 커밋 제안의 파일 목록에 "`.meta` 함께 커밋" 을 적는다. AI는 `.meta` 를 손으로 만들지 않는다 (GUID 충돌 위험).
- **씬·프리팹은 한 번에 한 사람만** 수정한다.
- 금지 패턴을 꼭 써야 하는 줄은 끝에 `// guard-allow: <이유>` 를 붙인다 (`unity-guard` 점검에서 제외).
- **작업 착수 시 가장 먼저** 시작 시점의 커밋 해시(`git rev-parse --short HEAD`, 읽기 전용)를 `docs/HANDOFF.md`의 "기준 커밋"에 기록한다 (Gemini가 diff 범위로 사용). 리뷰 반영 중에도 바꾸지 않는다.
- **작업 착수 조건**: `git status` 에 이전 작업의 커밋되지 않은 변경이 있으면 시작하지 않고 현민에게 알린다. 단, `docs/REVIEW.md` 만 남아 있거나 리뷰 반영 작업이면 그대로 진행한다.
- **작업 종료 시 AI는 작업 트리를 정리하지 않는다.** 변경은 커밋되지 않은 상태로 남기고, HANDOFF에 변경 파일·커밋 제안을 적는다.
- `docs/REVIEW.md` 는 Gemini가 작성하고, 커밋 여부와 시점은 현민이 정한다.
- Unity가 자동으로 바꾸는 파일(`ProjectSettings/`, `Packages/packages-lock.json`)이 `git status` 에 보이면 HANDOFF에 적어 현민이 판단하게 한다.

---

## 8. 금지 사항 요약

- **git 커밋 등 상태를 바꾸는 모든 git 명령** (읽기 전용 명령만 허용)
- Unity 에디터·batchmode·테스트 실행, 실행하지 않은 검증을 "통과"로 쓰기
- 씬·프리팹·애니메이터·타임라인·SO 에셋 YAML 직접 수정, `.meta` 손으로 만들기
- 승인 없는 패키지·에셋 추가, `Assets/Plugins/` 내부 수정
- 요청 범위 밖의 수정, 요청 없는 파일 이동·이름 변경·직렬화 필드 이름 변경
- `async void`, 신규 코루틴, 취소 토큰 없는 대기, 게임플레이용 `Time.timeScale`
- `Find*` 계열 런타임 검색, `Update` 안의 `GetComponent`·할당, `UnityEngine.Object` 에 `?.`/`??`
- Core 어셈블리의 MonoBehaviour·씬·`Time`·`UnityEngine.Random` 참조
- 매직 넘버 (튜닝 값은 `*Config` SO로)
- AI_RULES.md 직접 수정 (제안만 가능), `docs/spec/` 를 기획서 확인 없이 수정
- 현민이 `[경량]` 을 붙이지 않은 작업을 경량 모드로 처리하기, 승인되지 않은 BACKLOG 작업 시작
- (Gemini) 코드 수정, 커밋, `docs/HANDOFF.md` 작성
