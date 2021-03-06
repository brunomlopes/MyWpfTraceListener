using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Media;
using Caliburn.Micro;
using System.Linq;

namespace MyTraceListener {
    using System.ComponentModel.Composition;

    [Export(typeof (IShell))]
    public class ShellViewModel : PropertyChangedBase, IShell
    {
        private readonly WpfTraceListener _listener;
        private BindableCollection<LogMessage> _messages;
        public BindableCollection<LogMessage> Messages
        {
            get { return _messages; }
            set
            {
                _messages = value;
                NotifyOfPropertyChange(() => Messages);
            }
        }

        private BindableCollection<LogMessage> _selectedMessages;
        public BindableCollection<LogMessage> SelectedMessages
        {
            get { return _selectedMessages; }
            set
            {
                _selectedMessages = value;
                NotifyOfPropertyChange(() => SelectedMessages);
                NotifyOfPropertyChange(() => CanIgnore);
            }
        }

        private LogMessage _selectedMessage;

        public LogMessage SelectedMessage
        {
            get { return _selectedMessage; }
            set
            {
                _selectedMessage = value;
                NotifyOfPropertyChange(() => SelectedMessage);
                NotifyOfPropertyChange(() => CanIgnore);
            }
        }

        private int _ignoredMessages;
        public int IgnoredMessages
        {
            get { return _ignoredMessages; }
            set { _ignoredMessages = value;
                NotifyOfPropertyChange(() => IgnoredMessages);
            }
        } 
        
        public BindableCollection<string> IgnoredProcesses { get; set; }
        public BindableCollection<string> IgnoredText { get; set; }

        public ShellViewModel()
        {
            Messages = new BindableCollection<LogMessage>();
            SelectedMessages = new BindableCollection<LogMessage>();
            IgnoredProcesses = new BindableCollection<string>();
            IgnoredText = new BindableCollection<string>();
            _listener = new WpfTraceListener();
            _listener.CollectionChanged += (sender, e) =>
                                              {
                                                  if (e.Action != NotifyCollectionChangedAction.Add) return;

                                                  var logMessage = (LogMessage) e.NewItems[0];
                                                  if(FilterMessage(logMessage))
                                                  {
                                                      IgnoredMessages += 1;
                                                      return;
                                                  }
                                                  Messages.Insert(0, logMessage);
                                              };
        }

        public bool CanIgnore
        {
            get { return SelectedMessages.Count > 0 || SelectedMessage != null; }
        }

        public void Ignore()
        {
            foreach (var processName in SelectedMessages.Select(s => s.ProcessName).Distinct())
            {
                IgnoredProcesses.Add(processName);
            }
            IgnoredProcesses.Add(SelectedMessage.ProcessName);
            Messages = new BindableCollection<LogMessage>(Messages.Where(m => !FilterMessage(m)));
        }

        private bool FilterMessage(LogMessage logMessage)
        {
            return IgnoredProcesses.Contains(logMessage.ProcessName);
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}
