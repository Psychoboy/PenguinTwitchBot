# 🎨 Visual Guide: Your New SubAction Workflow

## 📋 Step-by-Step: Creating a "Wait/Delay" SubAction

### Step 1️⃣: Add Enum Value (2 seconds)
```csharp
// File: SubActionTypes.cs
public enum SubActionTypes
{
    None,
    Alert,
    SendMessage,
    // ... existing ...
    Delay  // ← ADD THIS
}
```

### Step 2️⃣: Create SubAction Type (30 seconds)

**Option A: Simple (Inherit base class)**
```csharp
// File: DelayType.cs
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

[SubActionMetadata(
    displayName: "Delay",
    description: "Wait for a specified number of seconds",
    icon: "mdi-clock-outline",
    color: "Info")]
public class DelayType : SimpleSubActionType
{
    public DelayType() 
    { 
        SubActionTypes = SubActionTypes.Delay; 
    }

    public int Seconds { get; set; } = 5;

    protected override string TextLabel => "Delay Duration (seconds)";
    protected override bool TextRequired => false;

    // Add custom field
    protected override void AddCustomFields(List<SubActionUIField> fields)
    {
        fields.Insert(0, new()
        {
            PropertyName = nameof(Seconds),
            Label = "Seconds",
            FieldType = UIFieldType.Number,
            Attributes = new Dictionary<string, object> 
            { 
                { "Min", 1 }, 
                { "Max", 300 } 
            }
        });
    }

    protected override void AddCustomValues(Dictionary<string, object?> values)
    {
        values[nameof(Seconds)] = Seconds;
    }

    protected override void SetCustomValues(Dictionary<string, object?> values)
    {
        if (values.TryGetValue(nameof(Seconds), out var seconds))
            Seconds = seconds as int? ?? 5;
    }

    protected override string? ValidateCustom()
    {
        if (Seconds < 1 || Seconds > 300)
            return "Delay must be between 1 and 300 seconds";
        return null;
    }
}
```

**Option B: Full Control (Implement interface)**
```csharp
// File: DelayType.cs
using DotNetTwitchBot.Bot.Actions.SubActions.UI;

[SubActionMetadata(...)]
public class DelayType : SubActionType, ISubActionUIProvider
{
    public DelayType() { SubActionTypes = SubActionTypes.Delay; }
    
    public int Seconds { get; set; } = 5;

    public List<SubActionUIField> GetUIFields()
    {
        return new List<SubActionUIField>
        {
            new() 
            { 
                PropertyName = nameof(Seconds), 
                Label = "Seconds", 
                FieldType = UIFieldType.Number,
                Attributes = new Dictionary<string, object> { { "Min", 1 }, { "Max", 300 } }
            },
            new() 
            { 
                PropertyName = nameof(Enabled), 
                Label = "Enabled", 
                FieldType = UIFieldType.Switch 
            }
        };
    }

    public Dictionary<string, object?> GetValues()
    {
        return new() { { nameof(Seconds), Seconds }, { nameof(Enabled), Enabled } };
    }

    public void SetValues(Dictionary<string, object?> values)
    {
        if (values.TryGetValue(nameof(Seconds), out var s))
            Seconds = s as int? ?? 5;
        if (values.TryGetValue(nameof(Enabled), out var e))
            Enabled = e as bool? ?? true;
    }

    public string? Validate()
    {
        if (Seconds < 1 || Seconds > 300)
            return "Delay must be between 1 and 300 seconds";
        return null;
    }
}
```

### Step 3️⃣: Create Handler (15 seconds)
```csharp
// File: DelayHandler.cs
using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class DelayHandler(ILogger<DelayHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Delay;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not DelayType delay)
            {
                logger.LogWarning("SubAction is not DelayType");
                return;
            }

            logger.LogInformation("Delaying for {Seconds} seconds", delay.Seconds);
            await Task.Delay(delay.Seconds * 1000);
        }
    }
}
```

### ✅ DONE! That's It!

**What happens automatically:**
```
┌─────────────────────────────────────────┐
│  You create 2 files (Type + Handler)   │
└──────────────┬──────────────────────────┘
               │
               ├─────→ ✅ DI auto-registers handler
               ├─────→ ✅ EF Core auto-creates table
               ├─────→ ✅ UI auto-discovers SubAction
               ├─────→ ✅ UI auto-renders form fields
               ├─────→ ✅ UI auto-validates input
               ├─────→ ✅ UI auto-loads for editing
               └─────→ ✅ UI auto-saves on submit
```

