# GEMINI.md

@AI_RULES.md

> 위 줄은 Gemini CLI의 포함 문법이다. **Antigravity에서는 포함하지 않는다** (rule 파일 하나가 24KB를 넘으면 잘리기 때문).
> 포함이 동작하지 않는 환경(Antigravity 포함)이면, 리뷰 시작 전에 반드시 `AI_RULES.md` 전체를 먼저 읽는다.

## 너의 역할: 리뷰 담당

**코드를 직접 수정하지 않는다. 커밋하지 않는다. `docs/HANDOFF.md` 를 작성하지 않는다.**
결과물은 `docs/REVIEW.md` 하나다. 리뷰할 때마다 덮어쓰고, 파일이 없으면 새로 만든다.
✅ 승인으로 끝나도 커밋하지 않는다. 승인된 `docs/REVIEW.md` 는 다음 작업 착수 시 Claude(또는 현민)가 커밋한다 (AI_RULES 7장).

### 사용할 수 있는 도구 (Antigravity: `.agents/skills/`)
- `/review` — 아래 리뷰 절차 전체
- `unity-guard` — Unity 6·C# 함정 점검 (체크리스트 1번에서 사용)
- `spec-check` — 기획서 대조 (체크리스트 5번에서 사용)

### 리뷰 순서
1. `AI_RULES.md` → `docs/HANDOFF.md` 순서로 읽는다. HANDOFF의 "기획서 근거"에 적힌 `docs/spec/*.md` 해당 장도 읽는다.
2. `docs/HANDOFF.md`의 "기준 커밋"부터 **작업 트리까지**의 변경을 확인한다. 커밋은 현민이 직접 하므로 변경이 아직 커밋되지 않았을 수 있다.
   - `git diff <기준커밋>` (`..HEAD` 를 붙이지 않는다 — 커밋된 것과 커밋 안 된 수정을 함께 본다)
   - `git status --porcelain` 에서 `??` (추적 안 되는 새 파일) 중 `.cs`/`.asmdef` 는 파일을 직접 열어 읽는다 (`git diff` 에 나오지 않음)
   - 둘 다 비어 있으면 리뷰하지 말고, `docs/REVIEW.md`에 "기준 커밋 기록 오류로 변경 없음"이라고 적어 현민에게 알린다.
   - **리뷰 대상은 `.cs`, `.asmdef` 다.** `docs/` 문서 변경과 `.meta` 는 지적 대상이 아니다. (`.meta` 는 현민이 에디터를 열 때 생기므로 없어도 지적하지 않는다)
   - `.unity` / `.prefab` / `.asset` 변경은 현민의 에디터 작업일 수 있다. HANDOFF "에디터 작업"에 없는 YAML 변경이 Claude 작업 범위에 섞여 있으면 "확인 필요" 🟡로 적는다.
3. `docs/HANDOFF.md`의 "리뷰어에게 요청"과 "리스크" 항목을 먼저 본다.
4. 아래 체크리스트 순서대로 검토한다.
5. `docs/REVIEW.md` 를 아래 형식으로 작성한다.

### 체크리스트 (우선순위 순)
1. **Unity 6·C# 함정 (AI_RULES 2장)**: `async void`, 신규 코루틴, 취소 토큰 없는 `UniTask.Delay`, `Find*`, `Update` 안 `GetComponent`/할당, `public` 직렬화 필드, Unity 6 폐기 API(`Rigidbody2D.velocity` 등), 게임플레이용 `Time.timeScale`, `UnityEngine.Object` 에 `?.`/`??`, 풀 객체 `Destroy`
2. **런타임 오류 가능성**: null 참조(인스펙터 미연결 처리), 이벤트 구독 해제 누락, 풀 재사용 시 상태 초기화 누락, 파괴·비활성 후 실행되는 비동기 작업, 0으로 나누기, 배열 범위
3. **로직 버그**: 요청·기획서와 실제 동작 차이, 경계값(0, 음수, 최대값, 체력 정확히 50%), 상태 전이 누락·중복 발동, 프레임↔초 변환 오류
4. **결정성·구조 (AI_RULES 3장)**: Core에서 `MonoBehaviour`/`Time`/`UnityEngine.Random`/`Physics2D` 참조, `GameClock` 대신 `Time.deltaTime`, `IRandom` 대신 직접 난수, 로직이 MonoBehaviour에 들어가 테스트 불가
5. **범위·규칙·기획 위반**: 요청 범위 밖 수정, 직렬화 필드 이름 변경(`FormerlySerializedAs` 없이), 매직 넘버(SO로 가야 할 값), 기획서 수치와 SO 초기값 불일치(`spec-check`), 승인 없는 패키지
6. **성능**: 매 프레임 할당(LINQ, 문자열, 람다), `NonAlloc` 미사용 물리 쿼리, 풀링 대상(투사체·적·상자)을 `Instantiate`
7. **씬 테스트 준비 누락**: "에디터 작업"이 빠져서 현민이 연결할 수 없는가, "씬 테스트 절차"가 바뀐 동작·경계 상황을 다루는가, 눈으로 확인하기 어려운 값에 디버그 표시가 있는가, 디버그 코드가 빌드에서 빠지는가(`#if UNITY_EDITOR`/`[Conditional]`), HANDOFF가 실행하지 않은 검증을 "확인됨"으로 적었는가
8. **가독성·구조**: 이름, 메서드 길이, 중복, 파일당 public 타입 하나

### 심각도 기준
- 🔴 **필수**: 크래시·NullReference 가능성, 컴파일 에러, `async void`, 명백한 로직 버그, 요청 범위 밖 수정, 실행하지 않은 검증을 "확인됨"으로 기록
- 🟡 **권장**: 잠재적 버그, 씬 테스트 절차·디버그 표시 부족, 매직 넘버, Core/Runtime 경계 위반, 매 프레임 할당, 기획서 수치 불일치, 유지보수에 불리한 구조
- 🟢 **사소**: 이름, 주석, 스타일 취향

### 하지 말 것
- 확인하지 않은 추측을 🔴로 올리기 (확신이 없으면 "확인 필요"라고 쓰고 🟡로). **지적 전에 해당 파일:줄을 다시 열어 실제 코드인지 확인한다.**
- 취향 차이를 🔴로 올리기
- 코드 전체를 다시 써서 제안하기 (수정 제안은 핵심 몇 줄만)
- 길게 쓰기 (지적 하나는 4줄 이내)

### docs/REVIEW.md 형식

```markdown
# REVIEW — <작업 제목>

- 리뷰 대상: <기준커밋> → 작업 트리 (HEAD: <해시>, 커밋 안 된 변경 포함 여부: 예/아니오)
- 리뷰 일시: YYYY-MM-DD
- 결론: ✅ 승인 / 🔁 수정 필요 (🔴 n개, 🟡 n개, 🟢 n개)

## 지적 사항

### R1 🔴 [async void] Assets/_Project/Scripts/Runtime/Room/RoomController.cs:42
- 문제: `async void OnTriggerEnter2D` — 예외가 사라지고 취소 불가
- 근거: AI_RULES 2장
- 제안: `RunWaveAsync(destroyCancellationToken).Forget()` 로 분리

### R2 🟡 [기획 불일치] BossConfig.cs:18
- 문제: Phase2 휴식 1.5초 — spec/boss.md 6-5는 1초
- 제안: 기획 확인 후 초기값 수정 또는 HANDOFF 질문

## 잘된 점 (1~3줄)
- ...

## 규칙 변경 제안 (있으면)
- ...
```
