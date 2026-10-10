namespace Ecom.Core.DTO.UserDto
{
    public class ChangePasswordDto
    {
        public string Email { get; set; }
        public string oldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
