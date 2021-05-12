
﻿/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace CloudVeil.IPC.Messages
{
    /// <summary>
    /// Enum of the different block action types that a notify block action message can represent. 
    /// </summary>
    [Serializable]
    public enum BlockType
    {
        /// <summary>
        /// Means that a URL filtering rule has caused the block action.
        /// </summary>
        Url,

        /// <summary>
        /// Means that a text trigger rule has caused the block action.
        /// </summary>
        TextTrigger,

        /// <summary>
        /// Means that document classification has caused the block action.
        /// </summary>
        TextClassification,

        /// <summary>
        /// Means that image classification has caused the block action.
        /// </summary>
        ImageClassification,

        /// <summary>
        /// Means that some other form of content classification has caused the block action.
        /// </summary>
        OtherContentClassification,

        /// <summary>
        /// Placeholder for default conditions (such as first initialization of variables)
        /// </summary>
        None,
        TimeRestriction
    }

    /// <summary>
    /// The NotifyBlockActionMessage class represents an IPC communication between client (GUI) and
    /// server (Service) with regards to actions taken by the server to block a network connection.
    /// The server alone may construct and send these messages. They are purely informational for the
    /// sake of updating the client.
    /// </summary>
    /// <remarks>
    /// Note the ServerOnlyMessage base class. Cannot be constructed by the client, or the default
    /// constructor chain will throw.
    /// </remarks>
    [Serializable]
    public class NotifyBlockActionMessage : ServerOnlyMessage
    {

        /// <summary>
        /// The block type. Indicates what caused the block to take place.
        /// </summary>
        public BlockType Type
        {
            get;
            private set;
        }

        public DateTime BlockDate
        {
            get;
            private set;
        }

        /// <summary>
        /// The absolute URI of the requested resource that caused the network connection to be blocked.
        /// </summary>
        public Uri Resource
        {
            get;
            private set;
        }

        /// <summary>
        /// A string representation of the category that the rule causing the block action belongs to. 
        /// </summary>
        public string Category
        {
            get;
            private set;
        }

        /// <summary>
        /// A string representation of the rule that caused the block action. May not always apply. 
        /// </summary>
        public string Rule
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new NotifyBlockActionMessage instance. 
        /// </summary>
        /// <param name="type">
        /// The block action type. Aka the reason for the block.
        /// </param>
        /// <param name="resource">
        /// An absolute URI to the requested resource for the network connection that was blocked.
        /// </param>
        /// <param name="rule">
        /// A string representation of the filtering rule that caused the block action. May not always apply.
        /// </param>
        /// <param name="category">
        /// The cateogry 
        /// </param>
        public NotifyBlockActionMessage(BlockType type, Uri resource, string rule, string category, DateTime blockDate)
        {
            Type = type;
            Resource = resource;
            Category = category;
            BlockDate = blockDate;

            switch(type)
            {
                case BlockType.ImageClassification:
                {
                    Rule = "Image Classification";
                }
                break;

                case BlockType.OtherContentClassification:
                {
                    Rule = "Other Content Classifier";
                }
                break;

                default:
                {
                    Rule = rule != null ? rule : string.Empty;
                }
                break;
            }
        }
    }
}