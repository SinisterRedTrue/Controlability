using Controlability.Core.Mapping;
using Controlability.Core.Models;

namespace Controlability.Core.Tests;

public class ButtonRemapperTests
{
    [Fact]
    public void Empty_DropsEverything()
    {
        ButtonRemapper.Empty.Remap(ButtonId.South | ButtonId.Start).Should().Be(XboxButton.None);
    }

    [Fact]
    public void Remap_MapsSingleButton()
    {
        var remapper = ButtonRemapper.Empty.With(ButtonId.South, XboxButton.A);

        remapper.Remap(ButtonId.South).Should().Be(XboxButton.A);
    }

    [Fact]
    public void Remap_ManyToOne_CombinesIntoOneTarget()
    {
        var remapper = ButtonRemapper.Empty
            .With(ButtonId.Touchpad, XboxButton.Back)
            .With(ButtonId.Back, XboxButton.Back);

        remapper.Remap(ButtonId.Touchpad).Should().Be(XboxButton.Back);
        remapper.Remap(ButtonId.Back).Should().Be(XboxButton.Back);
        remapper.Remap(ButtonId.Touchpad | ButtonId.Back).Should().Be(XboxButton.Back);
    }

    [Fact]
    public void Remap_UnmappedButtons_AreDropped()
    {
        var remapper = ButtonRemapper.Empty.With(ButtonId.South, XboxButton.A);

        remapper.Remap(ButtonId.South | ButtonId.Paddle1 | ButtonId.Misc1).Should().Be(XboxButton.A);
    }

    [Fact]
    public void Remap_MultipleButtons_AreCombined()
    {
        var remapper = ButtonRemapper.Empty
            .With(ButtonId.South, XboxButton.A)
            .With(ButtonId.East, XboxButton.B);

        remapper.Remap(ButtonId.South | ButtonId.East).Should().Be(XboxButton.A | XboxButton.B);
    }

    [Fact]
    public void Remap_OneSourceCanPressSeveralTargets()
    {
        var remapper = ButtonRemapper.Empty.With(ButtonId.Misc1, XboxButton.A | XboxButton.B);

        remapper.Remap(ButtonId.Misc1).Should().Be(XboxButton.A | XboxButton.B);
    }

    [Fact]
    public void With_DoesNotChangeTheOriginal()
    {
        var original = ButtonRemapper.Empty;

        _ = original.With(ButtonId.South, XboxButton.A);

        original[ButtonId.South].Should().Be(XboxButton.None);
    }

    [Fact]
    public void Create_BuildsFromEntries()
    {
        var remapper = ButtonRemapper.Create(new Dictionary<ButtonId, XboxButton>
        {
            [ButtonId.North] = XboxButton.Y,
            [ButtonId.West] = XboxButton.X,
        });

        remapper[ButtonId.North].Should().Be(XboxButton.Y);
        remapper[ButtonId.West].Should().Be(XboxButton.X);
        remapper[ButtonId.South].Should().Be(XboxButton.None);
    }

    [Theory]
    [InlineData(ButtonId.None)]
    [InlineData(ButtonId.South | ButtonId.East)]
    public void With_RejectsNonSingleSource(ButtonId source)
    {
        var act = () => ButtonRemapper.Empty.With(source, XboxButton.A);

        act.Should().Throw<ArgumentException>();
    }
}
