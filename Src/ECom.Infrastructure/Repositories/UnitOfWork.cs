using Ecom.Core.Entities.User;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;
using Ecom.Core.Services.Facads;
using Ecom.Core.Services.Orchestrators;

using ECom.Infrastructure.Data;

using Microsoft.AspNetCore.Identity;

namespace ECom.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        #region Private Fields

        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManage;
        private readonly SignInManager<AppUser> _signInManager;
        #endregion

        #region Properties

        public IProductService ProductService { get; }

        public ICategoryService CategoryService { get; }

        public IDiscountFacade DiscountFacade { get; }

        public IAddProductOrchestrator AddProductOrchestrator { get; }

        public ICartProductFacade CartProductFacade { get; }

        public IEmailService EmailService { get; }


        public IGenerateToken GenerateToken { get; }
        public IMyAuth myAuth { get; }

        public IOrderService OrderService { get; }

        public IShippingAddressesRepo ShippingAddressesRepo { get; }
        public IStockReservationService StockReservationService { get; }



        #endregion

        #region Constructors

        public UnitOfWork(
            AppDbContext context,
            IProductService productService,
            ICategoryService categoryService,
            IDiscountFacade discountFacade,
            IAddProductOrchestrator addProductOrchestrator,
            ICartProductFacade cartProductFacade,
            IEmailService emailService,
            UserManager<AppUser> userManage,
            SignInManager<AppUser> signInManager,
            IGenerateToken generateToken,
            IOrderService orderService,
            IShippingAddressesRepo shippingAddressesRepo,
            IStockReservationService stockReservationService)
        {
            _context = context;
            ProductService = productService;
            CategoryService = categoryService;
            DiscountFacade = discountFacade;
            AddProductOrchestrator = addProductOrchestrator;
            CartProductFacade = cartProductFacade;
            EmailService = emailService;
            _signInManager = signInManager;
            _userManage = userManage;
            GenerateToken = generateToken;
            myAuth = new AuthReposiory(userManage, signInManager, generateToken, emailService);
            OrderService = orderService;
            ShippingAddressesRepo = shippingAddressesRepo;
            StockReservationService = stockReservationService;
        }

        #endregion

        #region Methods

        public async Task Commit(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        #endregion
    }
}
