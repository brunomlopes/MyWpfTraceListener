using System;
using System.Diagnostics;

namespace MyTraceListener
{
    public class LogMessage
    {
        public DateTimeOffset Timestamp { get; set; }
        public string ProcessName { get; set; }
        public string Message { get; set; }
        public int Pid { get; set; }

        public LogMessage(int pid, string message)
        {
            Message = message;
            Pid = pid;
            Process processById = Process.GetProcessById(pid);
            ProcessName = processById.ProcessName;
            Timestamp = DateTimeOffset.Now;
        }
    }
}