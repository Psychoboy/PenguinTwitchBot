using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using LiteDB;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class ViewerData : IViewerData
    {
        private LiteDatabase _liteDb;
        private string TableName = "Viewers";

        public ViewerData(ILiteDbContext liteDbContext) {
            _liteDb = liteDbContext.Database;
            var col = _liteDb.GetCollection<Viewer>(TableName);
            col.EnsureIndex(x => x.Username);
        }
        public IEnumerable<Viewer> FindAll()
        {
            return _liteDb.GetCollection<Viewer>(TableName).FindAll();   
        }

        public Viewer? FindOne(string username)
        {
            return _liteDb.GetCollection<Viewer>(TableName).Find(x => x.Username == username.ToLower()).FirstOrDefault();
        }

        public int Insert(Viewer viewer)
        {
            return _liteDb.GetCollection<Viewer>(TableName).Insert(viewer);
        }

        public bool InsertOrUpdate(Viewer viewer)
        {
            return _liteDb.GetCollection<Viewer>(TableName).Upsert(viewer);
        }

        public bool Update(Viewer viewer)
        {
            return _liteDb.GetCollection<Viewer>(TableName).Update(viewer);
        }
    }
}