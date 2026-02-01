using MasterProject.Interfaces;
using MasterProject.Models;

namespace MasterProject.Services
{
    public class AdminUserService : UserService
    {
        public AdminUserService(IUserRepository repository) : base(repository)
        {
        }

        public void DeleteUser(int userId)
        {
            var user = GetById(userId);
            // Additional admin logic here
        }
    }
}
