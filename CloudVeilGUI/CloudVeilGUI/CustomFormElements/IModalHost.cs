// Copied from https://github.com/gaborv/xam-forms-transparent-modal/blob/master/CustomFormElements/IModalHost.cs

using Xamarin.Forms;
using System.Threading.Tasks;

namespace CloudVeilGUI.CustomFormElements
{
    public interface IModalHost
    {
        Task DisplayPageModal(Page page);

        Task DisplayAlert(string title, string message, string cancel);
        Task<bool> DisplayAlert(string title, string message, string accept, string cancel);
        Task<string> DisplayActionSheet(string title, string cancel, string destruction, params string[] buttons);
    }
}
