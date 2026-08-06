# Unity docs 딥리서치 — 손맛/피드백 키워드 (WebGL 제약 포함)

2026-08-05 · subagent(architect) 조사 통합 · 출처: docs.unity3d.com 매뉴얼/스크립팅 API.
표기: [OBSERVED]=코드/문서 확인, [미확인]=추가 검증 필요.

## (a) AudioSource 원샷 SFX 풀링 + pitch 랜덤화 — **이번 사이클 구현됨**

- API: `AudioSource.PlayOneShot(AudioClip, volumeScale)`, `AudioSource.pitch`.
- WebGL 제약(`Manual/webgl-audio.html`): WebGL은 스레드 미지원이라 FMOD 대신 **Web Audio API**
  기반 구현. 지원 화이트리스트에 `PlayOneShot`/`pitch`/`volume`/`loop`/`time` 포함.
  - **`pitch`는 양수만 지원** → 지터 범위 `[0.94, 1.06]`은 절대 0을 넘지 않아야 한다 [OBSERVED].
  - `AudioSource.priority`는 **WebGL에서 무효**(보이스 수 제한 없음) → 볼륨이 큰 큐가 조용한 큐를
    밀어내지 않게 하려면 **보이스 분산(풀)** 이 유일한 수단.
  - AudioClip은 **AAC 임포트** 후 브라우저 `decodeAudioData`로 디코드 → 런타임 샘플레이트가 원본과
    다를 수 있고 루프 지점 글리치 가능(원샷 큐엔 무영향).
  - Chrome autoplay 정책: 첫 사용자 제스처 전 BGM 자동재생 차단 가능(기존 mute 게이트로 커버).
- 구현: `AudioDirector`에 `AudioSource` 6-보이스 라운드로빈 풀 + xorshift32 결정론 pitch 지터.
  RNG는 **View 전용**(틱 입력에 안 들어감)이라 결정론 심 계약 불침해.
- [미확인] 이미 재생 중인 보이스에 `pitch`를 바꾸면 소급 적용되는지 — Web Audio 특성상 검증 필요.
  현재 구현은 **재생 직전** 설정하므로 이 이슈에 영향받지 않음.

## (b) 히트스톱/슬로우모 — **이미 구현됨(GameView.ApplyTimeScale)**

- `Time.timeScale`은 실행을 늦추는 게 아니라 `Time.deltaTime`/`Time.fixedDeltaTime` 보고값을 스케일.
  **변경은 다음 프레임부터 적용**, `timeScale=0`이면 `FixedUpdate`/`WaitForSeconds` 미호출.
- 회복 감쇠는 `Time.unscaledDeltaTime`으로 진행 → 펄스가 스스로 갇히지 않음 [OBSERVED, GameView.cs].
- 결정론: accumulator가 `min(deltaTime, MaxFrameDelta)`만 소비하고 틱 크기(1/60)는 불변.
- 정정 후보(이월): 카메라 셰이크 감쇠가 `Time.deltaTime`이라 **히트스톱 중 셰이크도 같이 느려진다**.
  손맛상 셰이크는 unscaled로 빼는 게 맞을 수 있음 → 별도 검토.

## (c) 카메라 셰이크 — **이미 구현됨(CameraRig, Cinemachine 불사용)**

- Cinemachine 런타임 패키지 미설치(manifest.json) → 순수 `Transform` + `Mathf.PerlinNoise` 노이즈.
- `Punch()` 우선순위 체인으로 약한 셰이크가 강한 셰이크를 못 덮음 [OBSERVED].

## (d) WebGL 로드 분할 — 조사분(이월)

- `Application.streamingAssetsPath`는 WebGL에서 **URL**이며 `UnityWebRequest`로만 접근.
- 현재 오디오/캐릭터는 `Resources.Load` → 전량 초기 번들 포함. 지연로드하려면 Addressables 필요
  (현재 미설치). GitHub Pages는 `Content-Encoding` 설정 불가라 `decompressionFallback=true` 유지 필수.
- [미확인] 실제 빌드 용량 80 MB는 사용자 진술 — `build-webgl/` 실측 미수행.

## (e) 입력 버퍼/코요테타임 — 조사분(이월)

- New Input System `wasPressedThisFrame`를 InputAdapter가 불리언 래치로 누적, 틱 소비 시 클리어 →
  **1틱 버퍼는 이미 존재**. 시간기반 선입력 윈도(≈120 ms)는 미구현.
- `SimInput` 주석이 \"버퍼링은 심 밖\"이라고 명시 → InputAdapter 단독 구현 계약 근거 확보.
