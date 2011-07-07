taskkill /F /IM JobManagerClient.exe /T
miso NULL -umnt 1
net use z: /delete
del /f /s /q TestDLL
del /f /s /q JobManager*
del /f /s /q *.log
regedit /s run_once_automation.reg
shutdown -s -t 15