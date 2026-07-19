@echo off
SET DLL_PATH="D:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\Managed\Assembly-CSharp.dll"
SET FALLBACK_PATH="C:\Users\pc\IdeaProjects\Stones\game_files\Assembly-CSharp.dll"

:: Check if the primary path exists. If not, switch to the fallback.
IF NOT EXIST %DLL_PATH% (
    SET DLL_PATH=%FALLBACK_PATH%
)

if "%1"=="type" (
    ilspycmd %DLL_PATH% -t %2
) else if "%1"=="search" (
    ilspycmd %DLL_PATH% --list-types | findstr /I %2
) else (
    echo Usage: ilspy_helper.bat [type|search] [TargetName]
)