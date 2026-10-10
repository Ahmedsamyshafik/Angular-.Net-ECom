namespace Ecom.Core.DTO.UserDto
{
    public class UserDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string? ImgUrl { get; set; }
    }

    public class UserWithTokenDto
    {
        public string token { get; set; }
        public UserDto user { get; set; }
    }
}
