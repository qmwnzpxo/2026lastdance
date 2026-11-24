# UNITY_전환_비교.md — Lua 협업 체제 → 언머신(Unity 6) 전환 검토

> 대상: 기존 LÖVE2D/Lua 5.1 갤로그 게임용 AI 협업 문서 (`AI_RULES.md`, `CLAUDE.md`, `GEMINI.md`, `AGENTS.md`, `PROMPTS.md`, `.agents/skills/*`, `docs/*`)
> 기준 기획서: `NEW____5.docx` (Unmachine 언머신 — 2D 픽셀 횡스크롤 액션·플랫포머·핵 앤 슬래시, 시네마틱)
> 작성: 2026-11-24 / 결정권자: 현민
>
> **2차 수정 (2026-11-24, 현민 지시)**
> 1. **커밋은 현민이 직접 판단해서 한다. AI는 절대 커밋하지 않는다** (git 읽기 전용 명령만).
> 2. **검증은 `run_tests.sh` 가 아니라 현민이 Unity 씬에 직접 배치해서 플레이로 한다.** AI는 Unity를 실행하지 않고 에디터 작업·씬 테스트 절차·디버그 표시를 준비한다.
> 3. **(3차) 협업 문서는 md 파일만 둔다.** pre-commit 훅·스크립트는 넣지 않는다.
> → 아래 표와 파일 목록은 이 수정을 반영한 최종본이다.

---

## 0. 결론 요약

| 구분 | 결정 |
|---|---|
| **그대로 유지** | 역할 분담(Claude 구현 / Gemini 리뷰 / 현민 판정), 기능 분해(BACKLOG), 경량 모드, 기준 커밋 기록, HANDOFF / worklog / DECISIONS 세 문서 구조, 슬래시 명령 이름, 보고 형식 |
| **교체** | Lua 5.1 호환 규칙 → **Unity 6 / C# 주의 규칙**, `luacheck`·`run_all.lua`·스모크 테스트 → **AI 정적 점검 + 현민 씬 배치 테스트**, **Claude 커밋 → 현민 직접 커밋 (AI는 커밋 제안만)**, `config.lua` → **ScriptableObject 설정 에셋**, `love.*` 분리 원칙 → **순수 C# Core 어셈블리 분리** |
| **새로 추가** | 씬·프리팹 YAML 직접 수정 금지 + HANDOFF "에디터 작업"·"씬 테스트 절차"·"커밋 제안" 칸, 디버그 표시 규칙, `scene-test-writer` 스킬, Git LFS, `GameClock`(시간 채널), `IRandom`, 기획서를 md로 쪼갠 `docs/spec/`, `spec-check` 스킬 |
| **폐기** | `lua51-guard`, `headless-test-writer`, `.luacheckrc`, `stylua.toml`, `run.sh/run.bat`, `conf.lua` 관련 규칙, AI 커밋, AI 테스트 실행 |

> **전제 확인:** 요청에 "유니티 3D"라고 썼지만 기획서는 **2D 픽셀 횡스크롤**(스프라이트 반전, 2D 지형 예외 처리)이라서 문서는 **Unity 6 + 2D(Rigidbody2D, Physics2D, URP 2D)** 기준으로 썼어요. 실제로 3D 프로젝트라면 4-7(물리), 2장 일부만 바꾸면 됩니다.

---

## 1. 기획서에서 뽑은 기술 요구사항

