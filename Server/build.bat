@echo off
rem Build RankServer.sln (Release x64) with MSBuild, then run the tests
rem   build.bat          Release
rem   build.bat Debug    Debug
set CONFIG=%~1
if "%CONFIG%"=="" set CONFIG=Release
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1
msbuild "%~dp0RankServer.sln" /p:Configuration=%CONFIG% /p:Platform=x64 /m /nologo /v:minimal || exit /b 1
"%~dp0bin\%CONFIG%\RankTests.exe"
