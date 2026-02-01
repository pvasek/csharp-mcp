using MasterProject.Interfaces;
using MasterProject.Models;

namespace MasterProject.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly List<User> _users = new();

        public User GetById(int id)
        {
            return _users.FirstOrDefault(u => u.Id == id) ?? throw new KeyNotFoundException($"User with ID {id} not found");
        }

        public IEnumerable<User> GetAll()
        {
            return _users.AsReadOnly();
        }

        public void Add(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            _users.Add(user);
        }

        public void Update(User user)
        {
            var existing = GetById(user.Id);
            var index = _users.IndexOf(existing);
            _users[index] = user;
        }

        public void Delete(int id)
        {
            var user = GetById(id);
            _users.Remove(user);
        }
    }
}
