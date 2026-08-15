# Cycle 9 G8 인간 플레이테스트 패킷

> **상태: 사람 응답 수집용 / G8 `FIX` 유지. 이 문서는 게이트 통과를 선언하지 않는다.**
>
> **유효한 서로 다른 인간 참가자 5명이 각각 독립적으로 작성한 원시 투표 5장이 필요하다. 한 사람의 투표는 `1/5`일 뿐이며, 점수가 높아도 그 한 장만으로 G8을 닫거나 `PASS`로 바꿀 수 없다.**

이 패킷은 `_workspace/current/qa/test-plan.md` §4의 1–5 관찰 기준과 G8 블라인드 원칙을 사람이 그대로 수행하도록 만든 운영 문서다. 진행자는 참가자에게 아래 **「참가자용 안내」와 빈 투표지**만 보여 준다. 문서 맨 끝의 **투표 후 공개 부록**은 투표가 잠기기 전에는 참가자에게 보여 주거나 설명하지 않는다.

---

## 1. 진행자 사전 조건

### 1.1 참가자 적격성

한 투표는 다음 조건을 모두 만족해야 유효하다.

- 실제 인간 1명이 직접 관찰하고 직접 작성한다. LLM, 에이전트, 봇, 자동 플레이 결과, 다른 사람의 대필은 무효다.
- 해당 참가자는 이 후보의 의도, 기대 답, 벤치마크 비교, 기존 점수, 다른 참가자의 답을 미리 보지 않았다.
- 같은 사람은 이 빌드·후보에 투표를 한 번만 한다. 재시도·문구 수정·두 번째 계정은 표본 수를 늘리지 않는다.
- 다섯 투표는 서로 다른 다섯 사람에게서 받아야 한다. 한 사람의 여러 세션, 같은 답안의 복사, 이전 사이클 투표 재사용은 금지한다.
- 참가자는 투표를 잠그기 전 다른 참가자와 화면, 답, 점수, 공략을 공유하지 않는다.
- 참가자는 관찰 중 검색, 공략, 생성형 AI, 번역·요약 AI를 사용하지 않는다. 접근성 보조기술은 허용하되 사용 사실만 기록한다.
- 참가자는 키보드 조작이 가능한 데스크톱 브라우저, 안정적인 네트워크, 소리를 들을 수 있는 헤드폰 또는 스피커를 사용한다. 소리를 재생하지 못한 세션은 5번 차원을 평가할 수 없으므로 유효한 완성 투표로 세지 않는다.

### 1.2 독립성·개인정보

- 진행자는 참가자에게 무작위 가명 토큰만 발급한다. 예: `H-7K3M`. 이름, 이메일, 계정명, 얼굴, 음성, IP 주소를 저장소에 넣지 않는다.
- 진행자는 저장소 밖의 비공개 명부에 `참가자 토큰 ↔ 서로 다른 실제 사람 확인`만 보관한다. 공개 산출물에는 토큰만 남긴다.
- 화면 녹화가 필요하면 게임 화면과 입력 시각만 담고, 카메라·마이크·브라우저 계정·알림은 담지 않는다. 참가자가 녹화를 원하지 않으면 진행자 관찰 로그와 게임 화면 스크린샷으로 대체하고 그 방식을 투표지에 적는다.
- 참가자는 언제든 중단할 수 있다. 중단 투표는 원시 기록으로 보관할 수 있지만 완성 표본 `N`에는 넣지 않는다.

### 1.3 정확한 URL과 빌드 고정

이번 패킷의 기준 실행 위치는 다음과 같다.

- 공개 루트: `https://akillness.github.io/hongT/`
- 직접 진입 URL: `https://akillness.github.io/hongT/?mode=campaign&stage=cinder-sluice&intro=off`
- 고정 GitHub Pages 배포 커밋: `c3b0c08fe84e4e425c4abcbdacaf847420d23adc`
- 고정 WebGL 캐시 ID: `e9eb0a4c5c54442d`
- 배포 커밋 시각: `2026-08-08T07:47:52Z`
- 배포 커밋 확인 API: `https://api.github.com/repos/akillness/hongT/branches/gh-pages`

**빌드 식별자는 화면의 버전 배지만이 아니라 `GitHub Pages 전체 커밋 SHA + index.html의 WebGL 캐시 ID` 쌍이다.** 진행자는 참가자에게 화면을 넘기기 직전에 다음을 수행한다.

