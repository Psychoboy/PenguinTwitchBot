using System.Security.Cryptography.X509Certificates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using SQLite;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class ViewerData
    {
        private ILogger<ViewerData> _logger;
        private SQLiteAsyncConnection _db;

        public ViewerData(
            ILogger<ViewerData> logger,
            IDatabase db
            ) 
        {
            _logger = logger;
            _db = db.Db;
        }

        public async Task<Viewer?> FindOne(string username) {
            return await _db.FindAsync<Viewer>(x => x.Username.ToLower() == username.ToLower());
        }

        public async Task Update(Viewer viewer) {
            await _db.UpdateAsync(viewer);
        }

        public async Task InsertOrUpdate(Viewer viewer) {
            await _db.InsertOrReplaceAsync(viewer);
        }
    }
}