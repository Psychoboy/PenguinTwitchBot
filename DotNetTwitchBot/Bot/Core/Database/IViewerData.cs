using System.Reflection.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public interface IViewerData
    {
        Viewer? FindOne(string username);
        IEnumerable<Viewer> FindAll();
        int Insert(Viewer viewer);
        bool Update(Viewer viewer);

        bool InsertOrUpdate(Viewer viewer);
    }
}