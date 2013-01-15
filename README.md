ShutdownScheduler
===========

When run, this program monitors keyboard, mouse, network activity, AND CPU percentage to ensure that the system is
not in use.  If all conditions are true for the set time period, the system will be shut down.  

This program is designed to be run by the windows scheduler or some other scheduler.  
I have written a tray application in Autohotkey to easily enable or disable the scheduled task,
please see the github project for that:
https://github.com/jdunne525/TrayScheduler

![ScreenShot](https://github.com/jdunne525/ShutdownScheduler/blob/master/screenshot.PNG?raw=true)