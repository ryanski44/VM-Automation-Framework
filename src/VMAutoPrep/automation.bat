call c:\Automation\map.bat
xcopy /I /E /C /H /K /R /Y z:\client\* c:\Automation
start c:\Automation\JobManagerClient.exe