| 기획서 시스템 | 기술적으로 의미하는 것 | 영향을 받는 규칙/구조 |
|---|---|---|
| 상태 우선순위 (사망 > 피격 > 회피 > 공격 > 이동), 선입력 0.15초 | 우선순위 기반 플레이어 상태 머신 + 입력 버퍼 | ARCHITECTURE 3-2, 씬 확인 포인트 |
| 중력 전환 (중력 벡터 변경, 스프라이트 180° 반전, 카메라 고정) | 기본 `gravityScale` 대신 **직접 계산하는 중력 벡터** | ARCHITECTURE 3-4 |
| 프로 모션 (세계·유령 0.1배속 → 본체만 고속, 0.05초 단위 위치 기록) | `Time.timeScale` 하나로는 불가능 → **객체별 시간 채널** | AI_RULES 2장 금지, ARCHITECTURE 3-1 |
| 히트스톱, 일섬 무적 12프레임, 피격 무적 0.2초 | 프레임/초 단위 타이머가 시간 채널을 따라야 함 | GameClock |
| 적 AI: Behavior Tree, 시야·경보, 공격 슬롯 | 코드 BT + 공유 서비스(경보, 슬롯) | ARCHITECTURE 3-6 |
| 보스: FSM(BossController) + BT(BossBT) + BossAttackPatterns | 이미 정해진 3분할 구조 | ARCHITECTURE 3-7 |
| 룸 웨이브: 트리거 → 문 잠금 → 스폰 → 전멸 → 1.5초 → 개방 (UniTask 명시) | 비동기 흐름 = **UniTask**, 카메라 잠금 = Cinemachine Confiner | AI_RULES 2장, ARCHITECTURE 3-8 |
| 몬스터·상자·투사체 오브젝트 풀링 필수 | 풀 사용 규칙 (`Destroy` 금지, `Release`) | AI_RULES 2장 |
| 드랍 확률 55/30/10/5%, 보스 랜덤 패턴 교체 | 재현 가능한 난수 → `IRandom` 주입 (시드 고정으로 씬에서 같은 상황 재현) | AI_RULES 3장 |
| 스탯 표·분배 표 (모두 "임시 수치") | 현민이 코드 없이 튜닝 → **ScriptableObject** | AI_RULES 4장 |
| 시네마틱(대화형/장면형/영상형), 시네마틱 카메라, 분기 대화 | Timeline + Cinemachine, 대화는 SO 노드 그래프 | ARCHITECTURE 3-10 |
| "부분 심리스": 스테이지 1개 = 씬 1개, 룸 여러 개 | 씬 파일이 크고 충돌이 잦음 → **씬 직접 편집 금지, 씬 담당자 1명** | AI_RULES 0장, 7장 |
| 팀 4인(4대장) 프로젝트 | 커밋·브랜치는 현민이 판단, 씬은 한 번에 한 사람 | AI_RULES 7장 |

---

## 2. 기존 체제 항목별 판정

