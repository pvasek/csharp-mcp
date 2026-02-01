using MasterProject.Models;

namespace MasterProject.Interfaces
{
    /// <summary>
    /// Repository interface for user data access
    /// </summary>
    public interface IUserRepository
    {
        User GetById(int id);
        IEnumerable<User> GetAll();
        void Add(User user);
        void Update(User user);
        void Delete(int id);
    }
}
