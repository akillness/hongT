# asset-lane3 — 획득/팝업 효과음 3종 (ElevenLabs sound-generation)

날짜: 2026-08-08 | 담당: asset-lane3 (Codex 자산 레인)

## 요청 계약

ui-lane3 소비 코드와 확정된 파일명/경로 3종. 기존 cue-*.mp3 재생성 없이
신규 3종만 생성.

## 산출물

| 파일 | 목적 | 목표 길이 | 실측 길이(ffprobe) | 크기 |
|---|---|---|---|---|
| `Assets/Resources/Audio/cue-loot-fine.mp3` | 상급(Fine) 아이템 획득음 | ~0.6s | 0.627s | 10,493 bytes |
| `Assets/Resources/Audio/cue-loot-epic.mp3` | 영웅(Epic) 아이템 획득음 | ~1.0s | 1.045s | 17,180 bytes |
| `Assets/Resources/Audio/cue-toast.mp3` | 팝업 등장 UI 사운드 | ≤0.3s (요청) | 0.522s (API 하한 적용) | 8,821 bytes |

각 `.mp3` 옆에 기존 `cue-pickup.mp3.meta`를 템플릿으로 새 GUID를 발급한
`.meta`를 작성했다 (AudioImporter serializedVersion 8, sampleRateOverride
44100, compressionFormat 1/quality 1, 3D: 1 — 기존 cue-* 세트와 동일 설정).

- `cue-loot-fine.mp3.meta` guid `e2b5919042ba44bd864119e8dd1f2079`
- `cue-loot-epic.mp3.meta` guid `33e16169c07d4439aa4783801c9c4150`
- `cue-toast.mp3.meta` guid `e959156926a24622bd0c7ba3b4d317dc`

## [OBSERVED] 블로커 — toast 목표 길이(≤0.3s) 미달성

ElevenLabs `/v1/sound-generation` API는 `duration_seconds`에 **0.5s
하한**을 강제한다. `0.3`으로 첫 시도 시 `HTTP 400
invalid_generation_settings: "expected to be greater or equal to 0.5 and
less or equal to 30, received 0.3"`로 거부됨. 요청받은 "≤0.3s" 스펙은
API 레벨에서 물리적으로 불가능 — `cue-toast.mp3`는 API 하한인 `0.5s`로
생성했고 실측 0.522s. 프롬프트는 "extremely short... decays almost
instantly"로 최대한 타이트한 트랜지언트를 지시해 청감상 체감 길이는
줄였으나, 파일 길이 자체는 0.3s로 만들 수 없다. ui-lane3이 0.3s 미만을
엄격히 요구한다면 사후 트리밍(ffmpeg 등)이 필요 — 이번 작업 범위에는
포함하지 않음(스크립트 계약은 API 산출물 그대로 저장).

## 실행 명령

```bash
# tools/audio/gen_sfx.py CUES 테이블에 loot-fine / loot-epic / toast 3개
# 항목 추가 후, 기존 11종은 건드리지 않고 신규 3종만 지정 실행:
python3 tools/audio/gen_sfx.py loot-fine loot-epic toast
```

키 해석: `$ELEVENLABS_API_KEY` env 미설정 상태였고, 스크립트의 기존
fallback 경로(`../Abyssal-Surge/.env.game-audio`, 저장소 밖, 읽기 전용)에서
자동 해석됨 — 별도 조치 불필요.

첫 실행에서 `toast`가 0.3s로 HTTP 400 실패(위 블로커) → 스크립트가
프로세스 전체를 즉시 중단해 `docs/provenance/audio.json`이 갱신되지
않았음(최종 write가 루프 종료 후 1회이므로). `loot-fine`/`loot-epic`은
이미 디스크에 써졌으나 provenance 미기록 상태였음. `toast` duration을
0.5로 수정 후 동일 3개 인자로 재실행 → 3종 모두 성공, provenance 정상
기록.

## docs/provenance/audio.json 갱신

`cues.loot-fine`, `cues.loot-epic`, `cues.toast` 3개 항목 추가(prompt,
promptInfluence 0.55, durationSeconds, bytes, file 경로 포함). 기존 11개
cue 항목은 무수정.

## 범위 준수

- 커밋 안 함.
- `Assets/Scripts/**` 무수정.
- API 키를 코드/로그/리포트에 노출하지 않음(env 소스만 언급).
- 파일명·경로는 ui-lane3 계약 그대로 (`cue-loot-fine.mp3`,
  `cue-loot-epic.mp3`, `cue-toast.mp3`).

## git status (관련 경로만)

```
 M docs/provenance/audio.json
 M tools/audio/gen_sfx.py
?? Assets/Resources/Audio/cue-loot-epic.mp3
?? Assets/Resources/Audio/cue-loot-epic.mp3.meta
?? Assets/Resources/Audio/cue-loot-fine.mp3
?? Assets/Resources/Audio/cue-loot-fine.mp3.meta
?? Assets/Resources/Audio/cue-toast.mp3
?? Assets/Resources/Audio/cue-toast.mp3.meta
```