| 기존 항목 | 판정 | Unity에서 바꾸는 내용 / 이유 |
|---|---|---|
| AI_RULES 단일 원본, 24KB 제한 | ✅ 유지 | Antigravity 제약은 그대로 |
| 역할 분담 (Claude/Gemini/현민) | ✅ 유지 | 현민 칸에 **"에디터 작업(씬·프리팹 연결)"** 추가 |
| 작업 사이클 ⓪~⑥ | ✅ 유지 | ②에 "에디터 작업 목록 작성" 추가 |
| 기능 분해 크기 "파일 3개·200줄" | 🔁 조정 | C#은 같은 로직도 줄 수가 많음 → **파일 4개·300줄** 정도 |
| 경량 모드 | ✅ 유지 | 대상에 "SO 기본값 변경" 추가 |
| 0장 Lua 5.1.5 대상 | 🔁 교체 | Unity 6 / C# / 패키지 목록 |
| 2장 5.1 호환 금지표 | 🔁 교체 | Unity·C# 함정 금지표 (`async void`, `Find`, `Time.timeScale`, Unity 6 이름 변경 등) |
| 3장 코드 스타일 (snake_case) | 🔁 교체 | C# 관례 (PascalCase, `_camelCase` 필드) |
| 3장 update/draw 분리 | 🔁 대응 | Core(로직) / View(MonoBehaviour·연출) 분리로 대체 |
| 3장 시드 난수 한 곳 | ✅ 유지 | `IRandom` 인터페이스로 구현 |
| 3장 모듈/클래스 패턴 | 🔁 교체 | MonoBehaviour / 순수 클래스 / SO 템플릿 |
| 4장 config.lua (매직 넘버 금지) | 🔁 교체 | ScriptableObject 설정 에셋 |
| 4장 `love.*` 분리 | 🔁 교체 | `UnMachine.Core` 어셈블리(asmdef)에 MonoBehaviour 금지 |
| 5장 로그 모듈 + log.txt | 🔁 조정 | `GameLog` 래퍼 + Unity가 남기는 `Editor.log`/`Player.log` 사용 |
| 6장 luacheck | 🔁 교체 | AI 정적 점검 (컴파일 가능성 자체 점검 + `unity-guard` + `spec-check`). 실제 컴파일은 현민 에디터 Console |
| 6장 run_all.lua | ❌ 폐기 | 현민 씬 배치 테스트. 자동 테스트는 요청 시에만 |
| 6장 LÖVE 스모크 테스트 | 🔁 교체 | `docs/SCENE_TEST.md` (테스트 씬, 확인 순서, 디버그 표시 규칙) |
| 7장 pre-commit (luacheck + 테스트) | ❌ 폐기 | md 파일만 두기로 함. `.meta` 는 커밋 제안에서 안내, 금지 패턴은 `unity-guard` 로 점검 |
| 7장 기준 커밋 | ✅ 유지 | Claude가 착수 시 `git rev-parse` 로 기록 (읽기 전용) |
| 7장 Claude 커밋·종료 순서 | ❌ 폐기 | **AI는 커밋하지 않음.** 작업 트리를 그대로 두고 HANDOFF "커밋 제안" 작성 → 현민이 직접 커밋 |
| Gemini 리뷰 범위 `기준..HEAD` | 🔁 변경 | `git diff <기준커밋>` (작업 트리까지) + 새 파일 직접 읽기 — 현민이 커밋하기 전에도 리뷰 가능 |
| 7장 브랜치 | 🔁 변경 | 현민이 정함. AI는 브랜치를 만들지 않음 |
| `lua51-guard` 스킬 | ❌ 폐기 → `unity-guard` | |
| `review` 스킬 | 🔁 수정 | 체크리스트를 Unity 기준으로 |
| `headless-test-writer` 스킬 | ❌ 폐기 → `scene-test-writer` | 씬 테스트 절차 + 디버그 표시 작성 |
| (없음) | ➕ 추가 `spec-check` | 구현·SO 수치가 `docs/spec/` 기획과 맞는지 대조 |
| HANDOFF 템플릿 | 🔁 수정 | "에디터 작업 (현민)", "기획서 근거" 칸 추가, 실행 환경 줄 삭제 |
| worklog (날짜별 누적) | ✅ 유지 | "기획서와 다르게 구현한 점" 줄 추가 |
| DECISIONS | ✅ 유지 | 이 문서의 결정을 초기 항목으로 기록 |
| BACKLOG | ✅ 유지 | 작업 유형 칸(로직/어댑터/연출/에디터) 추가 |
| SMOKE_TEST.md | 🔁 교체 | `SCENE_TEST.md` |
| `.luacheckrc`, `stylua.toml`, `run.sh/bat`, `conf.lua` | ❌ 폐기 | `.editorconfig`(선택) |

---

## 3. 핵심 선택지 비교

### 3-1. 시간 제어 (프로 모션·히트스톱·일시정지)

| 후보 | 장점 | 단점 | 판정 |
|---|---|---|---|
| `Time.timeScale` 전역 | 쉬움, Animator·물리가 자동으로 따라감 | **세계만 0.1배속 + 본체 고속**을 표현할 수 없음. UI 애니메이션도 같이 느려짐 | 일시정지에만 사용 |
| 객체별 `GameClock` 채널 (World / Player / Ghost / UI) | 기획서 요구 그대로 구현 가능, 디버그로 배율을 바꿔 슬로우 확인 가능 | 모든 이동·타이머가 `Time.deltaTime` 대신 채널 dt를 써야 함. Animator 속도는 직접 맞춰야 함 | ✅ **채택** |

### 3-2. 비동기 흐름 (웨이브, 연출, 대화)

| 후보 | 장점 | 단점 | 판정 |
|---|---|---|---|
| 코루틴 | 기본 기능 | 반환값·취소·예외 처리가 약함 | 신규 코드 금지 |
| Unity 6 `Awaitable` | 패키지 설치 불필요 | `WhenAll` 같은 조합 기능이 부족, 기존 대화 시스템과 다름 | 보류 |
| **UniTask** | 기획서에 명시, 기존 대화·알림 시스템이 이미 사용, 취소 토큰·`WhenAll`·할당 적음 | 외부 패키지 | ✅ **채택** |

