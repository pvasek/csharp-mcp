namespace MasterProject.Services
{
    public abstract class BaseService
    {
        protected void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }
    }
}
