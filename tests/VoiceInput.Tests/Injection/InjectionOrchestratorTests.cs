using System;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceInput.Core.Injection;
using Xunit;

namespace VoiceInput.Tests.Injection;

public class InjectionOrchestratorTests
{
    [Fact]
    public void IInjectionOrchestrator_InterfaceContract()
    {
        typeof(IInjectionOrchestrator).GetMethod("InjectAsync").Should().NotBeNull();
    }

    [Fact]
    public void InjectionResult_HasExpectedValues()
    {
        Enum.GetValues<InjectionResult>().Should().Contain(InjectionResult.Success);
        Enum.GetValues<InjectionResult>().Should().Contain(InjectionResult.SkippedElevated);
        Enum.GetValues<InjectionResult>().Should().Contain(InjectionResult.SkippedPassword);
        Enum.GetValues<InjectionResult>().Should().Contain(InjectionResult.Error);
    }
}
