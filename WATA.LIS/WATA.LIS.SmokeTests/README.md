# WATA.LIS.SmokeTests

간단한 콘솔 스모크 테스트:
- config: SystemConfig 파서 검증
- db: PostgreSQL 스키마/테이블 마이그레이션 검증

사용법
- 모든 테스트: (기본)
  - dotnet run -c Release --project .\WATA.LIS\WATA.LIS.SmokeTests -- all
- Config만:
  - dotnet run -c Release --project .\WATA.LIS\WATA.LIS.SmokeTests -- config
- DB만:
  - 환경변수 WATA_LIS_CONN 또는 코드 내 기본값 사용
  - dotnet run -c Release --project .\WATA.LIS\WATA.LIS.SmokeTests -- db
