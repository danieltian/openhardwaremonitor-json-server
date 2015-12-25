# OpenHardwareMonitorJsonServer
This project is a spike for a simple JSON server that serves up data about your computer's hardware, using [OpenHardwareMonitor](https://github.com/openhardwaremonitor/openhardwaremonitor).

## Prerequisites
- .NET Framework 4.5.2

## Install and Run
You must run Visual Studio (or the exe in the `Bin/Release` folder) as an administrator; otherwise, the HTTP server won't run properly and you will get an `Access denied` exception. This repo comes with all the required libraries, and you should be able to compile and run it with no additional setup.

## Why?
Although OpenHardwareMonitor's GUI has a JSON server feature (under **Options -> Remote Web Server**, then accessible through `http://localhost:<port>/data.json`), it's pretty heavyweight if all you want is the JSON data for the sensors.

Also, OpenHardwareMonitor hasn't seen a release on their [official site](http://openhardwaremonitor.org/) in over a year and lacks support for the latest hardware. The [Github repo](https://github.com/openhardwaremonitor/openhardwaremonitor) is updated regularly though, and does have support for the latest and greatest. The copy of `OpenHardwareMonitor.dll` in this project was built off master from the Github repo as of 2015/12/25.
