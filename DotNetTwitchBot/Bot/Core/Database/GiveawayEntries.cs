using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using LiteDB;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class GiveawayEntries : IGiveawayEntries
    {
        
        private string TableName = "GiveawayEntries";
        private LiteDatabase _liteDb;

        public GiveawayEntries(ILiteDbContext liteDbContext) {
            _liteDb = liteDbContext.Database;
        }
        public int DeleteAll()
        {
            return _liteDb.GetCollection<GiveawayEntry>(TableName).DeleteAll();
        }

        public IEnumerable<GiveawayEntry> FindAll()
        {
            return _liteDb.GetCollection<GiveawayEntry>(TableName).FindAll();
        }

        public int InsertBulk(IEnumerable<GiveawayEntry> entries)
        {
            return _liteDb.GetCollection<GiveawayEntry>(TableName).InsertBulk(entries);
        }
    }
}