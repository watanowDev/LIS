@echo off
echo ========================================
echo WATA.LIS 최적화 배포 스크립트
echo ========================================
echo.

REM 이전 빌드 정리
echo [1/4] 이전 빌드 정리 중...
dotnet clean WATA.LIS\WATA.LIS.csproj -c Release

REM NuGet 복원
echo [2/4] NuGet 패키지 복원 중...
dotnet restore WATA.LIS\WATA.LIS.sln

REM Release 빌드
echo [3/4] Release 빌드 중...
dotnet build WATA.LIS\WATA.LIS.sln -c Release

REM 최적화된 Publish
echo [4/4] 최적화 배포 생성 중...
dotnet publish WATA.LIS\WATA.LIS.csproj -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishReadyToRun=false ^
  -p:PublishTrimmed=false ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:IncludeAllContentForSelfExtract=false ^
  -p:EnableCompressionInSingleFile=true ^
  -p:IsPublishing=true ^
  -o WATA.LIS\bin\publish\OptimizedRelease

echo.
echo ========================================
echo 배포 완료!
echo 경로: WATA.LIS\bin\publish\OptimizedRelease\
echo.
echo 주의: SystemConfig 폴더와 Modules 폴더가
echo       EXE 파일과 같은 위치에 있어야 합니다!
echo ========================================
pause
