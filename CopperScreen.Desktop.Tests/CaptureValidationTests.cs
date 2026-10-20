#if COPPERSCREEN_INSTALLED_TESTS
using Avalonia;
using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

[Collection("Desktop UI")]
public sealed class CaptureValidationTests(LaunchUiFixture ui)
{
    [Theory]
    [InlineData(2962,2962,true)]
    [InlineData(2962,0,false)]
    [InlineData(0,2962,false)]
    [InlineData(0,0,false)]
    [InlineData(99,2962,false)]
    public void MissingProofTextCannotBeAccepted(int presenter, int capture, bool accepted)
        => Assert.Equal(accepted, CopperScreenInstalledCapture.HasProofText(presenter,capture));

    [Fact]
    public void TransparentWhiteAndDarkBlueAreNotVisibleProofText()
        => Assert.Equal(1, CopperScreenInstalledCapture.CountNeutralTextPixels(
            [0x00ffffff, unchecked((int)0xff001020), unchecked((int)0xffeeeeee)]));

    [Fact]
    public Task CapturePreservesAllThreePresenterSlotsAndRetainsRejectedBlank() => ui.Session.Dispatch(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "copperscreen-capture-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var presenter = new FramebufferPresenter(128,64);
        var window = new Window { Width=256, Height=128, Content=presenter };
        window.Show(); window.UpdateLayout();
        try
        {
            var pixels = Enumerable.Repeat(unchecked((int)0xff001020), 128*64).ToArray();
            for(var i=0;i<6;i++)
            {
                // Two rotations of all three bitmaps; the bright rectangle moves.
                Array.Fill(pixels, unchecked((int)0xff001020));
                for(var y=8;y<24;y++) for(var x=8+i*8;x<24+i*8;x++) pixels[y*128+x]=unchecked((int)0xffeeeeee);
                presenter.Update(pixels);
                var result = CopperScreenInstalledCapture.Save(window,presenter,window.ClientSize,1,
                    Path.Combine(directory,$"frame-{i}.png"),true,i);
                Assert.True(result.Passed);
                Assert.Equal(256,result.PresenterTextPixels);
                Assert.True(result.CaptureTextPixels >= 256);
                Assert.Equal(CopperScreenInstalledSoak.Hash(result.Path), result.Sha256);
                Assert.True(File.Exists(result.Path+".json"));
            }
            Array.Fill(pixels,unchecked((int)0xff001020)); presenter.Update(pixels);
            var rejected = Path.Combine(directory,"blank.png");
            Assert.Throws<InvalidOperationException>(() => CopperScreenInstalledCapture.Save(window,presenter,window.ClientSize,1,rejected,true,7));
            Assert.True(File.Exists(rejected)); Assert.True(File.Exists(rejected+".json"));
        }
        finally { window.Close(); Directory.Delete(directory,true); }
    }, CancellationToken.None);
}
#endif
