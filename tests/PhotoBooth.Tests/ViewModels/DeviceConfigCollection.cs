using Xunit;

namespace PhotoBooth.Tests.ViewModels;

/// <summary>
/// WARNING: All test classes that mutate PhotoBooth.Event.DeviceConfig (static mutable
/// state: EnablePrinting, PrinterName, PrintMedia, ...) MUST be in this collection to
/// prevent parallel execution from corrupting the shared values. Same constraint as
/// SequentialCollection guards for SequentialNumberService.CounterFilePath.
/// </summary>
[CollectionDefinition("DeviceConfig", DisableParallelization = true)]
public class DeviceConfigCollection { }
