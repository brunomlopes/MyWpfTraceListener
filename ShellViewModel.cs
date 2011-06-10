using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Caliburn.Micro;

namespace MyTraceListener {
    using System.ComponentModel.Composition;

    [Export(typeof(IShell))]
    public class ShellViewModel : PropertyChangedBase, IShell
    {
        public WpfTraceListener Listener { get; set; }
        public BindableCollection<string> Messages { get; set; }

        public ShellViewModel()
        {
            Listener = new WpfTraceListener();
            Messages = new BindableCollection<string>();
            Listener.CollectionChanged += (sender, e) =>
                                              {
                                                  if (e.Action == NotifyCollectionChangedAction.Add)
                                                  {
                                                      Messages.Add(e.NewItems[0].ToString());
                                                  }
                                              };
        }
    }
}
