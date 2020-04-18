# OpenTrackDSUServer

Receives OpenTrack data and outputs it to any DSU clients

## Configuration

A configuration json "appsettings.json" is shipped with the application.  This file contains application settings that can be modified.

## OpenTrack

Please go to their [github](https://github.com/opentrack/opentrack) to get more information on how to configure and use OpenTrack.

OpenTrack should be running with its output set to "UDP over network".  To match the default settings of this project, you will need to click the wrench icon and change the the ip to "127.0.0.1".  Alternatively, you can modify this application's json configuration.

## DSU Clients

This application listens for OpenTrack data and will attempt to determine the rotational and position speed changes to send to any DSU clients (typically video game emulators).

You may need to modify this application's DSU ip/port in the json to match what is specified in the emulator..

Citra note: While the "Test" button in the UI does not work, tracking is still available in game
