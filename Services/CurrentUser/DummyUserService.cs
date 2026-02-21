namespace Hive_Movie.Services.CurrentUser
{
    // A temporary dummy service until we implement real JWT validation!
    public class DummyUserService : ICurrentUserService
    {
        // Pretend user ID "1" is always logged in for now
        public string? UserId => "1";
    }
}