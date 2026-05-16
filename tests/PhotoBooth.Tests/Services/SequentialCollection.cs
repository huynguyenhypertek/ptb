using Xunit;

namespace PhotoBooth.Tests.Services;

/// <summary>
/// WARNING: All test classes that access SequentialNumberService.CounterFilePath
/// (a static mutable field) MUST be in this collection to prevent parallel execution
/// from corrupting the shared state. Never add SequentialNumberService tests to a
/// different collection without understanding this constraint.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection { }

