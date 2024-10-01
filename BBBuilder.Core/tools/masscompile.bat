@echo off
REM This simple batch file recursively compiles and encrypts all .nut files in a directory.
REM To use it, run the batch file from the directory containing bbsq.exe and nutcracker.exe,
REM and give the directory to process as a parameter on the command line.
REM E.g.  masscompile C:\users\me\desktop\scripts

for /r %1 %%f in (*.nut) do echo %%~pnf & sq -o "%%~dpnf.cnut" -c "%%f" & bbsq -e "%%~dpnf.cnut"
