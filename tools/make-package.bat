@echo off
chcp 65001 >nul
title 打包 娅娅桌面宠物（分享给别人用）
cd /d "%~dp0.."

set "NAME=娅娅桌面宠物"
set "VER=1.1"
set "STAGE=dist\%NAME%"
set "SRCSTAGE=dist\_src\%NAME%-v%VER%-源码"

if not exist "%NAME%.exe" (
  echo [ERROR] 还没编译，请先运行 tools\build.bat
  pause
  exit /b 1
)

echo [1/5] 准备目录 ...
if exist "dist\%NAME%" rd /s /q "dist\%NAME%"
del /q "dist\%NAME%-v%VER%*.zip" 2>nul
mkdir "%STAGE%\content\sprites" 2>nul

echo [2/5] 拷贝运行必需的文件（exe + 图标 + 双语台词库 + 立绘/占位形象 + 说明 + 许可证）...
copy /y "%NAME%.exe" "%STAGE%\" >nul
copy /y "娅娅.ico" "%STAGE%\" >nul
copy /y "使用说明.txt" "%STAGE%\" >nul
copy /y "QUICKSTART.txt" "%STAGE%\" >nul
copy /y "LICENSE" "%STAGE%\" >nul
copy /y "ATTRIBUTION.md" "%STAGE%\" >nul
copy /y "content\lines.json" "%STAGE%\content\" >nul
copy /y "content\lines.en.json" "%STAGE%\content\" >nul
copy /y "content\sprites\*.png" "%STAGE%\content\sprites\" >nul
mkdir "%STAGE%\content\placeholder" 2>nul
copy /y "content\placeholder\*.png" "%STAGE%\content\placeholder\" >nul

echo [3/5] 打包成 zip（解压出来会是一个「%NAME%」文件夹）...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'dist\%NAME%' -DestinationPath 'dist\%NAME%-v%VER%.zip' -Force"

echo [4/5] 顺便打一个源码包（给懂技术的人，想改就能改）...
mkdir "%SRCSTAGE%\content\sprites" 2>nul
mkdir "%SRCSTAGE%\content\placeholder" 2>nul
mkdir "%SRCSTAGE%\docs" 2>nul
xcopy /e /i /y /q src   "%SRCSTAGE%\src"   >nul
xcopy /e /i /y /q tools "%SRCSTAGE%\tools" >nul
xcopy /e /i /y /q docs  "%SRCSTAGE%\docs"  >nul
copy /y "README.md" "%SRCSTAGE%\" >nul
copy /y "README.zh-CN.md" "%SRCSTAGE%\" >nul
copy /y "使用说明.txt" "%SRCSTAGE%\" >nul
copy /y "QUICKSTART.txt" "%SRCSTAGE%\" >nul
copy /y "LICENSE" "%SRCSTAGE%\" >nul
copy /y "ATTRIBUTION.md" "%SRCSTAGE%\" >nul
copy /y "娅娅.ico" "%SRCSTAGE%\" >nul
copy /y "content\lines.json" "%SRCSTAGE%\content\" >nul
copy /y "content\lines.en.json" "%SRCSTAGE%\content\" >nul
copy /y "content\_*.json" "%SRCSTAGE%\content\" >nul
copy /y "content\sprites\*.png" "%SRCSTAGE%\content\sprites\" >nul
copy /y "content\placeholder\*.png" "%SRCSTAGE%\content\placeholder\" >nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'dist\_src\%NAME%-v%VER%-源码' -DestinationPath 'dist\%NAME%-v%VER%-源码.zip' -Force"
rd /s /q "dist\_src"

echo [5/5] 结果 ...
echo.
for %%F in ("dist\%NAME%-v%VER%.zip") do echo   dist\%NAME%-v%VER%.zip          %%~zF 字节
for %%F in ("dist\%NAME%-v%VER%-源码.zip") do echo   dist\%NAME%-v%VER%-源码.zip   %%~zF 字节
echo.
echo   给别人直接发那个「%NAME%-v%VER%.zip」就行：
echo   对方解压到任意文件夹（会得到一个 %NAME% 文件夹），双击里面的 %NAME%.exe 即可
echo   Win7 以上，不用装任何东西
echo.
echo   dist\%NAME% 就是解压后的样子，可以先自己点开试试
echo.
pause
