using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using LiteDB;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class DbViewerPoints : IDbViewerPoints
    {
        private LiteDatabase _liteDb;
        private string TableName = "ViewerPoints";

        public DbViewerPoints(ILiteDbContext liteDbContext) {
            _liteDb = liteDbContext.Database;

            //Setup Index as username
            var col = _liteDb.GetCollection<ViewerPoints>(TableName);
            col.EnsureIndex(x => x.Username);
        }
        public IEnumerable<ViewerPoints> FindAll()
        {
            return _liteDb.GetCollection<ViewerPoints>(TableName).FindAll();   
        }

        public ViewerPoints? FindOne(string username)
        {
            return _liteDb.GetCollection<ViewerPoints>(TableName)
            .Find(x => x.Username.Equals(username, StringComparison.CurrentCultureIgnoreCase))
            .FirstOrDefault();
        }

        public int Insert(ViewerPoints viewerPoints)
        {
            return _liteDb.GetCollection<ViewerPoints>(TableName).Insert(viewerPoints);
        }

        public bool InsertOrUpdate(ViewerPoints viewerPoints)
        {
            return _liteDb.GetCollection<ViewerPoints>(TableName).Upsert(viewerPoints);
        }

        public bool Update(ViewerPoints viewerPoints)
        {
            return _liteDb.GetCollection<ViewerPoints>(TableName).Update(viewerPoints);
        }
    }
}