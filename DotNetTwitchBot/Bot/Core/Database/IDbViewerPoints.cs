using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public interface IDbViewerPoints
    {
        ViewerPoints? FindOne(string username);
        IEnumerable<ViewerPoints> FindAll();
        int Insert(ViewerPoints viewerPoints);
        bool Update(ViewerPoints viewerPoints);
        bool InsertOrUpdate(ViewerPoints viewerPoints);
    }
}