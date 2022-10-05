# Veeam Repository Reporter
Example application using the Veeam VBR API to query and report on Scaleout Backup Repository state information (capacity, free space, settings)

Load up with Visual Studio or editor if your choice, build/run project.

## Settings
In appsettings.json, modify to your requirements

-   **Host** == your VBR server IP or hostname
-   **Port** == 9419 is the default, leave it as is unless you have changed during deployment
-   **APIVersion** = 1.0-rev1 is the latest in V12 beta 2, this will change as new versions come out
-   **APIRouteVersion** == is the version in the url path, currently this is just v1 as new versions are released in the future we may see versioning come into play
-   **Username** == service account or account username you want to use to connect to the api
-   **Password** == this can be left empty, in which case it will prompt you in the application, however if you are just playing around in a lab feel free to enter the password, or for production use securely inject/encrypt the appsettings file

### Disclaimer
_The material embodied in this software is provided to you "as-is" and without warranty of any kind, express, implied or otherwise, including without limitation, any warranty of fitness for a particular purpose._
