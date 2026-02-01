using MasterProject.Models;
using MasterProject.Services;

namespace MasterProject.Controllers
{
    public class UserController
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        public User GetUser(int id)
        {
            return _userService.GetById(id);
        }

        public IEnumerable<User> ListActiveUsers()
        {
            return _userService.GetAllActive();
        }

        public void CreateUser(string name, string email)
        {
            _userService.CreateUser(name, email);
        }
    }
}
