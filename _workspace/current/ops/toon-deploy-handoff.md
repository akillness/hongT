# 인계 — 툰 렌더링 2단계 배포 대기

2026-08-12. 작성: 툰 레인. 수신: 릴리스 프로비넌스 게이트를 만든 레인.

## 요청

`7c9f2a0`까지 푸시된 상태를 **빌드해서 gh-pages로 배포**해 주세요. 소스 쪽 작업은
끝났고 검증도 마쳤습니다. 제 쪽에서 배포만 실행하지 못합니다.

## 왜 제가 못 하는가

`tools/deploy/deploy_pages.sh:67`이 `build-webgl/release-build-provenance.json`을
요구하고, 없으면 `FATAL: frozen Release provenance is missing`으로 멈춥니다.

그 파일을 만들려면 `release_provenance.py create --metadata`에 evidence 레코드가
필요한데, 검증기가 이렇게 요구합니다:

```
metadata.evidence IDs must exactly match the stage-shadow policy
```

[INFERENCE] 이 ID 집합과 probe 해시의 규격은 `9d30d88`(08/12 02:03)에서 도입된
릴리스 체계의 일부이고, 저장소·`docs/`·`specs/` 어디에도 절차 문서가 없습니다.
제가 값을 채워 넣으면 **동결 증거 없이 릴리스하지 못하게 막으려던 게이트를 제가
무력화**하는 셈이라 실행하지 않았습니다.

## 배포 대상에 담긴 것 (커밋 3건)

| 커밋 | 내용 |
|---|---|
| `b1a3a4a` | 툰 셰이더 도입 + 키트 석재 20종 전환 |
| `7c9f2a0` | 환경 툰 전환 — gti 툰 텍스처 18종 + 셰이더 배선 |
| `cc864aa` | 기믹 고체 실그림자 캐스터 승격 (그 앞 `5f413ce` 블롭 포함) |

## 이미 통과한 검증 [OBSERVED]

```
EditMode      992 중 990 통과 · 실패 0 · 스킵 2
빌드          errors 0 · data 85.7 MB (상한 120)
9스테이지 스모크  전부 진입 · 페이지 에러 0
§E0.5 해저드 판독  텔레그래프 채도 / 환경 채도 = 1.6~3.8배 (전 스테이지 지배적)
```

마지막 항목은 환경 톤이 바뀌었기 때문에 새로 잰 것입니다. 툰 전환이 해저드
텔레그래프를 흐리지 않았음을 스테이지별로 확인했습니다.

## 주의점 두 가지

- **툰 텍스처는 `Assets/Resources/Textures/Toon/`에 별도로 있고 `Env/`는 그대로
  둡니다.** `EnvironmentBuilder`가 Toon 우선 → Env 폴백으로 읽습니다. Env를 지우면
  폴백이 사라지므로 건드리지 마세요.
- **셰이더는 `ViewWorld.LitShader` 한 곳에서 전환됩니다.** 되돌릴 일이 생기면
  거기서 `Shader.Find("Universal Render Pipeline/Lit")`만 남기면 전체가 원복됩니다.

## 현재 라이브

`70058d9` — 툰 이전 빌드입니다. 사용자에게 보이는 화면에 회귀는 없습니다.
