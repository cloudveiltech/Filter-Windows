using Newtonsoft.Json;
using System;

namespace Te.Citadel.Data.Serialization
{
    /// <summary>
    /// This class is passed to JSON.NET for the purpose of deserializing integer values into
    /// timespans that are from minutes.
    /// </summary>
    internal class IntToMinutesTimespanConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TimeSpan);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var minutes = int.Parse((string)reader.Value);
            return TimeSpan.FromMinutes(minutes);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}