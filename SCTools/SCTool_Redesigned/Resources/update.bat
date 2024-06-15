@echo off
setlocal

REM 현재 실행 중인 배치파일의 위치를 변수로 저장
set current_dir=%~dp0
set update_dir=%current_dir%updates

timeout /t 1

REM 새 버전의 shatagon.exe 파일을 update 폴더에서 복사
if exist "%update_dir%\Shatagon.exe" (
    echo Update the new Shatagon.exe file...
    move /y "%update_dir%\Shatagon.exe" "%current_dir%Shatagon.exe"
    if %errorlevel% equ 0 (
        echo The update completed successfully.
        set "update_status=0"
    ) else (
        echo The update failed.
        set "update_status=1"
    )
) else (
    echo The update file was not found.
    set "update_status=1"
    goto :end
)

REM update 폴더 및 그 안의 모든 파일을 삭제
if exist "%update_dir%" (
    echo Delete the ''updates' older...
    rmdir /s /q "%update_dir%"
)

:end
REM shatagon.exe 파일을 update_status 파라미터와 함께 실행
echo Run Shatagon.exe with the value update_status %update_status%...
start "" "%current_dir%shatagon.exe" update_status %update_status%

endlocal