1. 배포 커밋 확인 API의 `commit.sha`가 위 40자리 SHA와 정확히 같은지 확인한다.
2. 공개 루트 `index.html`에서 `CinderCourt WebGL build cache version` 값이 `e9eb0a4c5c54442d`인지 확인한다.
3. 두 확인을 한 정확한 시각을 ISO 8601 형식(초와 UTC 오프셋 포함)으로 적는다. 예: `2026-08-08T18:03:27+09:00`.
4. API 응답과 캐시 ID가 보이는 텍스트 또는 스크린샷을 해당 투표의 `build_identity_evidence_path`에 저장한다.
5. 둘 중 하나라도 다르면 세션을 시작하지 않는다. 이전 값을 새 빌드에 붙이거나, 짧은 SHA·화면 배지로 대신하거나, 보이지 않는 값을 추측하지 않는다.

기존 Cycle 9 녹화는 정확한 배포 커밋을 당시 기록하지 않았으므로, 그 녹화만 재생한 응답은 이 패킷의 **정확한 빌드 고정 조건을 단독으로 만족하지 않는다**. 기존 자료는 진행 절차와 마스킹 위치를 확인하는 참고 증거이며, 이번 인간 투표는 위 고정 빌드의 새 세션 증거와 결합해야 한다.

### 1.4 세션 준비

- 브라우저 프로필에는 `prologueDone`과 대상 스테이지 해금 상태가 유효해야 한다. 진행자는 참가자가 오기 전에 직접 진입 URL이 전투로 연결되는지 확인하고, 사용한 저장 상태의 원문 또는 해시를 `seed_evidence_path`에 남긴다.
- 캐시 삭제나 시크릿 창 때문에 해금이 사라지면 참가자 앞에서 공략을 설명하지 말고 세션을 중단한 뒤 준비된 프로필로 다시 연다.
- 화면의 스테이지 제목과 목표 배너는 참가자에게 의도·정답을 암시할 수 있다. 기존 익명 캡처와 동일하게 해당 두 영역을 불투명 중립 마스크로 가린 뒤 화면을 넘긴다. 마스크는 전투 공간, 체력·기름, 스킬, 웨이브·점수, 소리 상태를 가리지 않아야 한다.
- 소리는 `켜짐`으로 두고 운영체제 볼륨을 참가자가 들을 수 있는 수준으로 맞춘다. 자막이나 게임 내 기본 안내는 그대로 두되 진행자가 설명을 덧붙이지 않는다.
- 인간 평가 전에 진행자는 위 고정 빌드의 한 실제 전투 세션에서 최종 프레임 4장을 새로 캡처하고, 제목·목표 영역만 기존 `cycle9-g8-anon-{a,b,c,d}.png`와 같은 방식으로 가린다. 새 자극물은 `_workspace/current/qa/cycle9-g8-human/stimuli/<build-sha>-anon-{a,b,c,d}.png`에 두고 각각의 SHA-256을 기록한다.
- 네 새 캡처는 모든 참가자에게 같은 세트로 쓰되, 참가자마다 순서를 독립적으로 무작위화한다. 진행자는 순서를 미리 정해 두고 반응에 따라 바꾸지 않는다. 과거 익명 캡처는 마스크 참고용일 뿐, 정확한 당시 배포 SHA가 없으므로 새 인간 표본의 최종 자극물로 재사용하지 않는다.
- 관찰 시작 전 참가자에게 점수표, 다섯 차원, 후보명, 기대 행동을 보여 주지 않는다.

---

## 2. 참가자용 안내 — 이 부분만 사전 제공

아래 문구를 모든 참가자에게 글자 그대로 읽어 준다.

> 먼저 서로 다른 네 장의 게임 캡처를 무작위 순서로, 총 30–60초 동안 한 번만 봅니다. 무엇을 찾아야 하는지, 제작진이 확인하려는 요소, 잘한 답은 알려 드리지 않습니다. 끝난 직후 점수표를 보기 전에 첫 질문에 혼자 답합니다.
>
> 그 답을 잠근 다음 같은 빌드의 게임 장면을 30–60초 동안 직접 조작합니다. 조작은 `WASD` 또는 방향키로 이동, `Space`로 기본 공격, `Shift`와 `Q`·`E`·`R`·`F`로 화면 하단에 표시된 행동을 사용합니다. 모든 키를 반드시 쓸 필요는 없습니다.
>
> 두 단계 모두 멈추거나 되감거나 공략을 묻지 마세요. 화면에서 이해한 대로 자유롭게 행동하면 됩니다. 정답은 없습니다.

