/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel.Data.Serialization
{
    class CookieListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<Cookie>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var ret = new List<Cookie>();

            foreach(var jo in JArray.Parse((string)reader.Value))
            {
                var httpOnly = jo["HttpOnly"].ToObject<bool>();
                var discard = jo["Discard"].ToObject<bool>();
                var domain = jo["Domain"].ToObject<string>();
                var expired = jo["Expired"].ToObject<bool>();
                var expires = jo["Expires"].ToObject<DateTime>();
                var name = jo["Name"].ToObject<string>();
                var path = jo["Path"].ToObject<string>();
                var secure = jo["Secure"].ToObject<bool>();
                var timestamp = jo["TimeStamp"].ToObject<DateTime>();
                var value = jo["Value"].ToObject<string>();

                var cookie = new Cookie(name, value, path, domain);
                cookie.Secure = secure;
                cookie.Expires = expires;
                cookie.Discard = discard;
                cookie.HttpOnly = httpOnly;

                ret.Add(cookie);
            }

            return ret;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(JsonConvert.SerializeObject(value));
        }
    }
}
