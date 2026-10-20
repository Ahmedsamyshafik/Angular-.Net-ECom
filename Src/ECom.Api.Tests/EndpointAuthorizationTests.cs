using System.Reflection;

using Ecom.Core.Constants;

using ECom.Api.Controllers.Account.V1;
using ECom.Api.Controllers.Cart.V1;
using ECom.Api.Controllers.Category.V1;
using ECom.Api.Controllers.Order.V1;
using ECom.Api.Controllers.Payment.V1;
using ECom.Api.Controllers.Product.V1;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Tests;

/// <summary>
/// Locks the [Authorize]/[AllowAnonymous]/roles surface declared on controllers. These are
/// attribute-level tests (no host): the middleware behavior is ASP.NET Core's, but the
/// intended protection of every endpoint is documented and guarded here.
/// </summary>
public class EndpointAuthorizationTests
{
    [Theory]
    [InlineData(typeof(ProductController), "Add")]
    [InlineData(typeof(ProductController), "Update")]
    [InlineData(typeof(ProductController), "Delete")]
    [InlineData(typeof(ProductController), "ApplyDiscount")]
    [InlineData(typeof(ProductController), "ApplyCategoryDiscount")]
    [InlineData(typeof(CategoryController), "Add")]
    [InlineData(typeof(CategoryController), "Update")]
    [InlineData(typeof(CategoryController), "Delete")]
    public void CatalogWriteActions_RequireAdminRole(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);

        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        // Plain [Authorize] would let any signed-in shopper mutate the catalog; the role is
        // what makes these administrator-only.
        Assert.Equal(AppRoles.Admin, authorize!.Roles);
    }

    [Theory]
    [InlineData(typeof(ProductController), "GetAllV1")]
    [InlineData(typeof(ProductController), "GetById")]
    [InlineData(typeof(CategoryController), "GetAll")]
    [InlineData(typeof(CategoryController), "GetById")]
    public void CatalogReadActions_RemainPublic(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);

        Assert.True(
            method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any(),
            $"{controllerType.Name}.{actionName} must stay anonymously accessible.");
    }

    [Theory]
    [InlineData(typeof(OrderController))]
    [InlineData(typeof(PaymentController))]
    public void AuthenticatedControllers_RequireAuthorizationAtClassLevel(Type controllerType)
    {
        Assert.True(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Any(),
            $"{controllerType.Name} must require authentication at the controller level.");
    }

    [Fact]
    public void CartController_AllowsAnonymousGuestShopping()
    {
        // Guest shopping is intentional: no class-level [Authorize], only [AllowAnonymous].
        Assert.True(
            typeof(CartController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any(),
            "CartController must stay reachable for guests.");

        Assert.False(
            typeof(CartController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Any(),
            "CartController must not require authentication at the controller level.");
    }

    [Theory]
    [InlineData(typeof(AccountController), "ChangePassword")]
    [InlineData(typeof(AccountController), "UpdateAddress")]
    [InlineData(typeof(AccountController), "GetAddress")]
    public void SensitiveAccountActions_RequireAuthentication(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);

        Assert.True(
            method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Any(),
            $"{controllerType.Name}.{actionName} must require authentication.");
    }

    [Theory]
    [InlineData(typeof(AccountController), "Login")]
    [InlineData(typeof(AccountController), "Register")]
    [InlineData(typeof(AccountController), "ForgetPassword")]
    [InlineData(typeof(AccountController), "ResetPassword")]
    [InlineData(typeof(AccountController), "ActiveAccount")]
    public void AnonymousAccountActions_DoNotRequireAuthentication(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);

        Assert.False(
            method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Any(),
            $"{controllerType.Name}.{actionName} must stay anonymous.");
    }

    [Fact]
    public void PaymentWebhook_RemainsAnonymous()
    {
        var method = typeof(PaymentController).GetMethod("Webhook");
        Assert.NotNull(method);

        Assert.True(
            method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any(),
            "The Stripe webhook must stay anonymous (it is authenticated by signature).");
    }
}
