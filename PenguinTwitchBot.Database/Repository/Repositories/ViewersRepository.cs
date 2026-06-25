using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class ViewersRepository : GenericRepository<Viewer>, IViewersRepository
    {
        public ViewersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
