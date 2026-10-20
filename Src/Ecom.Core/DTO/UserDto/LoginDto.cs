namespace ECom.Infrastructure.Repositories
{
    public class LoginDto
    {
        public string Email { get; set; }
        public string password { get; set; }

        public string? cartGuestId { get; set; }
    }
}
