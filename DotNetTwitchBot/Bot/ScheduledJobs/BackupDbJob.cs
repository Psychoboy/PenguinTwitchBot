using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core.Database;
using MySqlConnector;
using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class BackupDbJob : IJob
    {
        private readonly ILogger<BackupDbJob> _logger;
        private readonly IDatabaseTools _databaseTools;

        public BackupDbJob(ILogger<BackupDbJob> logger, IDatabaseTools databaseTools)
        {
            _logger = logger;
            _databaseTools = databaseTools;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("DB Backup Starting");
            await _databaseTools.Backup();
            _logger.LogInformation("DB Backup Complete");
        }
    }
}