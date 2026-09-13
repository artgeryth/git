@echo off
chcp 65001 >nul
title 娅娅桌面宠物 · 自检
cd /d "%~dp0.."

rem 一键自检：不出图不用点鼠标，跑完把结果写到 data\selftest\
if not exist "娅娅桌面宠物.exe" (
  echo [ERROR] 还没有编译，请先运行 tools\build.bat
  pause
  exit /b 1
)

echo 正在自检（大约 20 秒，中间她会真的在屏幕上做动作）...
"娅娅桌面宠物.exe" --selftest "%~dp0..\data\selftest"

echo.
echo   结果：%~dp0..\data\selftest\
echo     selftest.txt   文字结果（词库/路由/等级/禁词/聊天全链路）
echo     01-pet-frame.png  她这一帧长什么样
echo     02-chat.png       聊天窗
echo     03-settings.png   设置窗
echo     10-action-*.png   7 个动作各一帧
echo.
start "" "%~dp0..\data\selftest"
pause