### 3-3. 튜닝 수치 보관

| 후보 | 장점 | 단점 | 판정 |
|---|---|---|---|
| `const` 상수 클래스 | 단순, AI가 수정하기 쉬움 | 현민이 수정하려면 코드 수정·재컴파일 | 수학 상수만 |
| JSON/CSV | 표 편집이 쉬움 | 로딩 코드·검증 필요 | 드랍 표가 커지면 재검토 |
| **ScriptableObject** | 인스펙터에서 플레이 중 튜닝, 보스·적 타입별 변형 에셋 | 에셋 파일(YAML)은 AI가 직접 고치지 않음 → 기본값은 코드의 필드 초기값으로 | ✅ **채택** |

### 3-4. Behavior Tree 구현

| 후보 | 장점 | 단점 | 판정 |
|---|---|---|---|
| Unity Behavior 패키지 (그래프) | 시각적 편집 | 그래프 에셋은 AI가 읽고 고치기 어렵고 diff 리뷰 불가 | ❌ |
| Behavior Designer (유료) | 기능 많음 | 비용, 위와 같은 리뷰 문제 | ❌ |
| **코드 BT (BossBT 네임스페이스 확장)** | 이미 보스에 있음, Gemini가 diff로 리뷰 가능, 노드에 디버그 표시를 붙이기 쉬움 | 시각화 없음 → 디버그 표시를 따로 만들어야 함 | ✅ **채택** |

### 3-5. 오브젝트 풀링

| 후보 | 판정 |
|---|---|
| 자체 풀 클래스 | 보스 문서의 "추적 리스트 일괄 회수" 기능만 얇게 추가 |
| **`UnityEngine.Pool.ObjectPool<T>`** (내장) | ✅ **채택** — 내장이라 검증됨. 그 위에 `TrackedPool<T>`(활성 목록 + `ReleaseAll`)만 직접 작성 |

### 3-6. 검증 방식 (2차 수정으로 결정)

| 후보 | 장점 | 단점 | 판정 |
|---|---|---|---|
| Unity Test Framework batchmode (`run_tests.sh`) | 자동, 결과가 남음 | 에디터가 열려 있으면 실행 불가, 손맛·연출은 확인 못 함 | ❌ (현민 결정) |
| **현민이 씬에 배치해서 플레이** | 실제 게임 감각까지 확인, 에디터 작업과 한 번에 | 사람 시간이 듦, 결과가 기록으로 안 남음 | ✅ **채택** |

보완책 (사람 검증의 단점을 줄이기 위해):
- Claude가 HANDOFF에 **씬 테스트 절차 표**(조작 → 기대 결과 → 결과 칸)를 쓴다 → 무엇을 봐야 할지 명확
- 눈에 안 보이는 값은 **디버그 표시**(Gizmos, 디버그 라벨, `Debug_` 컨텍스트 메뉴, `GameLog`)를 코드로 같이 준비 → 경계 상황(체력 정확히 50% 등)을 바로 만들 수 있음
- 현민이 결과를 말해 주면 Claude가 HANDOFF "현민 씬 테스트 결과"에 기록 → 기록이 남음
- 로직은 계속 Core 순수 클래스로 분리 → 나중에 자동 테스트가 필요해지면 바로 붙일 수 있음

### 3-7. 커밋 (2차 수정으로 결정)

| 후보 | 판정 |
|---|---|
| Claude가 작업 단위로 커밋 (Lua 방식) | ❌ (현민 결정) — 현민이 변경을 직접 보고 커밋 단위를 정함 |
| **AI는 git 읽기 전용, 커밋 제안만** | ✅ **채택** — HANDOFF "커밋 제안"에 메시지·파일 목록·`.meta` 안내 |
| pre-commit 훅 | ❌ md 파일만 두기로 함 (3차 수정) |

### 3-8. 씬·프리팹 편집

