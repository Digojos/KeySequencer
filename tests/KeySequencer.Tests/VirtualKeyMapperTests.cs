using KeySequencer.Core.Services;

namespace KeySequencer.Tests;

public class VirtualKeyMapperTests
{
    [Theory]
    [InlineData("Q", 0x51)]
    [InlineData("SPACE", 0x20)]
    [InlineData("F5", 0x74)]
    [InlineData("1", 0x31)]
    public void TryResolve_KnownKeys_ReturnsExpectedVirtualKey(string key, ushort expectedVk)
    {
        var resolved = VirtualKeyMapper.TryResolve(key, out var vk, out _);

        Assert.True(resolved);
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void TryResolve_BlankKey_ReturnsFalse()
    {
        var resolved = VirtualKeyMapper.TryResolve("   ", out _, out _);

        Assert.False(resolved);
    }

    [Theory]
    [InlineData("Q")]
    [InlineData("W")]
    [InlineData("1")]
    public void TryResolve_LettersAndDigits_DoNotRequireShift(string key)
    {
        // Slot/sequence keys are always normalized to uppercase before reaching this method; a
        // letter's virtual-key code doesn't depend on case, so this must not report a Shift
        // requirement just because the uppercase form "looks" shifted.
        var resolved = VirtualKeyMapper.TryResolve(key, out _, out var requiresShift);

        Assert.True(resolved);
        Assert.False(requiresShift);
    }
}
