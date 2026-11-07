using Xunit;

namespace Lacoos.IntegrationTests.Support;

[Collection(ApiCollection.Name)]
public abstract class ApiTest
{
    protected ApiTest(ApiFixture api)
    {
        Api = api;
        Api.Clock.FixedAt = null;
        Api.Emails.ShouldFail = false;
    }

    protected ApiFixture Api { get; }

    protected static string W(Guid weddingId) => Scenario.W(weddingId);

    /// <summary>Dono com um casamento novo, isolado dos demais testes.</summary>
    protected async Task<(TestUser Owner, string Base, Guid WeddingId)> NewOwner()
    {
        var owner = await Api.NewUser();
        var weddingId = await owner.NewWedding();
        return (owner, W(weddingId), weddingId);
    }
}
