# llm-wiki 유지보수 계약 (HongT 프로젝트 볼트)

이 볼트는 저장소 로컬 지식 위키다. 저장소 계약은 `../CLAUDE.md` §7이 우선한다.

## 불변 규칙

1. `raw/`는 소스 오브 트루스, **불변**. 정정은 wiki 페이지나 후속 소스 노트로.
2. `wiki/` + `index.md` + `log.md`는 LLM 소유 작업 산출물. 자유롭게 리라이트.
3. 모든 인제스트는 4곳을 갱신: `raw/sources/` 캡처 → `wiki/sources/` 요약 →
   `index.md` → `log.md`.
4. 지속 가치가 있는 답변은 `wiki/queries/`(질문형) 또는 `wiki/reports/`(리포트형)에
   파일링하고 index/log를 갱신한다.
5. `[OBSERVED]` / `[INFERENCE]` / `[TARGET]` 표기를 위키에도 동일 적용 (CLAUDE.md §4).
6. 페이지 배치: 반복 재현되는 함정·결론 = `wiki/concepts/`, 시스템·캐릭터·모듈
   단위 지식 = `wiki/entities/`, 소스 요약 = `wiki/sources/`.
7. 커밋은 ingest / query / lint 단위로 분리한다.

## 이 프로젝트 특화

- 판정 기준은 항상 `docs/SIM_SPEC*.md` + CLAUDE.md §2 수치계약. 외부 레퍼런스는
  비교축일 뿐 기획서가 아니다.
- 수치 분쟁은 `Assets/Scripts/Sim/`의 상수(SimTypes.cs, HackTypes.cs)와
  CLAUDE.md §2를 단일 진실로 해소한다. 서술의 확신도는 근거가 아니다.

## 알려진 린트 한계

- `lint-wiki.py`는 `raw/`를 페이지로 수집하지 않는다(`collect_pages`가 `raw/` 제외)
  → `[[raw/...]]` 위키링크는 **항상** 깨진 링크로 보고된다. 반면 인제스트 훅
  (`~/vaults/llm-wiki/scripts/ingest-prompt.py:242`, `ingest-output.py:324`)은
  프런트매터에 `[[raw/...]]`를 계속 생성한다 — 훅 산출 페이지의 깨진 raw 링크는
  **예상된 모순이니 재추적하지 말 것.** 직접 쓰는 페이지만 백틱 경로
  (`raw/sources/....md`)를 사용한다.
- `.graphify/` 하위 오펀 보고는 생성 산출물(graphify 파이프라인 소유) — 무시한다.
