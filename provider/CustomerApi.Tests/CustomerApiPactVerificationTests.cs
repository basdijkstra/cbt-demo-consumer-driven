using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace CustomerApi.Tests;

public class CustomerApiPactVerificationTests(ITestOutputHelper output, CustomerApiFactory factory)
    : IClassFixture<CustomerApiFactory>
{
    private const string ProviderName = "SAPCustomerProvider";

    // Hardcoded for demo purposes. In a real pipeline these would come from git / CI.
    private const string ProviderVersion = "1.0.0";
    private const string ProviderBranch = "main";

    [Fact]
    public void VerifyPactsFromPactFlow()
    {
        var brokerBaseUrl = RequiredEnvironmentVariable("PACT_BROKER_BASE_URL");
        var brokerToken = RequiredEnvironmentVariable("PACT_BROKER_TOKEN");

        var serverUri = factory.ServerUri;

        var config = new PactVerifierConfig
        {
            Outputters = [new XunitOutput(output)],
        };

        using var verifier = new PactVerifier(ProviderName, config);

        verifier
            .WithHttpEndpoint(serverUri)
            .WithPactBrokerSource(new Uri(brokerBaseUrl), options => options
                .TokenAuthentication(brokerToken)
                // The recommended selectors: the consumers' main branch, whatever is currently
                // deployed or released, and any consumer branch with the same name as this one.
                .ConsumerVersionSelectors(
                    new ConsumerVersionSelector { MainBranch = true },
                    new ConsumerVersionSelector { DeployedOrReleased = true },
                    new ConsumerVersionSelector { MatchingBranch = true })
                // Needed for pending pacts and the MatchingBranch selector.
                .ProviderBranch(ProviderBranch)
                // New, not yet verified expectations do not fail the provider build.
                .EnablePending()
                .PublishResults(ProviderVersion, publish => publish.ProviderBranch(ProviderBranch)))
            .WithProviderStateUrl(new Uri(serverUri, ProviderStateMiddleware.Path))
            .Verify();
    }

    private static string RequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Environment variable {name} must be set.");

    private class XunitOutput(ITestOutputHelper output) : IOutput
    {
        public void WriteLine(string line) => output.WriteLine(line);
    }
}
