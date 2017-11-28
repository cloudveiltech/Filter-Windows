using Citadel.Core.Windows.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Te.Citadel.UI.ViewModels;

namespace Te.Citadel.UI.Views
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml
    /// </summary>
    public partial class HistoryView : Window
    {
        public HistoryView()
        {
            InitializeComponent();
        }

        public void AppendBlockActionEvent(string category, string fullRequest)
        {
            try
            {
                var dataCtx = (HistoryViewModel)this.DataContext;
                // Keep number of items truncated to 50.
                if (dataCtx.BlockEvents.Count > 50)
                {
                    dataCtx.BlockEvents.RemoveAt(0);
                }

                // Add the item to view.
                dataCtx.BlockEvents.Add(new ViewableBlockedRequests(category, fullRequest));
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }
    }
}
