using Elsa.Api.Client.Resources.ActivityDescriptors.Models;
using Elsa.Api.Client.Shared.UIHints.DropDown;
using Microsoft.Extensions.Logging;
using RIoT2.Elsa.Studio.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RIoT2.Elsa.Studio.UIProviders
{

    public static class GetRIoTTemplateItemExtensions
    {
        /// <summary>
        /// Gets the <see cref="SelectList"/> for the specified <see cref="InputDescriptor"/>.
        /// </summary>
        public static RIoTTemplateList GetRIoTTemplateList(this InputDescriptor descriptor)
        {
            var specifications = descriptor.UISpecifications;
            var props = specifications != null ? specifications.TryGetValue("riot-output-selector", out var propsValue) ? propsValue is JsonElement value ? value : default : default : default;

            if (props.ValueKind == JsonValueKind.Undefined)
                return new([]);
        
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };

            try 
            {
                var itemProps = props.Deserialize<RIoTOutputProps>(serializerOptions);
                return itemProps?.SelectList ?? new([]);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error deserializing props: " + ex.Message);
                return new([]);
            }
        }
    }
}
