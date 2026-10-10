namespace Ecom.Core.DTO.UserDto
{
    public class ResetPasswordDto
    {
        public string Token { get; set; }
        public string Email { get; set; }
        public string password { get; set; }
    }
}
