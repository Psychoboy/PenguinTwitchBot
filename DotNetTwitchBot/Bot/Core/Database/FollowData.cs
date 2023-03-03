using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using SQLite;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class FollowData
    {
        private ILogger<FollowData> _logger;
        private SQLiteAsyncConnection _db;

        public FollowData(
            ILogger<FollowData> logger,
            IDatabase db
        )
        {
            _logger = logger;
            _db = db.Db;
        }

        public async Task Insert(Follower follower) {
            await _db.InsertAsync(follower);
        }

        public async Task InsertAll(IEnumerable<Follower> followers) {
            await _db.InsertAllAsync(followers);
        }

        public async Task<Follower?> GetFollower(string username) {
            return await _db.FindAsync<Follower>(x => x.Username.ToLower() == username);
        }
    }
}