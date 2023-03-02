using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public interface ILiteDbContext
    {
        LiteDatabase Database { get; }
    }
}