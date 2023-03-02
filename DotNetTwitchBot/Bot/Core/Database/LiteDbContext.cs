using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Options;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class LiteDbContext : ILiteDbContext
    {
        public LiteDatabase Database {get;}

        public LiteDbContext(IOptions<LiteDbOptions> options){
            Database = new LiteDatabase(options.Value.DatabaseLocation);
        }
    }
}