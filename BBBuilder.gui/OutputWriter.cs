using System.IO;
using System.Text;
using System.Windows.Controls;

namespace BBBuilder_gui
{
    internal class OutputWriter : TextWriter
    {
        private readonly TextBox textbox;
        public OutputWriter(TextBox textbox)
        {
            this.textbox = textbox;
        }

        public override void Write(char value)
        {
            textbox.Dispatcher.BeginInvoke(() => textbox.AppendText(value.ToString()));
        }

        public override void Write(string value)
        {
            textbox.Dispatcher.BeginInvoke(() => textbox.AppendText(value));
        }

        public override Encoding Encoding => Encoding.UTF8;

        public void Clear()
        {
            textbox.Clear();
        }
    }
}
