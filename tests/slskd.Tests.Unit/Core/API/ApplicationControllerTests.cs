using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Core.API;
using Xunit;

namespace slskd.Tests.Unit.Core.API;

public class ApplicationControllerTests
{
    [Fact]
    public async Task DumpMemory_IsForbiddenByDefault()
    {
        var controller = new ApplicationController(
            lifetime: null,
            application: null,
            optionsMonitor: new TestOptionsMonitor<global::slskd.Options>(new global::slskd.Options()),
            applicationStateMonitor: null);

        var result = await controller.DumpMemory();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void DumpMemory_RequiresAdministrator()
    {
        var method = typeof(ApplicationController).GetMethod(nameof(ApplicationController.DumpMemory));
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicy.Any, authorize.Policy);
        Assert.Equal(AuthRole.AdministratorOnly, authorize.Roles);
    }
}
