using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core.Database;
using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class BackupDbJob : IJob
    {
        private readonly ILogger<BackupDbJob> _logger;
        private readonly IDatabase _database;

        public BackupDbJob(
            ILogger<BackupDbJob> logger,
            IDatabase database
            )
        {
            _logger = logger;
            _database = database;
            
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("DB Backup Starting");
            await _database.Backup();
            _logger.LogInformation("DB Backup Complete");           
        }
    }
}