/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Newtonsoft.Json;
using System;

namespace Citadel.Core.Data.Serialization
{
    /// <summary>
    /// This class is passed to JSON.NET for the purpose of deserializing int values in a way that
    /// will cause the property being handled to not wind up with an invalid value, if the json int
    /// string is invalid.
    /// </summary>
    public class SafeIntConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(int);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // To account for errors. Default to zero when NaN, etc.
            int val = 0;

            if(!int.TryParse(reader.Value.ToString(), out val))
            {
                return null;
            }

            return val;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var cast = value as int?;
            if (cast == null)
            {
                writer.WriteValue((int?)null);
            }

            var castValue = cast.Value;

            writer.WriteValue(castValue);
        }
    }
}