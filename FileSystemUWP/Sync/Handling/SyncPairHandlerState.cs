﻿namespace FileSystemUWP.Sync.Handling
{
    public enum SyncPairHandlerState
    {
        WaitForStart,
        Running,
        Finished,
        Error,
        Canceled,
    }
}
