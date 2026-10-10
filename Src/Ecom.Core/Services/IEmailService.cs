using Ecom.Core.DTO.sharing;

namespace Ecom.Core.Services
{
    public interface IEmailService
    {
        Task SendEmail(EmailDto emailDto);
        string returnEmailFrom();
    }
}