진행자는 참가자의 질문에 후보나 공략을 설명하지 않고 다음 한 문장만 반복한다.

> “화면에서 이해한 대로 자유롭게 해 주세요.”

---

## 3. 블라인드 캡처 관찰과 라이브 세션

### 3.1 블라인드 캡처 관찰 — G8 첫 회상·기억성

1. 진행자는 참가자가 라이브 게임이나 다른 투표를 보기 전에, 위 고정 빌드에서 새로 만든 익명 캡처 4장을 참가자별 무작위 순서로 전체 화면에 띄운다.
2. 각 캡처를 8–15초씩 한 번만 보여 주어 총 관찰 시간을 30–60초로 맞춘다. 기본값은 장당 10초, 총 40초다. 진행자는 참가자의 반응에 따라 시간을 바꾸지 않는다.
3. `blind_observation_started_at`, `blind_observation_ended_at`, 실제 순서, 네 정확한 파일 경로와 SHA-256을 기록한다.
4. 마지막 캡처를 닫은 즉시 §4의 첫 회상 질문만 보여 준다. 답이 잠기기 전에는 라이브 게임, 루브릭, 다섯 차원을 보여 주지 않는다.
5. 첫 회상 답을 잠근 뒤 §5의 공통 루브릭을 보여 주고, 캡처 세트의 **전체 기억성 점수 1개**를 먼저 받는다. 이 값이 G8 블라인드 패널의 참가자별 원시 점수다.

### 3.2 라이브 30–60초 조작 — 다섯 차원

1. 진행자가 준비된 브라우저에서 직접 진입 URL을 연다. 로비로 돌아오면 준비된 해금 프로필인지 다시 확인한다. 불가피하게 수동 진입할 때는 `출정` 화면에서 준비된 대상 카드의 `강하`를 누르되, 카드 설명이나 선택 이유를 읽어 주지 않는다.
2. 로딩과 도입 연출이 끝나고 참가자가 캐릭터를 움직일 수 있는 첫 프레임을 `live_observation_started_at`으로 기록한다.
3. 참가자는 한 번만 자유롭게 조작한다. 기본 시간은 45초이며, 허용 범위는 30–60초다. 진행자는 시작 전에 종료 시점을 정하고 결과에 따라 늘리거나 줄이지 않는다.
4. 진행자는 힌트, 칭찬, 놀람, 손짓, 특정 키 권유, 재시작 권유를 하지 않는다. 기술 장애 외에는 개입하지 않는다.
5. 정한 시점에 입력을 멈추게 하고 `live_observation_ended_at`을 기록한다. 실제 시간은 `ended_at - started_at`으로 계산하여 30–60초인지 확인한다.
6. 참가자는 §5의 같은 1–5 기준으로 다섯 차원을 각각 평가한다. 첫 회상 문장과 블라인드 기억성 점수는 라이브 조작 뒤에도 수정할 수 없다.

블라인드 또는 라이브 단계에서 기술 장애, 로딩 중 시간 포함, 허용 시간 이탈, 마스크 실패, 진행자 힌트가 발생하면 `protocol_deviation`에 사실대로 적고 완성 표본에서 제외한다. 라이브 단계에서 소리가 꺼졌다면 5번 차원을 평가할 수 없으므로 역시 제외한다. 좋은 점수를 보존하려고 장애를 누락하지 않는다.
---

## 4. 무유도 첫 회상 질문

다른 질문이나 예시 없이, 블라인드 캡처 관찰 직후 아래 한 문장만 제시한다.

> **방금 본 네 장면에서 다른 사람에게 가장 먼저 설명하고 싶은 단 하나의 요소는 무엇입니까? 한 문장으로 적어 주세요.**

- 참가자가 “어떤 종류요?”라고 물어도 범주·예시·후보명을 주지 않는다.
- 답을 한 문장 그대로 저장하고 수정하지 못하게 잠근다.
- 빈 답도 빈 답 그대로 유효한 관찰값이다. 억지로 채우게 하거나 특정 단어를 유도하지 않는다.
- 첫 회상 답을 잠근 정확한 시각을 `first_recall_locked_at`에 기록한 뒤에만 다음 루브릭을 보여 준다.

