# OpenTrackDSUServer

Receives OpenTrack data and outputs it to any DSU clients

## Configuration

A configuration json "appsettings.json" is shipped with the application.  This file contains application settings that can be modified.

**DSUServerIp**: This is the ip that the emulator will listen on.
**DSUServerPort**: This is the port that the emulator will listen on.

**OpenTrackIp**: This is the ip that OpenTrack will send data to.
**OpenTrackPort**: This is the port that OpenTrack will send data to.

**DebugOpenTrack**: Sometimes helpful if you are debugging the software.  This doesn't send the data to an emulator but instead prints what OpenTrack sent to the console.

**RelativeData**: If this is true, the software will calculate the relative angular velocity expected by the DSU spec.  If this is false, the exact value you will be sent instead.  Defaults to true.

**DivideByGravity**: The DSU spec assumes the accelerometer is acceleration/second.  However, OpenTrack actually provides exact positional data.  Setting this to true will divide the opentrack data by gravity.  Defaults to false.

## OpenTrack

Please go to their [github](https://github.com/opentrack/opentrack) to get more information on how to configure and use OpenTrack.

OpenTrack should be running with its output set to "UDP over network".  To match the default settings of this project, you will need to click the wrench icon and change the the ip to "127.0.0.1".  Alternatively, you can modify this application's json configuration.

## DSU Clients

This application listens for OpenTrack data and will attempt to determine the rotational and position speed changes to send to any DSU clients (typically video game emulators).

You may need to modify this application's DSU ip/port in the json to match what is specified in the emulator..

Citra note: While the "Test" button in the UI does not work, tracking is still available in game
