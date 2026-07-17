@echo off
rem QA launcher - starts DueMap Web + Worker in separate windows
cd /d D:\Projects\DueMap\DueMap
start "DueMap.Web" cmd /k dotnet run --project src\DueMap.Web --launch-profile DueMap.Web
timeout /t 5 /nobreak >nul
start "DueMap.Worker" cmd /k dotnet run --project src\DueMap.Worker
