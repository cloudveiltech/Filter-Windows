/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using GalaSoft.MvvmLight;

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