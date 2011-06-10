using System;
using System.Globalization;
using System.Windows.Data;

namespace MyTraceListener
{
    [ValueConversion(typeof(LogMessage), typeof(string))]
    public class LogMessageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            LogMessage state = (LogMessage)value;
            return string.Format("[{0}] -{1}- {2}", state.Timestamp, state.ProcessName, state.Message);
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}