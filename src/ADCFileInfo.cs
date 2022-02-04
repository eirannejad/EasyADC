using System;

namespace EasyADC
{
    public class ADCFileInfo
    {
        public string Name { get; internal set; }
        public string RelativePath { get; internal set; }
        public string WorkspacePath { get; internal set; }

        public bool IsFolder { get; internal set; }

        public DateTime LastModifiedTimeStamp { get; internal set; }

        public bool CanDelete { get; internal set; }
        public bool CanRename { get; internal set; }
        public bool CanMove { get; internal set; }

        public string LockOwner { get; internal set; }
        public DateTime LockTimeStamp { get; internal set; }
    }
}
