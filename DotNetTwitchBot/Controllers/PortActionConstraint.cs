﻿using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace DotNetTwitchBot.Controllers
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PortActionConstraint(int Port) : ActionMethodSelectorAttribute
    {
        public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
        {
            //external port
            var externalPort = routeContext.HttpContext.Request.Host.Port;
            //local port 
            var localPort = routeContext.HttpContext.Connection.LocalPort;
            //write here your custom logic. for example  
            return Port == localPort;
        }
    }
}
