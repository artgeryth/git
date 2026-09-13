@echo off
chcp 65001 >nul
title 打包「公开发布版」（GitHub Releases 用）
cd /d "%~dp0.."

rem ============================================================
rem  给 GitHub / 公开发布用的打包脚本。和 make-package.bat 的区别：
rem    · zip 用英文名，方便挂在 GitHub Releases 上
rem    · 顺带打一个源码 zip
rem    · 打包后会打印内容清单，确认 LICENSE / ATTRIBUTION.md / 双语台词库都在
rem  ⚠ content\sprites 里那张立绘是网传 AI 图：
rem    带着发布可以，但 ATTRIBUTION.md 与 LICENSE 里"来源不明、不主张版权、不建议商用、
rem    收到异议立即移除"的说明必须一起发出去（脚本会自动带上）。
rem    想彻底干净：删掉 content\sprites（或取消 .gitignore 里那三行的注释），
rem    程序会自动改用 content\placeholder 的原创占位形象。
rem ============================================================

set "NAME=娅娅桌面宠物"
set "VER=1.0"
set "STAGE=dist\_build"
set "RUN=dist\YayaDesktopPet"
set "WINZIP=dist\YayaDesktopPet-v%VER%-win.zip"
set "SRCZIP=dist\YayaDesktopPet-v%VER%-source.zip"

if not exist "src\App.cs" ( echo [ERROR] 请在项目根目录运行 & pause & exit /b 1 )

echo [1/6] 准备构建树...
if exist "%STAGE%" rd /s /q "%STAGE%"
del /q "dist\YayaDesktopPet-v%VER%*.zip" 2>nul
if exist "%RUN%" rd /s /q "%RUN%"
mkdir "%STAGE%\content\sprites" 2>nul
mkdir "%STAGE%\content\placeholder" 2>nul
mkdir "%STAGE%\docs" 2>nul
xcopy /e /i /y /q src   "%STAGE%\src"   >nul
xcopy /e /i /y /q tools "%STAGE%\tools" >nul
xcopy /e /i /y /q docs  "%STAGE%\docs"  >nul
copy /y "content\lines.json"    "%STAGE%\content\" >nul
copy /y "content\lines.en.json" "%STAGE%\content\" >nul
copy /y "content\_*.json"       "%STAGE%\content\" >nul
if exist "content\sprites\pet-front.png" (
  copy /y "content\sprites\*.png" "%STAGE%\content\sprites\" >nul
  echo        立绘：使用 content\sprites ^(网传 AI 图，说明见 ATTRIBUTION.md^)
) else (
  echo        立绘：没有，使用原创占位形象
)
copy /y "content\placeholder\*.png" "%STAGE%\content\placeholder\" >nul
copy /y "README.md" "%STAGE%\" >nul
copy /y "README.zh-CN.md" "%STAGE%\" >nul
copy /y "LICENSE" "%STAGE%\" >nul
copy /y "ATTRIBUTION.md" "%STAGE%\" >nul
copy /y "QUICKSTART.txt" "%STAGE%\" >nul
copy /y "使用说明.txt" "%STAGE%\" >nul
copy /y ".gitignore" "%STAGE%\" >nul

echo [2/6] 编译（图标自动用立绘头像；没有立绘则用占位形象）...
pushd "%STAGE%"
call tools\build.bat < nul
if errorlevel 1 ( popd & echo [ERROR] 编译失败 & pause & exit /b 1 )
popd

echo [3/6] 组装运行包...
mkdir "%RUN%\content\sprites" 2>nul
mkdir "%RUN%\content\placeholder" 2>nul
mkdir "%RUN%\docs" 2>nul
copy /y "%STAGE%\%NAME%.exe" "%RUN%\" >nul
copy /y "%STAGE%\娅娅.ico" "%RUN%\" >nul
copy /y "%STAGE%\content\lines.json"    "%RUN%\content\" >nul
copy /y "%STAGE%\content\lines.en.json" "%RUN%\content\" >nul
if exist "%STAGE%\content\sprites\pet-front.png" copy /y "%STAGE%\content\sprites\*.png" "%RUN%\content\sprites\" >nul
copy /y "%STAGE%\content\placeholder\*.png" "%RUN%\content\placeholder\" >nul
copy /y "%STAGE%\docs\*.png" "%RUN%\docs\" >nul
copy /y "%STAGE%\README.md" "%RUN%\" >nul
copy /y "%STAGE%\LICENSE" "%RUN%\" >nul
copy /y "%STAGE%\ATTRIBUTION.md" "%RUN%\" >nul
copy /y "%STAGE%\QUICKSTART.txt" "%RUN%\" >nul
copy /y "%STAGE%\使用说明.txt" "%RUN%\" >nul

echo [4/6] 打包运行版 zip（英文名，挂 GitHub Releases）...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%RUN%' -DestinationPath '%WINZIP%' -Force"

echo [5/6] 打包源码 zip...
del /q "%STAGE%\%NAME%.exe" 2>nul
del /q "%STAGE%\娅娅.ico" 2>nul
del /q "%STAGE%\app.ico" 2>nul
del /q "%STAGE%\tools\*.exe" 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%STAGE%' -DestinationPath '%SRCZIP%' -Force"

echo [6/6] 检查发布包...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Add-Type -AssemblyName System.IO.Compression.FileSystem; $e=[IO.Compression.ZipFile]::OpenRead((Resolve-Path '%WINZIP%')).Entries; $n=@($e | Where-Object { $_.FullName -match 'sprites' }).Count; $lic=@($e | Where-Object { $_.FullName -match 'LICENSE' }).Count; $att=@($e | Where-Object { $_.FullName -match 'ATTRIBUTION' }).Count; $en=@($e | Where-Object { $_.FullName -match 'lines.en.json' }).Count; '  立绘文件 ' + $n + ' 个；LICENSE ' + $lic + '；ATTRIBUTION ' + $att + '；英文台词库 ' + $en; if($lic -eq 0 -or $att -eq 0){'  !! 缺少许可证或授权说明，先别发！'}else{'  OK  许可证与授权说明都在包里'}"

echo.
for %%F in ("%WINZIP%") do echo   %WINZIP%   %%~zF 字节
for %%F in ("%SRCZIP%") do echo   %SRCZIP%   %%~zF 字节
echo.
echo   运行包内容（解压即用）：dist\%NAME%\
echo   上传 GitHub：把这两个 zip 传到 Releases 里即可（别提交进 git）
echo.
pause
