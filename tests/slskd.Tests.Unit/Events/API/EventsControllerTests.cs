using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using slskd.Events.API;
using Xunit;

namespace slskd.Tests.Unit.Events.API;

public class EventsControllerTests
{
    [Fact]
    public void RaiseEvent_RequiresAdministrator()
    {
        var method = typeof(EventsController).GetMethod(nameof(EventsController.RaiseEvent));
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicy.Any, authorize.Policy);
        Assert.Equal(AuthRole.AdministratorOnly, authorize.Roles);
    }
}
