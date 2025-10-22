@echo off
echo ========================================
echo WATA.LIS ����ȭ ���� ��ũ��Ʈ
echo ========================================
echo.

REM ���� ���� ����
echo [1/4] ���� ���� ���� ��...
dotnet clean WATA.LIS\WATA.LIS.csproj -c Release

REM NuGet ����
echo [2/4] NuGet ��Ű�� ���� ��...
dotnet restore WATA.LIS\WATA.LIS.sln

REM Release ����
echo [3/4] Release ���� ��...
dotnet build WATA.LIS\WATA.LIS.sln -c Release

REM ����ȭ�� Publish
echo [4/4] ����ȭ ���� ���� ��...
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
echo ���� �Ϸ�!
echo ���: WATA.LIS\bin\publish\OptimizedRelease\
echo.
echo ����: SystemConfig ������ Modules ������
echo       EXE ���ϰ� ���� ��ġ�� �־�� �մϴ�!
echo ========================================
pause
