/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Newtonsoft.Json;
using System;

namespace Citadel.Core.Data.Serialization
{
    /// <summary>
    /// This class is passed to JSON.NET for the purpose of deserializing float values in a way that
    /// will cause the property being handled to not wind up with an invalid value, if the json float
    /// string is invalid.
    /// </summary>
    public class SafeFloatConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(float) || objectType == typeof(double);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // To account for errors. Default to zero when NaN, etc.
            float val = 0;

            if(!float.TryParse(reader.Value.ToString(), out val))
            {
                return null;
            }

            if(float.IsNaN(val))
            {
                return null;
            }

            return val;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}