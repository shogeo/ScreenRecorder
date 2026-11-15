using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Recording
{
    // The encoder must never keep recording after its owning app has exited.
    internal sealed class EncoderJob : IDisposable
    {
        private readonly SafeFileHandle handle;
        internal EncoderJob(Process process)
        {
            handle = CreateJobObject(IntPtr.Zero,null);
            if (handle.IsInvalid) throw new Win32Exception();
            var limits = new ExtendedLimits();
            limits.Basic.Flags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(handle,9,ref limits,Marshal.SizeOf(typeof(ExtendedLimits))) || !AssignProcessToJobObject(handle,process.Handle))
            { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error,"Could not attach the encoder to Recording's lifetime."); }
        }
        public void Dispose() => handle.Dispose();
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits { public long PerProcessTime,PerJobTime; public uint Flags; public UIntPtr MinWorkingSet,MaxWorkingSet; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass,SchedulingClass; }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory; }
        [DllImport("kernel32.dll",CharSet = CharSet.Unicode,SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes,string name);
        [DllImport("kernel32.dll",SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job,int type,ref ExtendedLimits limits,int size);
        [DllImport("kernel32.dll",SetLastError = true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
    }
}
