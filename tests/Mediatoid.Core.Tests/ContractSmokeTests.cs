using Mediatoid;

namespace Mediatoid.Core.Tests;

public class ContractSmokeTests
{
    [Fact]
    public void IRequestInterfaceShouldExist()
        => Assert.NotNull(typeof(IRequest<>));

    [Fact]
    public void ISenderInterfaceShouldExist()
        => Assert.NotNull(typeof(ISender));

    [Fact]
    public void IPipelineBehaviorInterfaceShouldExist()
        => Assert.NotNull(typeof(Mediatoid.Pipeline.IPipelineBehavior<,>));
}
