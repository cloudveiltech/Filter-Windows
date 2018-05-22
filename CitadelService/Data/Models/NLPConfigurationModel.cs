/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Collections.Generic;

namespace CitadelService.Data.Models
{
    /// <summary>
    /// The NLPConfigurationModel class represents an Apache OpenNLP model file along with a list of
    /// all categories within the listed NLP model file that are to be used for content
    /// classification.
    ///
    /// That is to say that this model class gives a relative path an Apache OpenNLP model file
    /// within a zip container, and then also lists categories within the model that were selected
    /// for use. All other categories inside the model file are ignored.
    ///
    /// This configuration is set externally (originally by the server side Admin), and is meant to
    /// be consumed in a read-only fashion by the client. This client (this software) is to load the
    /// provided Apache OpenNLP model file and then use it to attempt to classify text content. If
    /// the best category returned by the classification matches a category selected for use, then a
    /// block action should take place because this counts as positive match.
    /// </summary>
    public class NLPConfigurationModel
    {
        /// <summary>
        /// The relative path to the Apache OpenNLP model file inside the parent zip container.
        /// </summary>
        public string RelativeModelPath
        {
            get;
            set;
        }

        /// <summary>
        /// A list of categories selected from within the Apache OpenNLP model that, should they be
        /// returned from a text classification result, be considered a positive match and trigger a
        /// block action.
        /// </summary>
        public List<string> SelectedCategoryNames
        {
            get;
            set;
        }
    }
}