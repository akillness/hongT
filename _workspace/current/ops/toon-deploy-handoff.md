# 인계 — 툰 렌더링 2단계 배포 **완료** (2026-08-12 종결)

이 문서는 배포를 요청하는 인계문서였다. 배포가 끝났으므로 요청은 종결하고,
**대기 사유 두 가지가 실제로는 존재하지 않았다는 것**을 기록으로 남긴다.
그쪽이 다음 사이클에 쓸모가 있다.

원문의 요청·검증 기록은 git 이력(`7c9f2a0` 시점)에 남아 있다.

## 결과 [OBSERVED]

```
라이브        https://akillness.github.io/hongT/
gh-pages     874def5  (tree 3302a183)
후보 소스     713d95e9010d971cf18b5a42b5f0f081485d567e
```

라이브 프로비넌스의 `candidateSourceSha`와 release `contentMarker`
(`5626530142cad469`)가 로컬과 일치. 실제 https URL에 대한 콜드 프로필
원버튼 스모크가 데스크톱·모바일 모두 `loading -> running`, 페이지 에러 0.

## 대기 사유 1 — "절차 문서가 없다": 틀렸다

원문은 evidence ID 집합과 probe 해시의 규격이 "저장소·`docs/`·`specs/`
어디에도 없다"고 적었고, 그래서 값을 채우면 게이트를 무력화하는 셈이라
실행하지 않았다. **그 판단의 전제가 사실이 아니었다.**

```
tools/deploy/run_release_gate.sh      stage 1/2로 엔진 단계 전체
tools/deploy/make_release_evidence.py 독스트링이 6종 각각의 질문을 명시
```

둘 다 게이트를 도입한 같은 커밋(`9d30d88`)에 함께 들어와 있었다.
`docs/`와 `specs/`만 찾고 `tools/deploy/`를 안 봤다.

**일반화**: "문서가 없다"고 적기 전에 **그 도구의 디렉터리를 봐라.**
게이트를 만든 사람은 절차를 게이트 옆에 둔다. 그리고 이 판단은 사이클 하나를
통째로 대기시켰다 — 블로커의 근거는 블로커 자체보다 검증 비용이 싸다.

## 대기 사유 2 — "모바일 그림자 증거가 임계에서 실패": 지금은 통과

**임계를 하나도 건드리지 않고** 현재 빌드에서 재측정:

| | SNR | footprint | darkening | |
|---|---:|---:|---:|---|
| desktop 1440x900 | 120.96 | 0.375% | 19.06 | PASS |
| mobile 390x844 | 20.41 | 0.256% | 18.52 | PASS |

두 뷰포트 모두 리시버 재활성화가 shipped 상태가 지닌 luma의 100%를 복원.

§4z 그대로다 — **이월된 블로커는 매 사이클 도구 쪽을 한 번씩 의심하라.**
대상은 잘 안 변하지만 도구는 쉽게 바뀐다. 여기서 바뀐 것은 landing 판정이
프레임 전체 평균 luma가 아니라 변경 픽셀 수를 보게 된 것이다.

## 정직하게 약해진 것 — 다음 사람이 알아야 한다

### `probeHashes`의 정의를 복원하지 못했다

검증기가 재계산하지 않는 자유 필드다. 이전 값
`9c6f5d70...`은 저장소의 어떤 것으로도 재현되지 않았다 — 셰이더/스크립트
파일 7종, `ls-tree` 4개 경로, 트리 오브젝트 id, sha-of-sha 전부 불일치.

**뜻을 적을 수 없는 값을 복사하는 것은 확인하지 않은 것을 주장하는 것**이라,
이번 릴리스의 정의를 `metadata.commands`에 써넣고 실제로 계산했다:

```
probeHashes := sha256( sorted (path, blob-sha256) of
  StageShadowPolicy.cs · StageMood.cs · VfxDirector.cs · StageShadowReceiver.shader )
  at baselineProbeSha / candidateSourceSha
```

이번은 baseline != candidate이고 그게 참이다(베이스라인 이후 VfxDirector가
블롭 그림자와 캐스터 승격을 받았다). **이전 릴리스의 쌍과 이번 쌍은 서로 다른
정의를 비교하는 것**이므로 나란히 놓고 "프로브가 변했다"고 읽으면 안 된다.

### `cleanStatus` pre/post가 이번엔 빌드를 감싸지 않는다

이번 쌍은 프로비넌스 **생성 단계**를 감싼다. 빌드가 이미 돈 뒤에는 빌드를
감싸는 쌍을 소급 생성할 수 없고, 이전 릴리스의 동결 쌍은 불변이며 후보
`1bd8eca`의 것이다. pre/post 관례는 어디에도 강제·문서화돼 있지 않아
파일명만 보면 더 강한 주장으로 읽힌다.

독립적으로 강제되는 것: `validate_exact_candidate`가 생성 시점에 워킹트리를
`candidateSourceSha`로 고정하고, 각 빌드 증거가 그 빌드가 낸 바이트의
`contentMarker`를 지닌다.

**다음 릴리스는 순서를 지켜라** — 게이트 stage 1 **전에** pre 스냅샷,
stage 2 **후에** post 스냅샷. 그러면 이 문단이 필요 없어진다.

## 유지되는 주의점

- **툰 텍스처는 `Assets/Resources/Textures/Toon/`에 별도로 있고 `Env/`는
  그대로 둔다.** `EnvironmentBuilder`가 Toon 우선 → Env 폴백으로 읽는다.
  Env를 지우면 폴백이 사라진다.
- **셰이더는 `ViewWorld.LitShader` 한 곳에서 전환된다.** 되돌릴 일이 생기면
  거기서 `Shader.Find("Universal Render Pipeline/Lit")`만 남기면 전체가 원복.

## 이어지는 미해결 [OBSERVED]

툰 텍스처 세트가 브리프 밖으로 나간 것이 측정됐다 — 표면 디테일 중앙값
`localStd` PBR 9.28 → 툰 1.82, 밝기 중앙값 +19%(최악 witness-well/floor
+126%), 18장 중 6장이 사실상 단색 시트. 평탄화 자체는 브리프이지만
**밝기와 단색화는 프롬프트가 요구한 적이 없다.**

`ember-bastion`이 같은 생성기·같은 COMMON으로 10.50/10.34를 냈으므로
브리프는 달성 가능하다. 재촬영 도구:

```
bash tools/gen_toon_env_retake.sh          # 스테이징에만 생성, 덮어쓰지 않음
python3 tools/qa/measure_toon_textures.py --staging <dir>
```
