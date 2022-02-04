using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

using Autodesk.DesktopConnector.API;
using System.Diagnostics;

namespace EasyADC
{
    public class ADC
    {
        #region _private
        const string _adcName = "Autodesk Desktop Connector";
        const string _adcShortname = "ADC";
        const string _adcSchema = "{0}://";

        dynamic Connector { get; set; } = default;

        // separate method so connecter type reference does not
        // force loading the api assembly in constructor
        void Connect() => Connector = new DesktopConnectorService();
        #endregion

        public ADC(string adcLocation, string adcAssemblyName)
        {
            ADCAssemblyDirectory = adcLocation;
            ADCAssemblyName = adcAssemblyName;
        }

        public ADC() : this(
            adcLocation: @"C:\Program Files\Autodesk\Desktop Connector",
            adcAssemblyName: "Autodesk.DesktopConnector.API.dll"
        )
        {
            string assmPath = ADCAssemblyPath;
            if (File.Exists(assmPath))
            {
                // load adc assembly
                Assembly adcAssembly;
                try
                {
                    adcAssembly = Assembly.LoadFrom(ADCAssemblyPath);
                    Connect();
                }
                catch (Exception loadEx)
                {
                    throw new Exception($"Error loading ADC assembly at {assmPath} | {loadEx}");
                }
            }
            else
                throw new Exception($"Can not load ADC assembly at {assmPath}");
        }

        /// <summary>
        /// Directory of ADC assembly file
        /// </summary>
        public string ADCAssemblyDirectory { get; }

        /// <summary>
        /// File name of ADC assembly file
        /// </summary>
        public string ADCAssemblyName { get; }

        /// <summary>
        /// Full path of ADC assembly file
        /// </summary>
        public string ADCAssemblyPath => Path.Combine(ADCAssemblyDirectory, ADCAssemblyName);

