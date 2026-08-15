# Quarantine — 트리를 컴파일 불가로 만든 미추적 파일 보관소

여기 있는 파일은 **삭제된 것이 아니라 옮겨진 것**이다. 원본 내용 그대로 보존돼
있고, 소유 레인이 아래 결함을 고친 뒤 원위치로 되돌리면 된다.

배포 게이트는 컴파일되는 트리를 전제로 한다. 미추적 파일이라도 `Assets/` 아래
있으면 Unity는 그것을 컴파일하므로, 커밋되지 않았다는 사실이 그 파일을
무해하게 만들어 주지 않는다.

---

## SkillRoll.cs.quarantined

**원위치**: `Assets/Scripts/Sim/SkillRoll.cs` (+ `.cs.meta`)
**격리 시각**: 2026-08-12 (cycle 배포 레인)
**상태**: 미추적(`??`) — 어떤 커밋에도 들어간 적 없음

### 왜 옮겼나 — [OBSERVED]

```
$ bash tools/unity_batch.sh import-only
Assets/Scripts/Sim/SkillRoll.cs(3,7): error CS0246:
The type or namespace name 'UnityEngine' could not be found
## Script Compilation Error for: Csc CinderCourt.Sim.dll (+2 others)
Scripts have compiler errors.
```

`Assets/Scripts/Sim/CinderCourt.Sim.asmdef`는 `noEngineReferences: true`다
(CLAUDE.md §1: 심은 UnityEngine 참조 금지인 순수 C# 결정론 시뮬레이션).
이 파일은 3행에서 `using UnityEngine;`을 한다. **심 폴더 전체에서 UnityEngine을
참조하는 유일한 파일**이며, 그 한 줄이 `CinderCourt.Sim`과 여기에 의존하는
어셈블리 2개를 함께 무너뜨린다.

결과: 이 파일이 트리에 있는 동안 EditMode 테스트도, WebGL 빌드도, 릴리스
증거 생성도 **전부 실행 불가**였다. 배포 파이프라인 전체가 여기서 멈춰 있었다.

### 왜 그냥 고치지 않았나

고칠 것이 한 줄이 아니기 때문이다. 이 파일은 구현이 아니라 **자리표시자**다:

```csharp
public static int Roll(int enemyId, int wave, int attackOrdinal)
{
    // Placeholder: Implementation will be detailed in Phase 1d implementation phase.
    return 1;
}
```

- 항상 `1`을 반환한다 — 롤이 아니다.
- 네임스페이스가 없다. 심 코드는 `CinderCourt.Sim` 안에 있어야 한다.
- 어디에서도 호출되지 않는다 (`grep -rn SkillRoll --include=*.cs Assets/` → 0건).
- 설계 의도는 `_workspace/current/design/phase-1d-implementation-spec.md`의
  "W4: SkillRoll(enemyId, wave, attackOrdinal) + 아키타입 임계·천장 정의"인데,
  임계도 천장도 아직 코드에 없다.

`using UnityEngine;`만 지우면 컴파일은 통과한다. 그러면 **미검증 스텁이
동결된 심 계약 위에 올라탄 채 배포된다.** 심 인터페이스는 동결이고(CLAUDE.md §1),
`return 1`은 골든 다이제스트가 검사하지 않는 죽은 코드로 출하된다. 컴파일을
통과시키는 것과 요구사항을 만족시키는 것은 다른 명제다.

### 복귀 절차 (소유 레인)

1. `using UnityEngine;` / `using System;` 제거 — 심은 엔진 참조 금지.
2. `namespace CinderCourt.Sim { ... }`로 감싼다.
3. `Roll`을 실제로 구현하고 결정론을 확인한다 — 심 난수는 프레임 순서에
   의존하면 안 되고, 골든이 움직이면 그 이동이 의도된 것임을 증명해야 한다
   (CLAUDE.md §4: 다이제스트 회귀는 Unity 런타임 내 비교만 유효).
4. `bash tools/unity_batch.sh tests`로 골든 포함 EditMode 전체를 돌린다.
5. 호출자를 붙인다. 호출되지 않는 심 코드는 계약을 넓히지 않는다.

되돌릴 때는 `.meta`를 새로 만들게 두면 된다 — 옛 GUID는 어디에서도 참조되지
않으므로 복원할 이유가 없다.

### 일반화

**미추적은 무해와 동의어가 아니다.** `git status`가 `??`로 표시한다고 해서
빌드 시스템이 그것을 건너뛰지 않는다. 컴파일 게이트(`import-only`, ~15초)가
있는 이유가 이것이고, 이 결함은 그 게이트를 한 번 돌리는 것으로 즉시 드러났다.
