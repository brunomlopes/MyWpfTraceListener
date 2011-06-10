using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MyTraceListener
{
    public delegate void OnOutputDebugStringHandler(int pid, string text);

    /// <summary>
    /// Taken directly from http://stackoverflow.com/questions/1520119/replicate-functionality-of-dbgview-in-net-global-win32-debug-hooks
    /// </summary>
    public sealed class DebugMonitor
    {

        private DebugMonitor()
        {
            ;
        }

        #region Win32 API Imports

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_DESCRIPTOR
        {
            public byte revision;
            public byte size;
            public short control;
            public IntPtr owner;
            public IntPtr group;
            public IntPtr sacl;
            public IntPtr dacl;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [Flags]
        private enum PageProtection : uint
        {
            NoAccess = 0x01,
            Readonly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            Guard = 0x100,
            NoCache = 0x200,
            WriteCombine = 0x400,
        }


        private const int WAIT_OBJECT_0 = 0;
        private const uint INFINITE = 0xFFFFFFFF;
        private const int ERROR_ALREADY_EXISTS = 183;

        private const uint SECURITY_DESCRIPTOR_REVISION = 1;

        private const uint SECTION_MAP_READ = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint
                                                                                  dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow,
                                                   uint dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool InitializeSecurityDescriptor(ref SECURITY_DESCRIPTOR sd, uint dwRevision);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetSecurityDescriptorDacl(ref SECURITY_DESCRIPTOR sd, bool daclPresent, IntPtr dacl, bool daclDefaulted);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateEvent(ref SECURITY_ATTRIBUTES sa, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool PulseEvent(IntPtr hEvent);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFileMapping(IntPtr hFile,
                                                       ref SECURITY_ATTRIBUTES lpFileMappingAttributes, PageProtection flProtect, uint dwMaximumSizeHigh,
                                                       uint dwMaximumSizeLow, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        private static extern Int32 WaitForSingleObject(IntPtr handle, uint milliseconds);
        #endregion


        public static event OnOutputDebugStringHandler OnOutputDebugString;

        private static IntPtr _mAckEvent = IntPtr.Zero;

        private static IntPtr _mReadyEvent = IntPtr.Zero;

        private static IntPtr _mSharedFile = IntPtr.Zero;

        private static IntPtr _mSharedMem = IntPtr.Zero;

        private static Thread _mCapturer = null;

        private static object m_SyncRoot = new object();

        private static Mutex _mMutex = null;


        public static void Start()
        {
            lock (m_SyncRoot)
            {
                if (_mCapturer != null)
                    throw new ApplicationException("This DebugMonitor is already started.");

                if (Environment.OSVersion.ToString().IndexOf("Microsoft") == -1)
                    throw new NotSupportedException("This DebugMonitor is only supported on Microsoft operating systems.");

                bool createdNew = false;
                _mMutex = new Mutex(false, typeof(DebugMonitor).Namespace, out createdNew);
                if (!createdNew)
                    throw new ApplicationException("There is already an instance of 'DbMon.NET' running.");

                SECURITY_DESCRIPTOR sd = new SECURITY_DESCRIPTOR();

                if (!InitializeSecurityDescriptor(ref sd, SECURITY_DESCRIPTOR_REVISION))
                {
                    throw CreateApplicationException("Failed to initializes the security descriptor.");
                }

                if (!SetSecurityDescriptorDacl(ref sd, true, IntPtr.Zero, false))
                {
                    throw CreateApplicationException("Failed to initializes the security descriptor");
                }

                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();

                _mAckEvent = CreateEvent(ref sa, false, false, "DBWIN_BUFFER_READY");
                if (_mAckEvent == IntPtr.Zero)
                {
                    throw CreateApplicationException("Failed to create event 'DBWIN_BUFFER_READY'");
                }

                _mReadyEvent = CreateEvent(ref sa, false, false, "DBWIN_DATA_READY");
                if (_mReadyEvent == IntPtr.Zero)
                {
                    throw CreateApplicationException("Failed to create event 'DBWIN_DATA_READY'");
                }

                _mSharedFile = CreateFileMapping(new IntPtr(-1), ref sa, PageProtection.ReadWrite, 0, 4096, "DBWIN_BUFFER");
                if (_mSharedFile == IntPtr.Zero)
                {
                    throw CreateApplicationException("Failed to create a file mapping to slot 'DBWIN_BUFFER'");
                }

                _mSharedMem = MapViewOfFile(_mSharedFile, SECTION_MAP_READ, 0, 0, 512);
                if (_mSharedMem == IntPtr.Zero)
                {
                    throw CreateApplicationException("Failed to create a mapping view for slot 'DBWIN_BUFFER'");
                }

                _mCapturer = new Thread(Capture);
                _mCapturer.IsBackground = true;
                _mCapturer.Start();
            }
        }

        private static void Capture()
        {
            try
            {
                IntPtr pString = new IntPtr(
                    _mSharedMem.ToInt32() + Marshal.SizeOf(typeof(int))
                    );

                while (true)
                {
                    SetEvent(_mAckEvent);

                    int ret = WaitForSingleObject(_mReadyEvent, INFINITE);

                    if (_mCapturer == null)
                        break;

                    if (ret == WAIT_OBJECT_0)
                    {
                        FireOnOutputDebugString(
                            Marshal.ReadInt32(_mSharedMem),
                            Marshal.PtrToStringAnsi(pString));
                    }
                }

            }
            catch
            {
                throw;

            }
            finally
            {
                Dispose();
            }
        }

        private static void FireOnOutputDebugString(int pid, string text)
        {
            if (OnOutputDebugString == null)
                return;

#if !DEBUG
            try
            {
#endif
            OnOutputDebugString(pid, text);
#if !DEBUG
            }
            catch (Exception ex)
            {
                Console.WriteLine("An 'OnOutputDebugString' handler failed to execute: " + ex.ToString());
            }
#endif
        }


        private static void Dispose()
        {
            if (_mAckEvent != IntPtr.Zero)
            {
                if (!CloseHandle(_mAckEvent))
                {
                    throw CreateApplicationException("Failed to close handle for 'AckEvent'");
                }
                _mAckEvent = IntPtr.Zero;
            }

            if (_mReadyEvent != IntPtr.Zero)
            {
                if (!CloseHandle(_mReadyEvent))
                {
                    throw CreateApplicationException("Failed to close handle for 'ReadyEvent'");
                }
                _mReadyEvent = IntPtr.Zero;
            }

            if (_mSharedFile != IntPtr.Zero)
            {
                if (!CloseHandle(_mSharedFile))
                {
                    throw CreateApplicationException("Failed to close handle for 'SharedFile'");
                }
                _mSharedFile = IntPtr.Zero;
            }


            if (_mSharedMem != IntPtr.Zero)
            {
                if (!UnmapViewOfFile(_mSharedMem))
                {
                    throw CreateApplicationException("Failed to unmap view for slot 'DBWIN_BUFFER'");
                }
                _mSharedMem = IntPtr.Zero;
            }

            if (_mMutex != null)
            {
                _mMutex.Close();
                _mMutex = null;
            }
        }

        public static void Stop()
        {
            lock (m_SyncRoot)
            {
                if (_mCapturer == null)
                    throw new ObjectDisposedException("DebugMonitor", "This DebugMonitor is not running.");
                _mCapturer = null;
                PulseEvent(_mReadyEvent);
                while (_mAckEvent != IntPtr.Zero)
                    ;
            }
        }

        private static ApplicationException CreateApplicationException(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException("text", "'text' may not be empty or null.");

            return new ApplicationException(string.Format("{0}. Last Win32 Error was {1}",
                                                          text, Marshal.GetLastWin32Error()));
        }

    }
}