        /// <summary>
        /// Check if ADC service is available
        /// </summary>
        public bool IsReady
        {
            get
            {
                try
                {
                    Connector.Discover();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        #region Drive
        IEnumerable<dynamic> GetDrives()
        {
            var drives = new List<dynamic>();
            try
            {
                foreach (dynamic drive in Connector.GetDrives())
                    drives.Add(drive);
            }
            catch { }
            return drives;
        }

        dynamic GetDriveProperties(dynamic drive)
        {
            if (drive is Drive drv)
            {
                try
                {
                    return Connector.GetPropertyDefinitions(drv.Id);
                }
                catch { }
            }
            return null;
        }

        dynamic GetDriveFromPath(string path)
        {
            // get all drives
            foreach (Drive drive in GetDrives())
            {
                // check if path starts with the drive schema
                string driveSchema = string.Format(_adcSchema, drive.Name);
                if (path.ToLower().StartsWith(driveSchema.ToLower()))
                    return drive;
            }
            return null;
        }

        dynamic GetDriveFromLocalPath(string path)
        {
            // get all drives
            foreach (Drive drive in GetDrives())
            {
                // check if path starts with the drive schema
                string drivePath = new Uri(drive.WorkspaceLocation).LocalPath;
                string normPath = new Uri(path).LocalPath;
                if (normPath.StartsWith(drivePath))
                    return drive;
            }
            return null;
        }

        string ConvertDrivePathToLocalPath(dynamic drive, string path)
        {
            if (drive is Drive drv)
            {
                string driveSchema = string.Format(_adcSchema, drv.Name);
                return Path.Combine(
                    drv.WorkspaceLocation,
                    path.Replace(driveSchema, "")
                );
            }
            return null;
        }

        string EnsureLocalPath(string path)
        {
            dynamic drive = GetDriveFromPath(path) ?? GetDriveFromLocalPath(path);
            if (drive != null)
                return ConvertDrivePathToLocalPath(drive, path);
            return null;
        }
        #endregion

        #region File
        /// <summary>
        /// Check if file exists on any of ADC drives
        /// </summary>
        /// <param name="path">Path of file to check</param>
        /// <returns>true if file exists on any of ADC drives</returns>
        public bool Contains(string path) => GetItem(path) != null;

        /// <summary>
        /// Convert ADC BIM360 drive path to local path
        /// </summary>
        /// <param name="serverPath">BIM360 path of file to convert e.g. BIM360://path/to/file</param>
        /// <returns>Local path of the given file</returns>
        public string GetLocalPath(string serverPath)
        {
            dynamic drive = GetDriveFromPath(serverPath);
            if (drive != null)
                return ConvertDrivePathToLocalPath(drive, serverPath);
            return null;
        }

        dynamic GetItem(string path)
        {
            if (EnsureLocalPath(path) is string localPath)
            {
                try
                {
                    if (!File.Exists(localPath))
                        return null;

                    IEnumerable<WorkspacePathToItemResult> res =
                        Connector.GetItemsByWorkspacePaths(new string[] { path });
                    if (res is null)
                        return null;

                    // grab the first item
                    // we only accept one since path is to a single file
                    return res.First().Item;
                }
                catch { }
            }
            return null;
        }

        dynamic GetItemDrive(dynamic item)
        {
            foreach (Drive drive in GetDrives())
                if (drive.Id == item.DriveId)
                    return drive;
            return null;
        }
        #endregion

        #region File Properties
        /// <summary>
        /// Get information about give file
        /// </summary>
        /// <param name="path">Path of file to get info</param>
        /// <returns>File info if file exists on ADC drives</returns>
        public ADCFileInfo GetFileInfo(string path)
        {
            if (GetItem(path) is Item item)
            {
                var fileInfo = new ADCFileInfo()
                {
                    Name = item.Name,
                    RelativePath = item.RelativePath,
                    WorkspacePath = item.WorkspacePath,
                    IsFolder = item.IsFolder,
                    LastModifiedTimeStamp = item.LastModifiedDate,
                    CanDelete = item.CanDelete,
                    CanMove = item.CanMove,
                    CanRename = item.CanRename
                };

                if (GetItemLockStatus(item) is LockStatus lockStatus)
                {
                    fileInfo.LockOwner = lockStatus.LockOwner;
                    fileInfo.LockTimeStamp = lockStatus.LockTime;
                }

                return fileInfo;
            }
            return null;
        }

        dynamic GetItemPropertyIdValue(dynamic drive, dynamic item, string propId)
        {
            foreach (PropertyDefinition propDef in GetDriveProperties(drive))
            {
                if (propDef.Id == propId)
                {
                    try
                    {
                        PropertyValues res = Connector.GetProperties(
                            new List<ItemId> { item.Id },
                            new List<string> { propDef.Id }
                        );
                        if (res.Values != null && res.Values.Any())
                            return res.Values.First();
                    }
                    catch { }
                }
            }
            return null;
        }
        #endregion

        #region File Lock
        /// <summary>
        /// Check if file is locked
        /// </summary>
        /// <param name="path">Path of file to check</param>
        /// <returns>true if file is locked by this user or others</returns>
        public bool IsLocked(string path)
        {
            if (GetItem(path) is Item item)
            {
                if (GetItemLockStatus(item) is LockStatus lockStatus)
                    return lockStatus.State > LockState.NotLocked;
            }
            return false;
        }

        /// <summary>
        /// Check if file is locked by other
        /// </summary>
        /// <param name="path">Path of file to check</param>
        /// <returns>true if file is locked by others</returns>
        public bool IsLockedByOther(string path)
        {
            if (GetItem(path) is Item item)
            {
                if (GetItemLockStatus(item) is LockStatus lockStatus)
                    return lockStatus.State == LockState.LockedByOther;
            }
            return false;
        }

        /// <summary>
        /// Lock given file
        /// </summary>
        /// <param name="path">Path of file to lock</param>
        /// <returns>true if file is successfully locked</returns>
        public bool LockFile(string path)
        {
            if (GetItem(path) is Item item)
            {
                try
                {
                    LockResponse res = Connector.LockFile(item.Id);
                    if (LockResult.Success == res.Result)
                        return true;
                }
                catch { }
            }
            return false;
        }

        /// <summary>
        /// Unlock given file
        /// </summary>
        /// <param name="path">Path of file to unlock</param>
        /// <returns>true if file is successfully unlocked</returns>
        public bool UnlockFile(string path)
        {
            if (GetItem(path) is Item item)
            {
                try
                {
                    LockResponse res = Connector.UnlockFile(item.Id);
                    if (LockResult.Success == res.Result)
                        return true;
                }
                catch { }
            }
            return false;
        }

        dynamic GetItemLockStatus(dynamic item)
        {
            try
            {
                LockStatusResponse res =
                    Connector.GetLockStatus(new List<ItemId> { item.Id });
                if (res.Status != null && res.Status.Any())
                    return res.Status.First();
            }
            catch { }
            return null;
        }
        #endregion

        #region File Sync
        /// <summary>
        /// Check if given file is synchronized
        /// </summary>
        /// <param name="path">Path of file to check</param>
        /// <returns>true if synchronized</returns>
        public bool IsSynced(string path)
        {
            if (GetItem(path) is Item item)
            {
                if (GetItemDrive(item) is Drive drive)
                {
                    // ADC uses translated property names so
                    // check status property by its type "LocalState"
                    // see https://github.com/eirannejad/pyRevit/issues/1152
                    // ADC version 15 changed property_id_value
                    // see https://github.com/eirannejad/pyRevit/issues/1371
                    if (GetItemPropertyIdValue(drive, item, "DesktopConnector.Core.LocalState") is PropertyValue propValue)
                        return ((string)propValue.Value).Equals("Cached", StringComparison.OrdinalIgnoreCase)
                            || ((string)propValue.Value).Equals("Synced", StringComparison.OrdinalIgnoreCase);
                }
            }
            return false;
        }

        /// <summary>
        /// Synchronize given file
        /// </summary>
        /// <param name="path">Path of file to synchronized</param>
        /// <param name="forceSync">Synchronize even if file is already synchronized</param>
        public void SyncFile(string path, bool forceSync = false)
        {
            if (!forceSync && IsSynced(path))
                return;

            if (GetItem(path) is Item item)
            {
                if (EnsureLocalPath(path) is string localPath)
                {
                    // force release files from processes
                    foreach (int pid in Process.GetProcesses().Select(p => p.Id))
                        Connector.FileClosedWithinRunningProcess(pid, localPath);
                    Connector.SyncFiles(new List<ItemId> { item.Id });
                }
            }
        }
        #endregion
    }
}