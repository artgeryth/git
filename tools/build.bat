@echo off
chcp 65001 >nul
title 编译 娅娅桌面宠物
cd /d "%~dp0.."

rem 用 .NET Framework 自带的 csc 编译，不需要装 Visual Studio / dotnet SDK
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] 找不到 csc.exe（需要 .NET Framework 4.x，Win7 以上系统自带）
  pause
  exit /b 1
)

rem 图标素材：默认用你自己的立绘头像；没有立绘（比如公开仓库不带第三方素材）就用原创占位形象。
rem 想强制指定：set YAYA_ICON_SRC=content\placeholder\pet-avatar.png
set "ICOSRC=%YAYA_ICON_SRC%"
if "%ICOSRC%"=="" set "ICOSRC=content\sprites\pet-avatar.png"
if not exist "%ICOSRC%" set "ICOSRC=content\placeholder\pet-avatar.png"
echo [1/3] 生成程序图标（用 %ICOSRC%）...
"%CSC%" /nologo /target:exe /out:tools\make-icon.exe /reference:System.Drawing.dll tools\MakeIcon.cs
if errorlevel 1 ( echo [ERROR] 图标工具编译失败 & pause & exit /b 1 )
tools\make-icon.exe "%ICOSRC%" app.ico
if errorlevel 1 ( echo [ERROR] 生成图标失败 & pause & exit /b 1 )
rem 桌面上那个快捷方式指向这个独立的 .ico 文件：
rem 图标路径换个文件，Windows 就不会拿旧缓存糊弄，图标立刻变新
copy /y app.ico "娅娅.ico" >nul

echo [2/3] 编译主程序 ...
"%CSC%" /nologo /target:winexe /codepage:65001 /optimize+ ^
  /out:"娅娅桌面宠物.exe" ^
  /win32manifest:src\app.manifest ^
  /win32icon:app.ico ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Security.dll ^
  src\*.cs
if errorlevel 1 (
  echo.
  echo [ERROR] 编译失败，上面是 csc 的报错
  pause
  exit /b 1
)

echo [3/3] 检查素材 ...
if not exist "content\lines.json"    echo   [警告] 缺少 content\lines.json（她会没词可说）
if not exist "content\sprites\pet-front.png" echo   [警告] 缺少 content\sprites\pet-front.png（她会看不见）

echo.
echo   完成 -^> 娅娅桌面宠物.exe
echo   想分享给别人：跑一次 tools\make-package.bat 生成 zip
echo.
pause
