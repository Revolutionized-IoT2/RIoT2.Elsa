using Elsa.Api.Client.Resources.ActivityDescriptors.Models;
using Elsa.Api.Client.Shared.UIHints.DropDown;
using RIoT2.Elsa.Studio.Models;
using System.Text.Json;

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
            };

            var itemProps = props.Deserialize<RIoTTemplateList>(serializerOptions);
            return itemProps ?? new RIoTTemplateList(new List<RIoTTemplateItem>());
        }
    }
}
