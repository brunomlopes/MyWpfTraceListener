using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MyTraceListener
{
    public class WpfTraceListener : INotifyCollectionChanged
    {
        public WpfTraceListener()
        {
            Initialize();
        }

        private void Initialize()
        {
            CollectionChanged += (sender, e) => { };

            DebugMonitor.OnOutputDebugString += OnDebugMonitorOnOnOutputDebugString;
            DebugMonitor.Start();
        }

        private void OnDebugMonitorOnOnOutputDebugString(int pid, string text)
        {
            var message = new LogMessage(pid, text);
            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, message));
        }

        public void Start()
        {
            DebugMonitor.Start();
        }

        public void Stop()
        {
            DebugMonitor.Stop();
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
    }
}