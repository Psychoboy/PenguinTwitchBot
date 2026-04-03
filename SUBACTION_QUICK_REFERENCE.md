# SubAction Quick Reference Card

## 🚀 Creating a New SubAction (2 Steps!)

### 1️⃣ Create the Type Class
```csharp
[SubActionMetadata(
    displayName: "Your Action Name",
    description: "What it does",
    icon: "mdi-icon-name",           // See icon list below
    color: "Primary",                 // Primary, Secondary, Success, Error, Info, Warning
    tableName: "SubActions_YourName"  // Convention: SubActions_{Name}
)]
public class YourActionType : SubActionType
{
    public YourActionType()
    {
        SubActionTypes = SubActionTypes.YourAction;  // Must match enum
    }
    
    // Add your custom properties
    public string MyProperty { get; set; } = "default";
}
```

### 2️⃣ Create the Handler
```csharp
public class YourActionHandler : ISubActionHandler
{
    public SubActionTypes SupportedType => SubActionTypes.YourAction;
    
    public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
    {
        if (subAction is not YourActionType myAction)
            return Task.CompletedTask;
            
        // Variable replacement (if needed)
        myAction.Text = VariableReplacer.ReplaceVariables(myAction.Text, variables);
        
        // Your logic here
        
        return Task.CompletedTask;
    }
}
```

### ✅ That's It! Everything Else is Automatic

---

## 🎨 Common Icon Names
| Icon | MDI Name | Use For |
|------|----------|---------|
| 💬 | `mdi-message-text` | Messages, chat |
| 🔔 | `mdi-bell` | Alerts, notifications |
| 🔊 | `mdi-volume-high` | Audio, sounds |
| 💾 | `mdi-content-save` | File operations |
| ⏰ | `mdi-clock` | Time-based actions |
| ❤️ | `mdi-heart` | Follow, favorites |
| ⏱️ | `mdi-timer` | Timers, countdowns |
| 🔗 | `mdi-api` | API calls |
| 👁️ | `mdi-eye` | Visibility, watch |
| ↩️ | `mdi-reply` | Reply, respond |
| 🎁 | `mdi-gift` | Rewards, prizes |
| 🎲 | `mdi-dice-multiple` | Random, games |
| ⭐ | `mdi-star` | Featured items |

**Full list**: https://pictogrammers.com/library/mdi/

---

## 🎨 Color Themes
| Color | When to Use |
|-------|-------------|
| `Primary` | Default actions, messages |
| `Secondary` | Utility functions, helpers |
| `Success` | Positive outcomes, saves |
| `Error` | Alerts, warnings, critical |
| `Info` | Informational, status |
| `Warning` | Caution, important |

---

## 📝 Common Property Patterns

### Base Properties (Inherited from SubActionType)
```csharp
public string Text { get; set; }     // Main text/content
public string File { get; set; }     // File path
public bool Enabled { get; set; }    // Enable/disable
public int Index { get; set; }       // Execution order
```

### Chat-Related (Inherit from ChatType)
```csharp
public bool UseBot { get; set; }      // Use bot account
public bool FallBack { get; set; }    // Fallback to broadcaster
public bool StreamOnly { get; set; }  // Only when live
```

### Custom Properties Examples
```csharp
public int Duration { get; set; }     // Time in seconds
public float Volume { get; set; }     // 0.0 to 1.0
public bool Append { get; set; }      // Append vs overwrite
public string HttpMethod { get; set; } // GET, POST, etc.
public int Min { get; set; }          // Minimum value
public int Max { get; set; }          // Maximum value
```

---

## 🔧 Variable Replacement
Use in your handler when you need to replace variables:
```csharp
myAction.Text = VariableReplacer.ReplaceVariables(myAction.Text, variables);
```

Common variables:
- `%user%` - Username who triggered
- `%target%` - Target user (if any)
- `%channel%` - Channel name
- `%random.1-100%` - Random number
- `%prize%` - Giveaway prize
- Custom variables from your action

---

## 🧪 Unit Testing Template
```csharp
[Fact]
public async Task YourAction_DoesExpectedBehavior()
{
    // Arrange
    var dependency = Substitute.For<IDependency>();
    var logger = Substitute.For<ILogger<YourActionHandler>>();
    var handler = new YourActionHandler(dependency, logger);
    
    var action = new YourActionType
    {
        MyProperty = "test value"
    };
    
    var variables = new Dictionary<string, string> { { "user", "TestUser" } };
    
    // Act
    await handler.ExecuteAsync(action, variables);
    
    // Assert
    dependency.Received(1).DoSomething();
}
```

---

## 🚫 What NOT to Do

❌ **Don't manually register** handlers in `BotCommandsRegistry`  
✅ They're auto-discovered

❌ **Don't manually configure** EF Core in `ApplicationDbContext`  
✅ Use the `[SubActionMetadata]` attribute

❌ **Don't forget** to match enum value in constructor  
✅ Always set `SubActionTypes = SubActionTypes.YourAction`

❌ **Don't use generic names** for table names  
✅ Use convention: `SubActions_{YourActionName}`

---

## 🔍 Registry API Usage

```csharp
// Get metadata for a SubAction type
var metadata = SubActionRegistry.GetMetadata(SubActionTypes.SendMessage);
string displayName = metadata?.DisplayName;
string icon = metadata?.Icon;

// Get all registered SubActions
var allTypes = SubActionRegistry.Metadata.Keys;

// Get the Type class for a SubAction
Type? typeClass = SubActionRegistry.GetSubActionType(SubActionTypes.Alert);

// Create instance from registry
var instance = Activator.CreateInstance(typeClass) as SubActionType;
```

---

## 📚 Documentation Files

- **`SUBACTION_AUTO_REGISTRATION.md`** - Complete guide
- **`SUBACTION_AUTO_REGISTRATION_SUMMARY.md`** - Implementation summary

---

## 💡 Tips

1. **Use descriptive display names** - Users see these in the UI
2. **Write clear descriptions** - Help users understand what it does
3. **Choose appropriate icons** - Visual cues improve UX
4. **Follow naming conventions** - Table name: `SubActions_{Type}`
5. **Keep handlers simple** - One responsibility per SubAction
6. **Test thoroughly** - Write unit tests for each handler
7. **Log appropriately** - Use logger for debugging
8. **Handle type checks** - Always verify cast succeeded

---

## 🆘 Troubleshooting

### SubAction not appearing in UI?
✅ Verify `[SubActionMetadata]` attribute is present  
✅ Check constructor sets `SubActionTypes` correctly

### Handler not executing?
✅ Implements `ISubActionHandler`?  
✅ `SupportedType` matches enum value?  
✅ Has public constructor with DI parameters?

### Database migration issues?
✅ Table name matches metadata?  
✅ No naming conflicts?  
✅ Run `Add-Migration` after creating type?

---

**Remember**: With auto-registration, you only need to create 2 files (Type + Handler), everything else is automatic! 🎉
