# SubAction Auto-Registration System

## Overview
This system eliminates the need to manually register SubActions in multiple places when creating a new SubAction type. The system uses **reflection-based discovery** and **attribute-based metadata** to automatically:

1. ✅ Register handlers in DI (BotCommandsRegistry)
2. ✅ Configure EF Core TPC mapping (ApplicationDbContext)
3. ✅ Provide UI metadata (AddSubActionDialog)
4. ✅ Discover all SubAction types at runtime

## How It Works

### 1. Attribute-Based Metadata
Each SubAction type is decorated with a `[SubActionMetadata]` attribute that provides:
- Display name for UI
- Description for UI
- Icon (Material Design Icon)
- Color theme
- Database table name

### 2. Automatic Discovery
The `SubActionRegistry` class uses reflection to discover all SubAction types at startup and builds a registry of metadata, types, and handlers.

### 3. Extension Methods
Two extension method classes provide automatic registration:
- `SubActionServiceCollectionExtensions.AddSubActionHandlers()` - Registers all handlers in DI
- `SubActionModelBuilderExtensions.ConfigureSubActions()` - Configures all EF Core mappings

## Creating a New SubAction

To create a new SubAction, you now only need to:

### Step 1: Add the enum value
Add your new SubAction to the `SubActionTypes` enum:

```csharp
public enum SubActionTypes
{
    None,
    // ... existing types ...
    MyNewAction
}
```

### Step 2: Create the Type class with metadata
Create your SubAction type class with the `[SubActionMetadata]` attribute:

```csharp
namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "My New Action",
        description: "Does something amazing",
        icon: "mdi-star",
        color: "Primary",
        tableName: "SubActions_MyNewAction")]
    public class MyNewActionType : SubActionType
    {
        public MyNewActionType()
        {
            SubActionTypes = SubActionTypes.MyNewAction;
        }

        // Add your custom properties here
        public string CustomProperty { get; set; } = "default";
    }
}
```

### Step 3: Create the Handler
Create the handler implementing `ISubActionHandler`:

```csharp
namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class MyNewActionHandler : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.MyNewAction;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not MyNewActionType myAction)
            {
                // Handle type mismatch
                return Task.CompletedTask;
            }

            // Your implementation here
            return Task.CompletedTask;
        }
    }
}
```

### Step 4: (Optional) Add UI Configuration
If you need custom UI configuration in `AddSubActionDialog.razor`, add a case in the `RenderConfiguration()` method. Otherwise, the basic configuration will be used.

## That's It!

**You no longer need to:**
- ❌ Manually register the handler in `BotCommandsRegistry.cs`
- ❌ Manually add EF Core table mapping in `ApplicationDbContext.cs`
- ❌ Manually update metadata methods in the UI

The system will automatically:
- ✅ Register your handler in DI
- ✅ Configure the database table mapping
- ✅ Make it available in the UI with proper display name, icon, and description

## Available Metadata Properties

### Display Name
The user-friendly name shown in the UI dropdown.

### Description
A brief description shown to help users understand what the SubAction does.

### Icon
Material Design Icon identifier. Common options:
- `mdi-message-text` - Messages
- `mdi-bell` - Alerts
- `mdi-volume-high` - Sounds
- `mdi-content-save` - File operations
- `mdi-clock` - Time-based
- `mdi-heart` - User-related
- `mdi-api` - API calls
- `mdi-gift` - Rewards/Prizes

Full icon list: https://pictogrammers.com/library/mdi/

### Color
MudBlazor color theme:
- `Primary` - Blue (default actions)
- `Secondary` - Grey (utilities)
- `Success` - Green (positive actions)
- `Error` - Red (alerts, warnings)
- `Info` - Cyan (informational)
- `Warning` - Orange (caution)
- `Default` - No specific theme

### Table Name
The database table name for EF Core TPC mapping. Convention: `SubActions_{TypeName}`

## Migration Guide

If you have an existing SubAction system without auto-registration:

1. Add the `[SubActionMetadata]` attribute to all existing SubAction types
2. Replace manual handler registration in `BotCommandsRegistry` with `services.AddSubActionHandlers()`
3. Replace manual EF Core configuration in `ApplicationDbContext` with `modelBuilder.ConfigureSubActions()`
4. (Optional) Remove manual metadata methods from UI components

## Architecture

```
SubActionMetadataAttribute
  └─> Applied to SubAction Type classes
  
SubActionRegistry (Singleton)
  ├─> Discovers all SubAction types via reflection
  ├─> Builds metadata dictionary
  └─> Provides lookups for UI and runtime
  
SubActionServiceCollectionExtensions
  └─> Automatically registers handlers in DI
  
SubActionModelBuilderExtensions
  └─> Automatically configures EF Core TPC mapping
```

## Benefits

1. **Reduced Boilerplate**: Create new SubActions with ~60% less code
2. **Prevents Errors**: No more forgetting to register in one place
3. **Centralized Metadata**: All SubAction info in one place (the attribute)
4. **Type Safety**: Compile-time errors if metadata is missing
5. **Discoverability**: Easy to see all available SubActions
6. **Maintainability**: Changes to a SubAction stay local to that SubAction

## Performance

- **Discovery**: One-time reflection scan at startup (lazy initialization)
- **Runtime**: Zero overhead - uses compiled dictionaries
- **Memory**: Negligible - only metadata and type references cached

## Testing

The auto-registration system doesn't affect unit testing. Mock your dependencies as usual:

```csharp
var handler = new MyNewActionHandler(/* dependencies */);
await handler.ExecuteAsync(subAction, variables);
```

## Troubleshooting

### SubAction not appearing in UI
- Verify the `[SubActionMetadata]` attribute is present
- Check that the class inherits from `SubActionType`
- Ensure the constructor sets `SubActionTypes` correctly

### Handler not executing
- Verify handler implements `ISubActionHandler`
- Check `SupportedType` property matches the enum value
- Ensure handler has a public parameterless or DI constructor

### Database migration issues
- Run `Add-Migration` after adding new SubAction types
- Verify table name in metadata matches EF Core conventions
- Check for naming conflicts with existing tables

## Future Enhancements

Potential improvements to this system:
- Generate UI configuration automatically from property attributes
- Validate SubAction configurations at startup
- Auto-generate API documentation from metadata
- Support for SubAction categories/grouping