---

## 🎨 What The User Sees

### In the AddSubAction Dialog:

**Step 1: Selection Screen**
```
┌────────────────────────────────────────┐
│  Select SubAction Type                 │
│  Choose the type of action to add      │
│                                        │
│  🔍 Search subactions...               │
│                                        │
│  ┌──────────────────────────────────┐ │
│  │ 🔔 Alert                         │ │
│  │    Show an alert with text...    │ │
│  ├──────────────────────────────────┤ │
│  │ 💬 Send Message                  │ │
│  │    Send a chat message...        │ │
│  ├──────────────────────────────────┤ │
│  │ ⏰ Delay                  [NEW!] │ │ ← Auto-appears!
│  │    Wait for a duration...        │ │
│  └──────────────────────────────────┘ │
└────────────────────────────────────────┘
```

**Step 2: Configuration Screen** (Auto-generated from GetUIFields!)
```
┌────────────────────────────────────────┐
│  ⏰ Configure Delay                    │
│                                        │
│  Seconds:                              │
│  ┌──────────┐                          │
│  │    5     │ [Number field]           │
│  └──────────┘                          │
│  Must be between 1 and 300             │
│                                        │
│  Enabled: [Toggle] ✅                  │
│                                        │
│  ┌──────┐  ┌────────┐                 │
│  │ Back │  │  Add   │                 │
│  └──────┘  └────────┘                 │
└────────────────────────────────────────┘
```

**All of this from just your SubAction type!** No Razor editing! 🎉

---

## 🧪 Testing Your New SubAction

```csharp
[Fact]
public void Delay_ValidatesCorrectly()
{
    var delay = new DelayType { Seconds = 500 };
    
    var error = delay.Validate();
    
    Assert.Equal("Delay must be between 1 and 300 seconds", error);
}

[Fact]
public void Delay_LoadsValues()
{
    var delay = new DelayType();
    delay.SetValues(new Dictionary<string, object?> 
    { 
        { "Seconds", 10 },
        { "Enabled", false }
    });
    
    Assert.Equal(10, delay.Seconds);
    Assert.False(delay.Enabled);
}

[Fact]
public async Task DelayHandler_Delays()
{
    var handler = new DelayHandler(logger);
    var delay = new DelayType { Seconds = 1 };
    
    var sw = Stopwatch.StartNew();
    await handler.ExecuteAsync(delay, new());
    sw.Stop();
    
    Assert.InRange(sw.ElapsedMilliseconds, 900, 1100);
}
```

---

## 🎯 Cheat Sheet

### Creating a Simple SubAction (5 lines)
```csharp
[SubActionMetadata(displayName: "X", description: "Y", icon: "mdi-Z", color: "Primary")]
public class XType : SimpleSubActionType
{
    public XType() { SubActionTypes = SubActionTypes.X; }
}
```

### Creating a SubAction with Custom Fields (80 lines)
```csharp
[SubActionMetadata(...)]
public class XType : SubActionType, ISubActionUIProvider
{
    // Properties
    public string MyProp { get; set; }
    
    // 4 methods: GetUIFields, GetValues, SetValues, Validate
}
```

### Creating a Handler (20 lines)
```csharp
public class XHandler : ISubActionHandler
{
    public SubActionTypes SupportedType => SubActionTypes.X;
    public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
    {
        if (subAction is XType x) { /* logic */ }
        return Task.CompletedTask;
    }
}
```

### Files to Edit
- ✅ Add enum value (1 line)
- ✅ Create Type class (1 file)
- ✅ Create Handler class (1 file)
- ❌ ~~Edit Registry~~ Auto
- ❌ ~~Edit DbContext~~ Auto
- ❌ ~~Edit Razor~~ Auto

---

## 🚀 Go Build Amazing SubActions!

With this system, you can now:
- Create SubActions **3x faster**
- Make changes **without fear**
- Refactor **with confidence**
- Test **in isolation**
- Maintain **with ease**

**The Razor page is now YOUR generic, reusable SubAction renderer!** 🎊

Happy coding! 🚀✨
