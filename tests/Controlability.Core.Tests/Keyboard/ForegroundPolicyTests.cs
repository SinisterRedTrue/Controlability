using Controlability.Core.Keyboard;

namespace Controlability.Core.Tests.Keyboard;

public class ForegroundPolicyTests
{
    private static readonly string[] Game = ["MinecraftDungeons"];

    [Fact]
    public void With_no_targets_nothing_is_ever_blocked_but_the_pad_always_listens()
    {
        ForegroundPolicy.ShouldBlock([], "notepad").Should().BeFalse();
        ForegroundPolicy.ShouldBlock([], null).Should().BeFalse();
        ForegroundPolicy.ShouldListen([], "notepad").Should().BeTrue();
    }

    [Fact]
    public void A_target_game_in_the_foreground_blocks_and_listens()
    {
        ForegroundPolicy.ShouldBlock(Game, "MinecraftDungeons").Should().BeTrue();
        ForegroundPolicy.ShouldListen(Game, "minecraftdungeons").Should().BeTrue();
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("cmd")]
    [InlineData("explorer")]
    [InlineData(null)]
    [InlineData("")]
    public void Any_other_window_is_left_alone(string? foreground)
    {
        ForegroundPolicy.ShouldBlock(Game, foreground).Should().BeFalse();
        ForegroundPolicy.ShouldListen(Game, foreground).Should().BeFalse();
    }

    [Theory]
    [InlineData("game.exe", "game")]
    [InlineData("game", "GAME.EXE")]
    public void The_exe_suffix_and_case_do_not_matter(string target, string foreground)
    {
        ForegroundPolicy.ShouldBlock([target], foreground).Should().BeTrue();
    }
}
