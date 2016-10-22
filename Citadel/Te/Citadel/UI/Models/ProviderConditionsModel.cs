using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Models
{
    internal class ProviderConditionsModel : ObservableObject
    {
        private string m_terms = string.Empty;

        private object m_termsLock = new object();

        public string Terms
        {
            get
            {
                lock(m_termsLock)
                {
                    return m_terms;
                }
            }

            set
            {
                lock(m_termsLock)
                {
                    m_terms = value;

                    if(m_terms == null)
                    {
                        m_terms = string.Empty;
                    }
                }
            }
        }
    }
}
