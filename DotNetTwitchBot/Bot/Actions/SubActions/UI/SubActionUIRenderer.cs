using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace DotNetTwitchBot.Bot.Actions.SubActions.UI
{
    /// <summary>
    /// Generic UI renderer that automatically generates forms from SubAction UI field descriptors.
    /// </summary>
    /// <remarks>
    /// This class intentionally uses dynamic sequence numbers (sequence++) because it generates
    /// UI dynamically based on field descriptors. The ASP0006 warning is suppressed as this is
    /// a legitimate use case for variable sequences in a render fragment generator.
    /// </remarks>
#pragma warning disable ASP0006 // Do not use non-literal sequence numbers
    public static class SubActionUIRenderer
    {
        public static RenderFragment RenderFields(
            ISubActionUIProvider uiProvider,
            Dictionary<string, object?> values,
            object receiver,
            Action<string, object?> onValueChanged,
            IServiceProvider? serviceProvider = null)
        {
            return builder =>
            {
                var fields = uiProvider.GetUIFields(serviceProvider);
                int sequence = 0;

                foreach (var field in fields)
                {
                    RenderField(builder, ref sequence, field, values, receiver, onValueChanged);
                }
            };
        }

        private static void RenderField(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field,
            Dictionary<string, object?> values,
            object receiver,
            Action<string, object?> onValueChanged)
        {
            values.TryGetValue(field.PropertyName, out var currentValue);

            switch (field.FieldType)
            {
                case UIFieldType.Text:
                    RenderTextField(builder, ref sequence, field, currentValue as string ?? "", receiver, onValueChanged);
                    break;
                case UIFieldType.TextArea:
                    RenderTextArea(builder, ref sequence, field, currentValue as string ?? "", receiver, onValueChanged);
                    break;
                case UIFieldType.Number:
                    RenderNumberField(builder, ref sequence, field, currentValue as int?, receiver, onValueChanged);
                    break;
                case UIFieldType.Float:
                    RenderFloatField(builder, ref sequence, field, currentValue as float? ?? 0f, receiver, onValueChanged);
                    break;
                case UIFieldType.Switch:
                    RenderSwitch(builder, ref sequence, field, currentValue as bool? ?? false, receiver, onValueChanged);
                    break;
                case UIFieldType.Select:
                    RenderSelect(builder, ref sequence, field, currentValue as string ?? "", receiver, onValueChanged);
                    break;
                case UIFieldType.Info:
                    RenderInfo(builder, ref sequence, field);
                    break;
            }
        }

        private static void RenderTextField(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field,
            string value,
            object receiver,
            Action<string, object?> onValueChanged)
        {
            builder.OpenComponent<MudTextField<string>>(sequence++);
            builder.AddAttribute(sequence++, "Label", field.Label);
            builder.AddAttribute(sequence++, "Value", value);
            builder.AddAttribute(sequence++, "ValueChanged", EventCallback.Factory.Create<string>(receiver, v => onValueChanged(field.PropertyName, v)));
            builder.AddAttribute(sequence++, "Variant", Variant.Outlined);
            builder.AddAttribute(sequence++, "Required", field.Required);
            if (!string.IsNullOrEmpty(field.HelperText))
                builder.AddAttribute(sequence++, "HelperText", field.HelperText);

            builder.CloseComponent();
        }

        private static void RenderTextArea(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field,
            string value,
            object receiver,
            Action<string, object?> onValueChanged)
        {
            builder.OpenComponent<MudTextField<string>>(sequence++);
            builder.AddAttribute(sequence++, "Label", field.Label);
            builder.AddAttribute(sequence++, "Value", value);
            builder.AddAttribute(sequence++, "ValueChanged", EventCallback.Factory.Create<string>(receiver, v => onValueChanged(field.PropertyName, v)));
            builder.AddAttribute(sequence++, "Variant", Variant.Outlined);
            builder.AddAttribute(sequence++, "Lines", field.Lines ?? 3);
            builder.AddAttribute(sequence++, "Required", field.Required);
            if (!string.IsNullOrEmpty(field.HelperText))
                builder.AddAttribute(sequence++, "HelperText", field.HelperText);

            builder.CloseComponent();
        }

        private static void RenderNumberField(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field,
            int? value,
            object receiver,
            Action<string, object?> onValueChanged)
        {
            builder.OpenComponent<MudNumericField<int?>>(sequence++);
            builder.AddAttribute(sequence++, "Label", field.Label);
            builder.AddAttribute(sequence++, "Value", value);
            builder.AddAttribute(sequence++, "ValueChanged", EventCallback.Factory.Create<int?>(receiver, v => onValueChanged(field.PropertyName, v)));
            builder.AddAttribute(sequence++, "Variant", Variant.Outlined);
            if (field.Clearable)
                builder.AddAttribute(sequence++, "Clearable", true);

            if (field.Min != null)
                builder.AddAttribute(sequence++, "Min", field.Min);
            if (field.Max != null)
                builder.AddAttribute(sequence++, "Max", field.Max);
            if (!string.IsNullOrEmpty(field.HelperText))
                builder.AddAttribute(sequence++, "HelperText", field.HelperText);

            builder.CloseComponent();
        }

        private static void RenderFloatField(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field,
            float value,
            object receiver,
            Action<string, object?> onValueChanged)
        {
            builder.OpenComponent<MudNumericField<float>>(sequence++);
            builder.AddAttribute(sequence++, "Label", field.Label);
            builder.AddAttribute(sequence++, "Value", value);
            builder.AddAttribute(sequence++, "ValueChanged", EventCallback.Factory.Create<float>(receiver, v => onValueChanged(field.PropertyName, v)));
            builder.AddAttribute(sequence++, "Variant", Variant.Outlined);

            if (field.Min != null)
                builder.AddAttribute(sequence++, "Min", field.Min);
            if (field.Max != null)
                builder.AddAttribute(sequence++, "Max", field.Max);
            if (field.Step != null)
                builder.AddAttribute(sequence++, "Step", field.Step);
            if (!string.IsNullOrEmpty(field.HelperText))
                builder.AddAttribute(sequence++, "HelperText", field.HelperText);

            builder.CloseComponent();
        }

        private static void RenderSwitch(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field,
            bool value,
            object receiver,
            Action<string, object?> onValueChanged)
        {
            builder.OpenComponent<MudSwitch<bool>>(sequence++);
            builder.AddAttribute(sequence++, "Label", field.Label);
            builder.AddAttribute(sequence++, "Value", value);
            builder.AddAttribute(sequence++, "ValueChanged", EventCallback.Factory.Create<bool>(receiver, v => onValueChanged(field.PropertyName, v)));

            var color = !string.IsNullOrEmpty(field.SwitchColor)
                ? Enum.Parse<Color>(field.SwitchColor)
                : Color.Primary;
            builder.AddAttribute(sequence++, "Color", color);

            builder.CloseComponent();
        }

        private static void RenderSelect(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field,
            string value,
            object receiver,
            Action<string, object?> onValueChanged)
        {
            builder.OpenComponent<MudAutocomplete<string>>(sequence++);
            builder.AddAttribute(sequence++, "Label", field.Label);
            builder.AddAttribute(sequence++, "Value", value);
            builder.AddAttribute(sequence++, "ValueChanged", EventCallback.Factory.Create<string>(receiver, v => onValueChanged(field.PropertyName, v)));
            builder.AddAttribute(sequence++, "Variant", Variant.Outlined);
            builder.AddAttribute(sequence++, "MaxItems", (int?)null);
            builder.AddAttribute(sequence++, "Clearable", field.Clearable);
            builder.AddAttribute(sequence++, "CoerceText", true);
            builder.AddAttribute(sequence++, "CoerceValue", false);
            builder.AddAttribute(sequence++, "ResetValueOnEmptyText", false);

            if (!string.IsNullOrEmpty(field.HelperText))
                builder.AddAttribute(sequence++, "HelperText", field.HelperText);

            // Create search function
            if (field.SelectOptions != null && field.SelectOptions.Count > 0)
            {
                var options = field.SelectOptions;
                builder.AddAttribute(sequence++, "SearchFunc", new Func<string, CancellationToken, Task<IEnumerable<string>>>((searchValue, token) =>
                {
                    if (string.IsNullOrWhiteSpace(searchValue))
                    {
                        return Task.FromResult(options.Select(o => o.Value ?? o.Id.ToString()));
                    }

                    var filtered = options
                        .Where(o => o.Name.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                        .Select(o => o.Value ?? o.Id.ToString());
                    return Task.FromResult(filtered);
                }));

                // Custom display function to show names instead of IDs
                builder.AddAttribute(sequence++, "ToStringFunc", new Func<string, string>(val =>
                {
                    if (string.IsNullOrEmpty(val)) return string.Empty;
                    var option = options.FirstOrDefault(o => (o.Value ?? o.Id.ToString()) == val);
                    return option?.Name ?? val;
                }));
            }
            else if (field.Options != null && field.Options.Length > 0)
            {
                var options = field.Options;
                builder.AddAttribute(sequence++, "SearchFunc", new Func<string, CancellationToken, Task<IEnumerable<string>>>((searchValue, token) =>
                {
                    if (string.IsNullOrWhiteSpace(searchValue))
                    {
                        return Task.FromResult(options.AsEnumerable());
                    }

                    var filtered = options.Where(o => o.Contains(searchValue, StringComparison.OrdinalIgnoreCase));
                    return Task.FromResult(filtered);
                }));
            }

            builder.CloseComponent();
        }

        private static void RenderInfo(
            RenderTreeBuilder builder,
            ref int sequence,
            SubActionUIField field)
        {
            builder.OpenComponent<MudAlert>(sequence++);

            // Set the severity/color, default to Info
            var severity = !string.IsNullOrEmpty(field.Severity)
                ? Enum.Parse<Severity>(field.Severity, ignoreCase: true)
                : Severity.Info;
            builder.AddAttribute(sequence++, "Severity", severity);

            // Set variant if specified
            if (!string.IsNullOrEmpty(field.InfoVariant))
            {
                var variant = Enum.Parse<Variant>(field.InfoVariant, ignoreCase: true);
                builder.AddAttribute(sequence++, "Variant", variant);
            }

            // Set icon if specified
            if (!string.IsNullOrEmpty(field.Icon))
            {
                builder.AddAttribute(sequence++, "Icon", field.Icon);
            }

            // Set dense/compact if specified
            if (field.Dense)
            {
                builder.AddAttribute(sequence++, "Dense", true);
            }

            // Set NoIcon if specified
            if (field.NoIcon)
            {
                builder.AddAttribute(sequence++, "NoIcon", true);
            }

            // Content: Use Label as the message content
            builder.AddAttribute(sequence++, "ChildContent", (RenderFragment)(__builder =>
            {
                __builder.AddContent(0, field.Label);
            }));

            builder.CloseComponent();
        }
    }
}
