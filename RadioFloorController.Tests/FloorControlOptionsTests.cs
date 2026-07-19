using Microsoft.Extensions.Configuration;
using RadioFloorController.Domain;

namespace RadioFloorController.Tests;

/// <summary>
/// Verifies the <see cref="FloorControlOptions"/> defaults and configuration binding behavior
/// documented on the type — i.e. the same "FloorControl" section binding path that
/// <c>Program.cs</c> wires up via <c>services.Configure&lt;FloorControlOptions&gt;</c>.
/// </summary>
public class FloorControlOptionsTests
{
    [Fact]
    public void Defaults_MatchDocumentedValues_WhenSectionIsAbsent()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = new FloorControlOptions();
        configuration.GetSection(FloorControlOptions.SectionName).Bind(options);

        Assert.Equal(TimeSpan.FromSeconds(120), options.HoldTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.SweepInterval);
    }

    [Fact]
    public void Binding_OverridesDefaults_WhenSectionValuesAreProvided()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{FloorControlOptions.SectionName}:HoldTimeout"] = "00:00:30",
                [$"{FloorControlOptions.SectionName}:SweepInterval"] = "00:00:02",
            })
            .Build();

        var options = new FloorControlOptions();
        configuration.GetSection(FloorControlOptions.SectionName).Bind(options);

        Assert.Equal(TimeSpan.FromSeconds(30), options.HoldTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), options.SweepInterval);
    }

    [Fact]
    public void Binding_OnlyOverridesConfiguredKey_LeavingOtherDefaultInPlace()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{FloorControlOptions.SectionName}:HoldTimeout"] = "00:01:00",
            })
            .Build();

        var options = new FloorControlOptions();
        configuration.GetSection(FloorControlOptions.SectionName).Bind(options);

        Assert.Equal(TimeSpan.FromSeconds(60), options.HoldTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.SweepInterval);
    }
}
