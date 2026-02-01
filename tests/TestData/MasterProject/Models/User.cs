namespace MasterProject.Models
{
    /// <summary>
    /// Represents a user in the system
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public User()
        {
        }

        public User(int id, string name, string email)
        {
            Id = id;
            Name = name;
            Email = email;
            IsActive = true;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public string GetFullInfo()
        {
            return $"{Name} ({Email})";
        }
    }
}
