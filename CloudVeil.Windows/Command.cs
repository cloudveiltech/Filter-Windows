using CloudVeilGUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CloudVeil.Windows
{
    public class WPFCommand : ICommand
    {
        public WPFCommand(Command cmd)
        {
            toExecute = cmd;
        }

        private Command toExecute;

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            toExecute?.CommandAction();
        }
    }
}
