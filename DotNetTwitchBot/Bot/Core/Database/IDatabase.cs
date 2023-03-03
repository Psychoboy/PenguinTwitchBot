using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public interface IDatabase : IDisposable
    {
        public SQLiteAsyncConnection Db {get;}
    }
}