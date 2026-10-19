using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ward.Constants;
using Xunit;

namespace ward.Tests
{
    public class AuthorizationPolicyTests
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuthorizationPolicyProvider _policyProvider;

        public AuthorizationPolicyTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AppPolicies.RequireAdmin, policy =>
                    policy.RequireRole(AppRoles.Admin));

                options.AddPolicy(AppPolicies.ManageCatalog, policy =>
                    policy.RequireRole(AppRoles.Admin, AppRoles.Manager));

                options.AddPolicy(AppPolicies.ManageInventory, policy =>
                    policy.RequireRole(AppRoles.Admin, AppRoles.Manager, AppRoles.InventoryClerk));

                options.AddPolicy(AppPolicies.ProcessSales, policy =>
                    policy.RequireRole(AppRoles.Admin, AppRoles.Manager, AppRoles.Cashier));

                options.AddPolicy(AppPolicies.ProcessReturns, policy =>
                    policy.RequireRole(AppRoles.Admin, AppRoles.Manager));

                options.AddPolicy(AppPolicies.ViewAuditAndReports, policy =>
                    policy.RequireRole(AppRoles.Admin, AppRoles.Manager));
            });

            var serviceProvider = services.BuildServiceProvider();
            _authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();
            _policyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        }

        private ClaimsPrincipal CreateUserWithRole(string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth");

            return new ClaimsPrincipal(identity);
        }

        [Theory]
        [InlineData(AppRoles.Admin, true)]
        [InlineData(AppRoles.Manager, false)]
        [InlineData(AppRoles.Cashier, false)]
        [InlineData(AppRoles.InventoryClerk, false)]
        public async Task RequireAdminPolicy_AuthorizesOnlyAdmin(string role, bool expectedSuccess)
        {
            var user = CreateUserWithRole(role);
            var result = await _authorizationService.AuthorizeAsync(user, AppPolicies.RequireAdmin);
            Assert.Equal(expectedSuccess, result.Succeeded);
        }

        [Theory]
        [InlineData(AppRoles.Admin, true)]
        [InlineData(AppRoles.Manager, true)]
        [InlineData(AppRoles.InventoryClerk, true)]
        [InlineData(AppRoles.Cashier, false)]
        public async Task ManageInventoryPolicy_AuthorizesExpectedRoles(string role, bool expectedSuccess)
        {
            var user = CreateUserWithRole(role);
            var result = await _authorizationService.AuthorizeAsync(user, AppPolicies.ManageInventory);
            Assert.Equal(expectedSuccess, result.Succeeded);
        }

        [Theory]
        [InlineData(AppRoles.Admin, true)]
        [InlineData(AppRoles.Manager, true)]
        [InlineData(AppRoles.Cashier, true)]
        [InlineData(AppRoles.InventoryClerk, false)]
        public async Task ProcessSalesPolicy_AuthorizesExpectedRoles(string role, bool expectedSuccess)
        {
            var user = CreateUserWithRole(role);
            var result = await _authorizationService.AuthorizeAsync(user, AppPolicies.ProcessSales);
            Assert.Equal(expectedSuccess, result.Succeeded);
        }

        [Theory]
        [InlineData(AppRoles.Admin, true)]
        [InlineData(AppRoles.Manager, true)]
        [InlineData(AppRoles.Cashier, false)]
        [InlineData(AppRoles.InventoryClerk, false)]
        public async Task ProcessReturnsPolicy_AuthorizesOnlyAdminAndManager(string role, bool expectedSuccess)
        {
            var user = CreateUserWithRole(role);
            var result = await _authorizationService.AuthorizeAsync(user, AppPolicies.ProcessReturns);
            Assert.Equal(expectedSuccess, result.Succeeded);
        }
    }
}