| 후보 | 판정 |
|---|---|
| AI가 `.unity`/`.prefab` YAML 직접 수정 | ❌ fileID·GUID 깨짐, 리뷰 불가, 팀 충돌 |
| AI가 에디터 스크립트로 자동 배치 | 반복 작업(룸 50개 트리거 배치 등)에만 예외로 허용, 현민 승인 필요 |
| **AI는 코드만, 연결은 현민이 "에디터 작업" 목록대로** | ✅ **채택** |

### 3-9. 기획서 참조 방식

| 후보 | 판정 |
|---|---|
| docx를 매번 첨부 | ❌ 토큰 낭비, 버전 혼동 |
| **시스템별 md로 쪼갠 `docs/spec/*.md`** (보스는 이미 `docs/spec/boss.md`로 완료) | ✅ **채택** — `/task`에 "기획서 근거: spec/boss.md 7장"처럼 지목, `spec-check`로 대조 |

### 3-10. 작업로그 구조

| 후보 | 판정 |
|---|---|
| 세션별 파일 (`worklogs/NNN_날짜_session.md`) | 파일이 너무 많아짐 |
| 단일 누적 파일 | 길어져서 AI가 다 읽음 → 토큰 낭비 |
| **날짜별 파일 + HANDOFF(현재) + DECISIONS(결정)** | ✅ **유지** — Lua에서 잘 돌아간 구조 |

---

## 4. 최종 채택 파일 목록

```
AI_RULES.md                         공통 규칙 (Unity 판, 24KB 이하)
CLAUDE.md / GEMINI.md / AGENTS.md   역할 진입점 (커밋 금지·Unity 실행 금지 명시)
PROMPTS.md                          지시문 (기획서 근거·에디터 작업·씬 테스트·커밋 제안, 12번 기획서 변환)
docs/ARCHITECTURE.md                기획서 기반 코드 구조 + 씬 확인 포인트 + 부록 A 코드 템플릿
docs/HANDOFF.md                     인수인계 템플릿 (에디터 작업, 씬 테스트 절차·결과, 커밋 제안)
docs/BACKLOG.md                     기능 분해 템플릿 (완료 기준 = 씬에서 볼 동작)
docs/DECISIONS.md                   이 문서의 결정을 초기 기록
docs/SCENE_TEST.md                  씬 배치 테스트 가이드 (테스트 씬, 확인 순서, 디버그 표시 규칙)
docs/worklog/TEMPLATE.md            작업로그 템플릿
docs/spec/README.md, boss.md        기획서 md 분할
.claude/skills/ + .agents/skills/   unity-guard, review, spec-check, scene-test-writer
```

`.claude/commands/*`(슬래시 명령)와 `.claude/agents/*` 는 이름은 그대로 두고 본문에서 아래만 바꾸면 됩니다.
- `git commit` / `[docs] HANDOFF 갱신` 커밋 단계 → **전부 삭제**, 대신 "HANDOFF '커밋 제안' 작성"
- `luacheck .`, `lua tests/run_all.lua` → "정적 점검 (`unity-guard`, `spec-check`) + HANDOFF '씬 테스트 절차' 작성"
- `lua51-guard` → `unity-guard`, `headless-test-writer` → `scene-test-writer`
- `config.lua` → 해당 `*Config` ScriptableObject
- `test-runner` 서브에이전트 → 삭제 (실행할 테스트가 없음)

---

## 5. 현민이 정해야 할 것

1. **2D 확인:** 언머신은 2D 프로젝트가 맞는지 (3D면 물리·카메라 항목을 고쳐야 함)
2. **팀원 참여:** 지금도 팀원 4명이 같은 저장소에 커밋하는지 (씬 담당자 규칙이 필요한지)
3. **테스트 씬:** `Test_Sandbox`, `Test_Boss` 같은 샌드박스 씬을 만들지, 실제 스테이지 씬에서 바로 확인할지
4. **설치된 패키지:** Input System, Cinemachine 3.x, UniTask가 이미 들어가 있는지
5. **기존 코드 위치:** `Assets/` 아래 실제 폴더 구조 → ARCHITECTURE 2장의 폴더 구조를 맞출지, 문서를 현재 구조에 맞출지
