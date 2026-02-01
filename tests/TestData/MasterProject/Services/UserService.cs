using MasterProject.Interfaces;
using MasterProject.Models;

namespace MasterProject.Services
{
    /// <summary>
    /// Service for managing users
    /// </summary>
    public class UserService
    {
        private readonly IUserRepository _repository;

        public UserService(IUserRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Gets a user by their unique identifier
        /// </summary>
        public User GetById(int id)
        {
            return _repository.GetById(id);
        }

        public IEnumerable<User> GetAllActive()
        {
            return _repository.GetAll().Where(u => u.IsActive);
        }

        public void CreateUser(string name, string email)
        {
            var user = new User
            {
                Name = name,
                Email = email,
                IsActive = true
            };
            _repository.Add(user);
        }

        public void ActivateUser(int userId)
        {
            var user = GetById(userId);
            user.Activate();
            _repository.Update(user);
        }

        public void DeactivateUser(int userId)
        {
            var user = GetById(userId);
            user.Deactivate();
            _repository.Update(user);
        }

        public UserDto ToDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email
            };
        }

        private void ValidateUser(User user)
        {
            if (string.IsNullOrEmpty(user.Name))
                throw new ArgumentException("Name is required");
            if (string.IsNullOrEmpty(user.Email))
                throw new ArgumentException("Email is required");
        }
    }
}
