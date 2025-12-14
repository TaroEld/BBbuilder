@echo off
REM This simple batch file recursively decrypts and decompiles all .cnut files in a directory.
REM To use it, run the batch file from the directory containing bbsq.exe and nutcracker.exe,
REM and give the directory to process as a parameter on the command line.
REM E.g.  massdecompile C:\users\me\desktop\scripts

for /r %1 %%f in (*.cnut) do echo %%~pnf & copy "%%f" ~tmp.cnut >nul & bbsq -d ~tmp.cnut & nutcracker ~tmp.cnut >"%%~dpnf.nut" & del ~tmp.*nut
