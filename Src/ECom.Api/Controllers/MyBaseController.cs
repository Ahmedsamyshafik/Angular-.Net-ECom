using AutoMapper;
using ECom.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MyBaseController : ControllerBase
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IMapper _mapper;

        public MyBaseController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
    }
}
