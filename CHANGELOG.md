# 0.2.5

- 고블린 수송 포드 난민 사망으로 생기는 `피난민 거부` 무드 생각 면제
- 비고블린 난민과 다른 자선 거부 판정은 유지

# 0.2.4

- Prisoner Management Panel 수술 정책 종족 목록에 `MUGB 고블린/홉고블린` 통합 항목 추가
- 고블린과 홉고블린에만 해당 정책을 배정하고 다른 인간 제노타입은 제외
- 두 제노타입의 수술 부위 선택에 인간 신체 구조 사용

# 0.2.3

- `인간 시체` 아래에 MUGB 고블린/홉고블린 시체 특수 필터 추가
- 고블린/홉고블린과 그 외 인간형 시체를 작업 계획서에서 분리 선택 가능
- 필터 판정은 `Corpse.InnerPawn`의 MUGB 제노타입/핵심 유전자를 사용
- 한국어/영어 특수 필터 번역 추가
- 필터 선택과 이념 면책 판정을 분리 유지

# 0.2.2

- 한국어 번역 보강
- 한국어 밈 명칭을 `단, 고블린은 제외`로 변경
- 한국어 이념 명칭 생성 규칙 번역 추가
- MUGB 고블린 페르몬 적응과 밈의 상호작용 설명 추가
- 페르몬 적응이 비고블린 인간형 고기 생각을 제거하지 못하도록 하는 기존 C# 보호 로직 문서화

# 0.2.0

- 전역 무드 수치 변경 삭제
- 전역 요리 필터 변경 삭제
- 전역 페로몬 컴포넌트 제거 삭제
- 면책 스위치를 `GDGC_GoblinExceptionalism` 밈 하나로 통일
- 수신자 이념과 MUGB 고블린 피해자를 함께 검사하도록 C# 재작성
- 인간과 HAR 종족에 대한 기존 식인·처형·장기 적출 판정 유지
- 피해자 자신의 고통 및 원한 생각은 유지
- DLL 미포함 상태를 메타데이터와 설명서에 명시

## v0.2.2
- Linux build setup now follows the `STEAM_APPS` / `STEAM_OS` convention used by the working RimWorld project.
- Replaced the fragile local `0Harmony.dll` HintPath with the official `Lib.Harmony.Ref` compile-time package.
- Build output is written directly to `1.6/Assemblies/GDGC.dll`.
- Added a root `justfile` with build, install, enable, format, and clean recipes.
