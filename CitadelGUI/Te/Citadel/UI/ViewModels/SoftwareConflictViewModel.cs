using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel.UI.ViewModels
{
    public class SoftwareConflictViewModel : BaseCitadelViewModel
    {
        private string conflictText;
        public string ConflictText
        {
            get => conflictText;
            set
            {
                conflictText = value;
                RaisePropertyChanged(nameof(ConflictText));
            }
        }

        private Uri conflictSupportArticle;
        public Uri ConflictSupportArticle
        {
            get => conflictSupportArticle;
            set
            {
                conflictSupportArticle = value;
                RaisePropertyChanged(nameof(ConflictSupportArticle));
            }
        }
    }
}
