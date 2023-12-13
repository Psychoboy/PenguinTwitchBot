using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using System.Diagnostics.CodeAnalysis;

namespace DotNetTwitchBot.Pages
{
    public class Bookmark : ComponentBase, IDisposable
    {
        private bool _setFocus;

        [Inject] private NavigationManager NavManager { get; set; } = default!;
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter] public string? BookmarkName { get; set; }
        [DisallowNull] public ElementReference? Element { get; private set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(2, "tabindex", "-1");
            builder.AddContent(3, this.ChildContent);
            builder.AddElementReferenceCapture(4, this.SetReference);
            builder.CloseElement();
        }

        protected override void OnInitialized()
            => NavManager.LocationChanged += this.OnLocationChanged;

        protected override void OnParametersSet()
            => _setFocus = this.IsMe();

        private void SetReference(ElementReference reference)
            => this.Element = reference;

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            if (this.IsMe())
            {
                _setFocus = true;
                this.StateHasChanged();
            }
        }

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender == false) return;
            if (_setFocus)
                await this.Element!.Value.FocusAsync(false);

            _setFocus = false;
        }

        private bool IsMe()
        {
            string? elementId = null;

            var uri = new Uri(this.NavManager.Uri, UriKind.Absolute);
            if (uri.Fragment.StartsWith('#'))
            {
                elementId = uri.Fragment.Substring(1);
                return elementId == BookmarkName;
            }
            return false;
        }

        public void Dispose()
            => NavManager.LocationChanged -= this.OnLocationChanged;
    }
}
