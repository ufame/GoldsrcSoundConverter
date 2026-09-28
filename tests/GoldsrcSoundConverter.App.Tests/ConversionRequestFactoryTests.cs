using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class ConversionRequestFactoryTests
{
  private static ConversionSettings Settings()
  {
    return new ConversionSettings(
      OutputAudioFormat.Mp3,
      44100,
      TargetChannels.Stereo,
      TargetBitDepth.Sixteen,
      192,
      NormalizePeak: true,
      AsciiNames: false,
      LowercaseNames: false,
      PreserveStructure: true,
      CollisionPolicy.Skip,
      Parallelism: 3,
      OutputDirectory: @"C:\out");
  }

  [Fact]
  public void CreateOptionsMapsEveryField()
  {
    var options = new ConversionRequestFactory().CreateOptions(Settings());

    Assert.Equal(OutputAudioFormat.Mp3, options.Format);
    Assert.Equal(44100, options.SampleRate);
    Assert.Equal(TargetChannels.Stereo, options.Channels);
    Assert.Equal(TargetBitDepth.Sixteen, options.BitDepth);
    Assert.Equal(192, options.Mp3BitrateKbps);
    Assert.True(options.NormalizePeak);
    Assert.False(options.AsciiNames);
    Assert.False(options.LowercaseNames);
    Assert.True(options.PreserveStructure);
    Assert.Equal(CollisionPolicy.Skip, options.CollisionPolicy);
    Assert.Equal(3, options.Parallelism);
    Assert.Equal(@"C:\out", options.OutputDirectory);
  }

  [Fact]
  public void CreatePreviewOptionsOverridesOutputBehaviour()
  {
    var options = new ConversionRequestFactory().CreatePreviewOptions(Settings(), @"C:\temp");

    Assert.Equal(@"C:\temp", options.OutputDirectory);
    Assert.False(options.AsciiNames);
    Assert.False(options.LowercaseNames);
    Assert.Equal(CollisionPolicy.Overwrite, options.CollisionPolicy);
    Assert.Equal(1, options.Parallelism);
    Assert.Equal(44100, options.SampleRate);
  }

  [Fact]
  public void CreateWorkItemMapsItemState()
  {
    var item = new QueueItemViewModel(@"C:\in\clip.ogg", @"C:\in")
    {
      TrimStartSeconds = 1,
      TrimEndSeconds = 4,
    };
    item.Info = new AudioInfo(item.SourcePath, "ogg", "vorbis", TimeSpan.FromSeconds(5), 44100, 2, 128000, 10);

    var workItem = new ConversionRequestFactory().CreateWorkItem(item);

    Assert.Equal(item.Id, workItem.Id);
    Assert.Equal(item.SourcePath, workItem.SourcePath);
    Assert.Equal(item.SourceRoot, workItem.SourceRoot);
    Assert.Equal(TimeSpan.FromSeconds(1), workItem.TrimStart);
    Assert.Equal(TimeSpan.FromSeconds(4), workItem.TrimEnd);
    Assert.Same(item.Info, workItem.KnownInfo);
  }

  [Fact]
  public void CreateWorkItemsSkipsTrimsWhenNotSet()
  {
    var item = new QueueItemViewModel(@"C:\in\clip.wav", null);

    var workItems = new ConversionRequestFactory().CreateWorkItems(new[] { item });

    var workItem = Assert.Single(workItems);
    Assert.Null(workItem.TrimStart);
    Assert.Null(workItem.TrimEnd);
  }
}