---

## 5. 1–5 공통 루브릭과 다섯 차원

먼저 블라인드 캡처 세트의 전체 기억성에 **정수 하나(1, 2, 3, 4, 5)**를 고른다. 이어서 라이브 세션의 다섯 차원에도 같은 기준으로 정수 하나씩 고른다. 평균처럼 `3.5`를 쓰지 않는다. 참가자는 아래 기준을 동일하게 적용하고, 관찰한 사실을 짧게 덧붙일 수 있다.

| 점수 | 관찰 기준 |
|---:|---|
| 1 | 사건 또는 결과를 놓쳤거나 다른 것으로 잘못 이해했다. |
| 2 | 판단해야 할 시간이 지난 뒤에야 알아차렸거나, 서로 충돌하는 피드백을 보았다. |
| 3 | 사건과 결과를 제때 알아차렸지만 한 번 다시 확인하거나 외부 질문이 필요했다. |
| 4 | 외부 설명 없이 첫 관찰에서 사건, 대응, 결과를 알아차렸다. |
| 5 | 4점 수준의 명료성에 더해, 장면이 끝난 뒤에도 대응 방법 또는 타이밍과 장면의 정체성을 기억해 설명할 수 있다. |

평가할 다섯 차원은 다음과 같다.

1. **입력 반응** — 내가 입력한 행동과 화면·소리의 반응 관계가 알아보기 쉬웠는가.
2. **위협 판독성** — 위험이 언제·어디서 오는지와 대응 시점을 알아보기 쉬웠는가.
3. **타격·결과 명료성** — 맞음, 피함, 성공, 실패 또는 상태 변화의 결과가 알아보기 쉬웠는가.
4. **장면·장소 정체성** — 이 장면이 다른 전투 공간과 구분되어 기억에 남는가.
5. **시청각 응집성** — 화면 효과, 움직임, UI와 소리가 같은 사건을 일관되게 전달했는가.

진행자는 점수를 해석하거나 합산하지 않는다. 블라인드 기억성 점수는 다섯 차원과 별도로 보존하며 서로 대체하지 않는다. 특히 한 참가자의 높은 점수, 다섯 차원의 개인 중앙값, 기존 합성 패널 결과 어느 것도 단독 게이트 판정이 아니다.

---

## 6. 원시 투표지 템플릿

아래 블록을 참가자마다 새 파일에 복사한다. 권장 제출 경로는 `_workspace/current/qa/cycle9-g8-human/ballot-<participant_token>.md`다. 세션 증거는 같은 토큰 아래에 두되 개인정보를 포함하지 않는다.

