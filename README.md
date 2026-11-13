# Consumer-driven contract testing with Pact

A small demo of consumer-driven contract testing between a TypeScript consumer and an ASP.NET Core provider.

```
consumer/   Node.js + TypeScript client for the Customer API, with Pact consumer tests (Jest)
provider/   ASP.NET Core minimal API (CustomerApi) + Pact provider verification test (CustomerApi.Tests, xUnit)
pacts/      Contract written by the consumer tests, published from here to PactFlow
```

## The provider

`GET /customers/{id}`

| Situation                    | Response |
|------------------------------|----------|
| Customer exists              | `200` with customer JSON |
| Customer ID not found        | `404` (problem details) |
| Customer ID is not an integer| `400` (problem details) |

Run it with `dotnet run --project provider/CustomerApi`; customer `12345` is seeded in memory.

The API is described by an OpenAPI 3.0.1 spec in `provider/oas/openapi.yaml`.

## The contract

The consumer tests (`consumer/test/customerClient.pact.test.ts`) define three interactions:

- **200** (`given customer 12345 exists`): expectations on `id` (integer), `name` (string), `status` (`ACTIVE` or `INACTIVE`) and every `address` field, all matched by type/pattern rather than exact value. `email`, `phone` and `lastModified` are deliberately not part of the contract, so the provider is free to change them.
- **404** (`given customer 99999 does not exist`): status code only.
- **400** (no provider state): request for `/customers/abc`, status code only.

## Running

Requires Node.js 22+, the .NET 10 SDK, git, and a PactFlow account. Set these environment variables:

| Variable               | Purpose |
|------------------------|---------|
| `PACT_BROKER_BASE_URL` | PactFlow base URL, e.g. `https://<your-org>.pactflow.io` |
| `PACT_BROKER_TOKEN`    | PactFlow API token (read/write) |

```bash
# 1. Consumer: run the Pact tests, which write pacts/OSCustomerConsumer-SAPCustomerProvider.json
cd consumer
npm install
npm test

# 2. Consumer: publish the contract to PactFlow (version and branch taken from git / CI)
npm run pact:publish

# 3. Provider: fetch the contracts from PactFlow, verify, and publish the results back
cd ../provider
dotnet test
```

The provider test (`CustomerApi.Tests/CustomerApiPactVerificationTests.cs`) starts the real API on a random Kestrel port via `WebApplicationFactory`, adds a `/provider-states` endpoint (`ProviderStateMiddleware`) so the verifier can set up each `given` state, then:

- fetches the pacts for `SAPCustomerProvider` from PactFlow using the consumer version selectors `mainBranch`, `deployedOrReleased` and `matchingBranch`, with pending pacts enabled;
- replays every interaction against the running API;
- publishes the verification results to PactFlow as provider version `1.0.0` on branch `main` (hardcoded for the demo).

The consumer's main branch must be set in PactFlow (it is inferred automatically from the first publication on `main`/`master`/`develop`), otherwise the `mainBranch` selector will find nothing.
