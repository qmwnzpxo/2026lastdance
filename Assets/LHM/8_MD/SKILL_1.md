---
name: review
description: 리뷰 담당(Gemini)의 코드 리뷰 절차 전체. 현민이 /review 로 호출하거나 "리뷰해줘"라고 할 때 사용한다. docs/HANDOFF.md의 기준 커밋부터 HEAD까지 검토해 docs/REVIEW.md를 작성한다. 코드를 수정하지 않는다.
---

# review

`GEMINI.md` 의 역할과 절차를 그대로 따른다. 요약:

1. `AI_RULES.md` → `docs/HANDOFF.md` 순서로 읽는다. HANDOFF "기획서 근거"의 `docs/spec/*.md` 해당 장도 읽는다.
2. `git diff <기준 커밋>` (작업 트리까지, `..HEAD` 없이) + `git status --porcelain` 의 `??` 새 파일을 직접 읽어 변경을 확인한다. 커밋은 현민이 하므로 아직 커밋 안 된 변경이 정상이다.
   - 변경이 없으면 리뷰하지 말고 `docs/REVIEW.md` 에 "기준 커밋 기록 오류로 변경 없음"이라고 쓴다.
   - git을 실행할 수 없으면 현민에게 diff 붙여넣기를 요청한다.
   - `docs/`, `.meta` 는 지적 대상이 아니다 (`.meta` 는 현민이 에디터를 열 때 생긴다). HANDOFF "에디터 작업"에 없는 씬·프리팹·에셋 변경이 섞여 있으면 "확인 필요" 🟡.
3. HANDOFF의 "리뷰어에게 요청", "리스크", "기획서와 다르게 한 점", "씬 테스트 절차"를 먼저 본다.
4. `GEMINI.md` 체크리스트 1~8번 순서로 검토한다.
   - 1번(Unity 함정)은 `unity-guard` 스킬을 사용한다.
   - 5번(기획 위반)은 `spec-check` 스킬을 사용한다.
   - **지적하기 전에 해당 파일:줄을 다시 열어 실제 코드인지 확인한다** (오지적 방지).
5. `docs/REVIEW.md` 를 `GEMINI.md` 의 형식으로 덮어쓴다 (없으면 새로 만든다).
6. 현민에게 결론 한 줄과 🔴/🟡/🟢 개수만 보고한다.

**하지 않는 것:** 코드 수정, 커밋 등 git 상태 변경, Unity 실행, `docs/HANDOFF.md` 작성, 확인하지 않은 추측을 🔴로 올리기, `docs/spec/` 수정.