```yaml
protocol: C9-G8-HUMAN-v1
ballot_id: C9-G8-H-____
participant_token: H-____
admissible: pending|true|false


eligibility:
  actual_human: true|false
  unique_person_confirmed_by_facilitator: true|false
  prior_vote_for_same_build_candidate: true|false
  prior_candidate_or_expected-answer_exposure: true|false
  saw_other_ballot_or_discussed_answer: true|false
  used_ai_search_guide_or_answer_assistance: true|false
  accessibility_assistance_used: none|직접기재
  consented_to_anonymized_evidence: true|false

build_identity:
  requested_url: https://akillness.github.io/hongT/?mode=campaign&stage=cinder-sluice&intro=off
  final_url_after_load: ""
  gh_pages_commit_sha: c3b0c08fe84e4e425c4abcbdacaf847420d23adc
  webgl_cache_id: e9eb0a4c5c54442d
  identity_checked_at: "YYYY-MM-DDTHH:MM:SS+09:00"
  build_identity_evidence_path: _workspace/current/qa/cycle9-g8-human/evidence/<token>-build-identity.txt
  seed_evidence_path: _workspace/current/qa/cycle9-g8-human/evidence/<token>-seed.txt

blind_panel:
  stimulus_build_sha: c3b0c08fe84e4e425c4abcbdacaf847420d23adc
  stimulus_webgl_cache_id: e9eb0a4c5c54442d
  randomized_order: [anon-_, anon-_, anon-_, anon-_]
  capture_paths:
    - _workspace/current/qa/cycle9-g8-human/stimuli/<build-sha>-anon-_.png
    - _workspace/current/qa/cycle9-g8-human/stimuli/<build-sha>-anon-_.png
    - _workspace/current/qa/cycle9-g8-human/stimuli/<build-sha>-anon-_.png
    - _workspace/current/qa/cycle9-g8-human/stimuli/<build-sha>-anon-_.png
  capture_sha256:
    - ""
    - ""
    - ""
    - ""
  blind_observation_started_at: "YYYY-MM-DDTHH:MM:SS+09:00"
  blind_observation_ended_at: "YYYY-MM-DDTHH:MM:SS+09:00"
  blind_observation_duration_seconds: 0
  title_and_objective_masked: true|false
  first_recall_locked_at: "YYYY-MM-DDTHH:MM:SS+09:00"

live_session:
  browser_and_version: ""
  os_and_version: ""
  viewport_css_px: "widthxheight"
  input_device: keyboard|기타
  audio_output: headphones|speakers
  audio_on: true|false
  reduced_motion: on|off
  title_and_objective_masked: true|false
  live_observation_started_at: "YYYY-MM-DDTHH:MM:SS+09:00"
  live_observation_ended_at: "YYYY-MM-DDTHH:MM:SS+09:00"
  live_observation_duration_seconds: 0
  submitted_at: "YYYY-MM-DDTHH:MM:SS+09:00"
  session_evidence_path: _workspace/current/qa/cycle9-g8-human/evidence/<token>-session.webm
  protocol_deviation: none|직접기재

first_recall_exact_sentence: ""

scores:
  blind_memorability_1_to_5: 0
  input_response_1_to_5: 0
  threat_readability_1_to_5: 0
  hit_outcome_clarity_1_to_5: 0
  scene_location_identity_1_to_5: 0
  audiovisual_cohesion_1_to_5: 0

optional_observed_fact_per_dimension:
  input_response: ""
  threat_readability: ""
  hit_outcome_clarity: ""
  scene_location_identity: ""
  audiovisual_cohesion: ""

attestation:
  participant_statement: "나는 다른 투표나 의도 설명 없이 블라인드 캡처와 라이브 세션을 각각 한 번 관찰하고, 이 답과 점수를 직접 작성했다."
  participant_token_confirmation: H-____
  facilitator_token: F-____
```

### 6.1 유효성 확인표

진행자는 점수를 보기 전에 형식만 검사한다.

- [ ] 실제 인간이며 비공개 중복 확인을 통과했다.
- [ ] 이전 노출·토론·AI 보조·재투표가 모두 `false`다.
- [ ] 정확한 URL, 전체 배포 SHA, 캐시 ID가 모두 기록되어 기준과 일치한다.
- [ ] 빌드 확인, 블라인드·라이브 관찰 시작/종료, 첫 회상 잠금, 제출 시각이 모두 초·오프셋 포함 ISO 8601이다.
- [ ] 새 익명 캡처 네 장의 고정 빌드 ID, 정확한 경로, SHA-256, 참가자별 무작위 순서가 기록됐다.
- [ ] 블라인드 캡처 총 관찰 시간과 라이브 조작 시간이 각각 30–60초다.
- [ ] 라이브 소리가 켜졌고 두 단계 모두 제목·목표 마스크가 유지됐다.
- [ ] 첫 회상 문장이 블라인드 캡처 직후, 루브릭과 라이브 세션 공개 전에 잠겼다.
- [ ] 블라인드 기억성 1개와 다섯 차원 점수가 모두 1–5 정수다.
- [ ] 투표 경로와 자극물·세션·빌드·시드 증거 경로가 실제 파일을 가리킨다.
- [ ] 동일 투표 파일이나 라이브 세션 증거를 다른 참가자 토큰으로 복제하지 않았다. 공통 캡처 세트를 쓴 경우에도 순서와 사람의 원시 응답은 독립적이다.
- [ ] 진행자 개입이나 기술 장애가 있으면 숨기지 않고 이탈로 기록했다.

한 항목이라도 실패하면 원시 투표는 보존하되 `admissible: false`로 분류하고 인간 표본 `N`에 넣지 않는다. 실패 투표를 고쳐서 같은 사람의 새 표본으로 세지 않는다.

---

## 7. 제출·집계 경계

