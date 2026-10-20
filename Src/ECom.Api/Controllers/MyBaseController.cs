using Ecom.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MyBaseController : ControllerBase
    {
        // Audit/system identity used by the write endpoints of Category/Product.
        // It is a fixed system value on purpose: it must never be taken from the
        // request cookie/claims (the signed-in shopper id is not a system user).
        protected const string SystemUserId = "1";

        protected readonly IUnitOfWork _unitOfWork;

        public MyBaseController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
    }
}
