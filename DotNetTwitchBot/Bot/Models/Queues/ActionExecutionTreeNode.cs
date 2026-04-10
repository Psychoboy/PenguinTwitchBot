using System;
using System.Collections.Generic;

namespace DotNetTwitchBot.Bot.Models.Queues
{
    /// <summary>
    /// Represents a node in the action execution tree for hierarchical display
    /// </summary>
    public class ActionExecutionTreeNode
    {
        public Guid LogId { get; set; }
        public string ActionName { get; set; } = string.Empty;
        public ActionExecutionState State { get; set; }
        public string QueueName { get; set; } = string.Empty;
        public DateTime EnqueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public int VariableCount { get; set; }
        public List<SubActionTreeNode> SubActions { get; set; } = new();
        public int Level { get; set; }
        public bool IsExpanded { get; set; } = true;
        public Guid? ParentActionLogId { get; set; }
    }

    /// <summary>
    /// Represents a subaction node in the execution tree
    /// </summary>
    public class SubActionTreeNode
    {
        public int Index { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string State { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Messages { get; set; } = new();
        public ActionExecutionTreeNode? ChildAction { get; set; }
        public bool IsExpanded { get; set; } = true;
    }
}
