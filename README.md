# EasyADC
Easy Wrapper for Autodesk DesktopConnector API


## Get Started

Instantiate an ADC connector:

```csharp
using EasyADC;

// ADC could also be instantiated by passing location and name
// of ADC api assembly e.g. new ADC(adcLocation, adcAssemblyName)
var adc = new ADC();

// Full path of ADC assembly file
adc.ADCAssemblyPath

// Check if ADC service is available
if (adc.IsReady) {
    // Check if file exists on any of ADC drives
    string filePath = @"C:\some\file\path";
    adc.Contains(filePath);

    // Convert ADC BIM360 drive path to local path
    filePath = @"Autodesk Docs://My Org/My Project/Project Files/File.txt"
    adc.GetLocalPath(filePath);

    // Get information about give file
    ADCFileInfo info = adc.GetFileInfo(filePath);

    // Check if file is locked by any user
    adc.IsLocked(filePath);

    // Check if file is locked by other
    adc.IsLockedByOther(filePath);

    // Lock and unlock files
    adc.LockFile(filePath);
    adc.UnlockFile(filePath);

    //Check if given file is synchronized
    adc.IsSynced(filePath);

    // Synchronize given file
    adc.SyncFile(filePath);
}
```
## Developer Notes

- To build, ADC needs to be installed at `C:\Program Files\Autodesk\Desktop Connector\`. See `ADCInstallPath` in [Directory.Build.props](src/Directory.Build.props) file
- Project is intentionally build _without_ optimization. This allows the `ADC` constructor to load the ADC api assembly before calling the functions and removes this burden from the user. See `<Optimize>False</Optimize>` in [ADC.csproj](src/ADC.csproj)
