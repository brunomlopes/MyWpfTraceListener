using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MyTraceListener
{
    public class WpfTraceListener : TraceListener,INotifyCollectionChanged
    {
        public Queue<string> Messages { get; private set; }

        public WpfTraceListener()
        {
            Initialize();
        }

        public WpfTraceListener(string name) : base(name)
        {
            Initialize();
        }

        private void Initialize()
        {
            Messages = new Queue<string>();
            CollectionChanged += (sender, e) => {};
            int i = 0;
            Action<Task> continueWith = null;
            continueWith = (task) =>
                               {
                                   Thread.Sleep(TimeSpan.FromSeconds(1));
                                   WriteLine("Hey, ho! " + i++);
                                   task.ContinueWith(continueWith);
                               };
            Task.Factory.StartNew(() => Thread.Sleep(TimeSpan.FromSeconds(1)))
                .ContinueWith(continueWith);
        }

        public override void Write(string message)
        {
            //Messages.Enqueue(message);
            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, message));
        }

        public override void WriteLine(string message)
        {
            //Messages.Enqueue(message);
            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, message));
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
    }
}