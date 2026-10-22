using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using slskd.Core.API;
using slskd.Messaging.API;
using slskd.Search.API;
using slskd.Shares.API;
using slskd.Transfers.API;
using Xunit;

namespace slskd.Tests.Unit.Core.API;

public class WriteAuthorizationTests
{
    [Theory]
    [InlineData(typeof(ServerController))]
    [InlineData(typeof(SharesController))]
    [InlineData(typeof(TransfersController))]
    [InlineData(typeof(SearchesController))]
    [InlineData(typeof(RoomsController))]
    [InlineData(typeof(ConversationsController))]
    public void StateChangingRoutes_RequireWriteAccess(Type controllerType)
    {
        var methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>()
                .Any(attribute => attribute.HttpMethods.Any(verb => verb is "POST" or "PUT" or "PATCH" or "DELETE")));

        Assert.NotEmpty(methods);

        foreach (var method in methods)
        {
            var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal(AuthPolicy.Any, authorize.Policy);
            Assert.Equal(AuthRole.ReadWriteOrAdministrator, authorize.Roles);
        }
    }
}