- 한 참가자 파일은 **원시 투표 1장 = 인간 표본 `1/5`**다.
- 다섯 장을 모으기 전에는 중앙값, 빈도, 합격 여부를 확정하지 않는다.
- 다섯 참가자 모두 독립성·빌드 고정·시간·증거 경로 조건을 통과해야 한다.
- 진행자는 원문을 보정하거나 비슷한 표현으로 통합하지 않는다. 첫 회상 범주화와 중앙값 계산은 다섯 원시 투표가 잠긴 뒤 별도 QA 단계에서 수행한다.
- 합성 패널, 기존 캡처에 대한 에이전트 평가, 한 사람의 반복 투표는 인간 5명을 대체하지 않는다.
- 이 패킷으로 수집된 점수는 G8의 인간 인상 증거 한 축일 뿐이다. 빈도 출처 검증은 별도 축이며 어느 한쪽도 다른 쪽을 대신하지 않는다.
- 이 문서를 작성하거나 투표 한 장을 받았다는 사실은 `PASS`가 아니다. Cycle 9 상태는 별도 게이트 검토가 실제 값·방법·정확한 시각·증거 경로를 확인하기 전까지 `FIX`다.

---

# 투표 후 공개 부록 — 참가자는 제출 잠금 전 열람 금지

> **중지:** 이 아래에는 후보, 기존 관찰, 빈도와 출처가 공개되어 있다. 참가자는 자신의 `first_recall_exact_sentence`, 다섯 점수, `submitted_at`이 잠긴 뒤에만 읽을 수 있다. 진행자는 사전 안내에 이 내용을 인용하거나 요약하지 않는다.

## A. 후보·빈도·출처 공개

- 평가 후보는 Cinder Sluice의 `tide-current` 계열, 즉 `Conveyor / current / push field`로 분류된 장면이다.
- 현재 빈도표 값은 `1/11`이지만 유일한 양성 셀이 `ETG(t)`로 표시된 얇은 근거다. 현재 Cycle 9에서 분모와 출처 라벨을 다시 검증하지 않았으므로 빈도 축은 **`UNVERIFIED`**이며 통과로 사용할 수 없다.
- 빈도 원문: `_workspace/current/design/trend-survey/dungeon-gimmick-trends.md#frequency-table-g8-input`
- 점수표 연결: `_workspace/current/design/novelty-scorecard.md`
- 게이트 현재 기록: `_workspace/current/qa/gate-measurements.md#g8`

## B. 기존 Cycle 9 캡처와 합성 투표

- 기존 Playwriter 원본: `_workspace/current/qa/cycle9-g8-entry-raw.mp4`
- 원본 SHA-256: `361659c5eade0a3cc2295d0ed282e2dc6ec71d2861aa19e4c0682a45395c03ad`
- 원본 메타데이터: 생성 시각 `2026-08-08T14:57:27Z`, H.264, `1920×1200`, 약 `170.390167 s`, 오디오 없음.
- 전체 화면 캡처: `_workspace/current/qa/cycle9-g8-capture-01.png`부터 `_workspace/current/qa/cycle9-g8-capture-07.png`
- 제목·목표를 제거한 익명 캡처: `_workspace/current/qa/cycle9-g8-anon-a.png`, `cycle9-g8-anon-b.png`, `cycle9-g8-anon-c.png`, `cycle9-g8-anon-d.png`
- 기존 합성 투표: `_workspace/current/qa/cycle9-g8-ballots.md`

기존 투표는 다섯 LLM 리뷰어의 방향성 평가이며 인간 플레이테스트가 아니다. 또한 기존 원본은 당시 정확한 배포 커밋을 기록하지 않았고 오디오가 없으므로, 인간 투표의 빌드 고정 또는 시청각 응집성 증거로 소급 대체할 수 없다. 이 패킷에 적힌 현재 배포 SHA를 과거 녹화에 역으로 붙이지 않는다.

## C. 기존 결과를 사전 공개하지 않는 이유

기존 합성 패널은 특정 시각 요소와 결과 가독성에 관한 점수와 회상 빈도를 이미 남겼다. 이를 참가자에게 먼저 말하면 첫 회상 질문의 답과 점수를 직접 유도한다. 따라서 기존 점수, 기대 중앙값, 후보 빈도, 공략 또는 제작 의도는 다섯 인간 원시 투표가 모두 잠길 때까지 참가자에게 공개하지 않는다.
