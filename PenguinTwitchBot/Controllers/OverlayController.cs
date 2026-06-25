using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Database.Bot.Models.Overlay;
using PenguinTwitchBot.Database.Repository;
using System.Text.Json;

namespace PenguinTwitchBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Streamer, Editor")]
    public class OverlayController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OverlayController> _logger;

        public OverlayController(IUnitOfWork unitOfWork, ILogger<OverlayController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>
        /// Returns the overlay layout config for the compositor page.
        /// Called by overlay.html on load.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("config")]
        public async Task<IActionResult> GetConfig([FromQuery] string? layout = null)
        {
            OverlayLayout? overlayLayout = null;

            if (!string.IsNullOrWhiteSpace(layout))
                overlayLayout = await _unitOfWork.OverlayLayouts.GetByNameAsync(layout);

            overlayLayout ??= await _unitOfWork.OverlayLayouts.GetDefaultAsync();

            if (overlayLayout == null)
                return Ok(new OverlayConfigResponse("Default", 1920, 1080, []));

            var widgets = overlayLayout.Widgets
                .Select(w =>
                {
                    var def = WidgetRegistry.Find(w.WidgetType);
                    var basePath = def?.SourcePath ?? $"/{w.WidgetType}.html";
                    var sourcePath = BuildSourcePath(basePath, w.CustomSettings);
                    return new OverlayWidgetResponse(
                        w.WidgetType,
                        sourcePath,
                        w.IsEnabled,
                        w.X,
                        w.Y,
                        w.Width,
                        w.Height,
                        w.ZIndex
                    );
                })
                .ToList();

            return Ok(new OverlayConfigResponse(overlayLayout.Name, overlayLayout.CanvasWidth, overlayLayout.CanvasHeight, widgets));
        }

        /// <summary>Returns all layouts (names + ids) for the editor UI.</summary>
        [HttpGet("layouts")]
        public async Task<IActionResult> GetLayouts()
        {
            var layouts = await _unitOfWork.OverlayLayouts.GetAllWithWidgetsAsync();
            return Ok(layouts.Select(l => new OverlayLayoutSummary(l.Id, l.Name, l.IsDefault)));
        }

        /// <summary>Returns a single layout with all widgets for the editor.</summary>
        [HttpGet("layouts/{id:int}")]
        public async Task<IActionResult> GetLayout(int id)
        {
            var layout = await _unitOfWork.OverlayLayouts.GetByIdWithWidgetsAsync(id);
            if (layout == null) return NotFound();
            return Ok(layout);
        }

        /// <summary>Creates a new layout.</summary>
        [HttpPost("layouts")]
        public async Task<IActionResult> CreateLayout([FromBody] CreateLayoutRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name is required.");

            // If this is the first layout, make it the default
            var existing = await _unitOfWork.OverlayLayouts.GetAllWithWidgetsAsync();
            var isFirst = existing.Count == 0;

            var layout = new OverlayLayout
            {
                Name = request.Name,
                IsDefault = isFirst || request.IsDefault
            };

            // If setting as default, clear other defaults
            if (layout.IsDefault)
            {
                foreach (var l in existing.Where(l => l.IsDefault))
                {
                    l.IsDefault = false;
                    _unitOfWork.OverlayLayouts.Update(l);
                }
            }

            await _unitOfWork.OverlayLayouts.AddAsync(layout);
            await _unitOfWork.SaveChangesAsync();
            return Ok(layout);
        }

        /// <summary>Saves the full widget list for a layout (replaces all widgets).</summary>
        [HttpPut("layouts/{id:int}/widgets")]
        public async Task<IActionResult> SaveWidgets(int id, [FromBody] List<SaveWidgetRequest> widgets)
        {
            var layout = await _unitOfWork.OverlayLayouts.GetByIdWithWidgetsAsync(id);
            if (layout == null) return NotFound();

            // Remove existing widgets for this layout and re-add
            var existing = await _unitOfWork.OverlayWidgets.GetByLayoutIdAsync(id);
            foreach (var w in existing)
                _unitOfWork.OverlayWidgets.Remove(w);

            foreach (var w in widgets)
            {
                _unitOfWork.OverlayWidgets.Add(new OverlayWidget
                {
                    OverlayLayoutId = id,
                    WidgetType = w.WidgetType,
                    IsEnabled = w.IsEnabled,
                    X = w.X,
                    Y = w.Y,
                    Width = w.Width,
                    Height = w.Height,
                    ZIndex = w.ZIndex,
                    CustomSettings = w.CustomSettings
                });
            }

            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        /// <summary>Sets a layout as the default.</summary>
        [HttpPost("layouts/{id:int}/set-default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var all = await _unitOfWork.OverlayLayouts.GetAllWithWidgetsAsync();
            foreach (var l in all)
            {
                l.IsDefault = l.Id == id;
                _unitOfWork.OverlayLayouts.Update(l);
            }
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        /// <summary>Deletes a layout and all its widgets (cascade).</summary>
        [HttpDelete("layouts/{id:int}")]
        public async Task<IActionResult> DeleteLayout(int id)
        {
            var layout = await _unitOfWork.OverlayLayouts.GetByIdWithWidgetsAsync(id);
            if (layout == null) return NotFound();
            _unitOfWork.OverlayLayouts.Remove(layout);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        /// <summary>Returns all widget types known to the registry.</summary>
        [HttpGet("widget-types")]
        public IActionResult GetWidgetTypes()
        {
            return Ok(WidgetRegistry.All.Select(w => new
            {
                w.Type,
                w.DisplayName,
                w.SourcePath,
                w.DefaultWidth,
                w.DefaultHeight
            }));
        }

        /// <summary>
        /// Appends CustomSettings JSON fields as URL query parameters onto basePath.
        /// Null/empty/whitespace values are skipped.
        /// </summary>
        private static string BuildSourcePath(string basePath, string? customSettings)
        {
            if (string.IsNullOrWhiteSpace(customSettings)) return basePath;
            try
            {
                using var doc = JsonDocument.Parse(customSettings);
                var parts = doc.RootElement.EnumerateObject()
                    .Where(p => p.Value.ValueKind != JsonValueKind.Null
                             && p.Value.ValueKind != JsonValueKind.Undefined)
                    .Select(p =>
                    {
                        var val = p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString() ?? ""
                            : p.Value.ToString();
                        return (key: p.Name, val);
                    })
                    .Where(t => !string.IsNullOrWhiteSpace(t.val))
                    .Select(t => $"{Uri.EscapeDataString(t.key)}={Uri.EscapeDataString(t.val)}")
                    .ToList();

                return parts.Count > 0 ? basePath + "?" + string.Join("&", parts) : basePath;
            }
            catch
            {
                return basePath;
            }
        }
    }

    public record OverlayConfigResponse(string Name, int CanvasWidth, int CanvasHeight, List<OverlayWidgetResponse> Widgets);
    public record OverlayWidgetResponse(string WidgetType, string SourcePath, bool IsEnabled, int X, int Y, int Width, int Height, int ZIndex);
    public record OverlayLayoutSummary(int Id, string Name, bool IsDefault);
    public record CreateLayoutRequest(string Name, bool IsDefault);
    public record SaveWidgetRequest(string WidgetType, bool IsEnabled, int X, int Y, int Width, int Height, int ZIndex, string? CustomSettings);
}
