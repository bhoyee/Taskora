using Xunit;

// Disables parallel test execution across this assembly, since the API integration tests
// share a single test host/database and would otherwise race each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
