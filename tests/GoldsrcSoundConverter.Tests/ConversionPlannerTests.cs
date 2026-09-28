using GoldsrcSoundConverter.Core.Files;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class ConversionPlannerTests : IDisposable
{
  private readonly TempDirectory _temp = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  private ConversionOptions Options => new()
  {
    OutputDirectory = _temp.Path,
    AsciiNames = true,
    LowercaseNames = true,
    CollisionPolicy = CollisionPolicy.Rename,
  };

  [Fact]
  public void ReservesUniquePathsForIdenticalNamesFromDifferentFolders()
  {
    var first = Path.Combine(_temp.Path, "a", "shot.wav");
    var second = Path.Combine(_temp.Path, "b", "shot.wav");
    Directory.CreateDirectory(Path.GetDirectoryName(first)!);
    Directory.CreateDirectory(Path.GetDirectoryName(second)!);

    var planned = new ConversionPlanner().Plan(
      new[]
      {
        new ConversionJob(Guid.NewGuid(), first, null, null, null),
        new ConversionJob(Guid.NewGuid(), second, null, null, null),
      },
      Options);

    Assert.Equal(Path.Combine(_temp.Path, "shot.wav"), planned[0].OutputPath);
    Assert.Equal(Path.Combine(_temp.Path, "shot_1.wav"), planned[1].OutputPath);
  }

  [Fact]
  public void ReservesUniquePathsForSanitizedCollision()
  {
    var jobs = new[]
    {
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "test!.wav"), null, null, null),
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "test?.wav"), null, null, null),
    };

    var planned = new ConversionPlanner().Plan(jobs, Options);

    Assert.Equal(Path.Combine(_temp.Path, "test.wav"), planned[0].OutputPath);
    Assert.Equal(Path.Combine(_temp.Path, "test_1.wav"), planned[1].OutputPath);
  }

  [Fact]
  public void RenamesWhenTargetExistsOnDisk()
  {
    File.WriteAllText(Path.Combine(_temp.Path, "sound.wav"), "x");
    var jobs = new[] { new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "sound.ogg"), null, null, null) };

    var planned = new ConversionPlanner().Plan(jobs, Options);

    Assert.Equal(Path.Combine(_temp.Path, "sound_1.wav"), planned[0].OutputPath);
  }

  [Fact]
  public void SkipPolicyReturnsNullForExistingAndReservedTargets()
  {
    File.WriteAllText(Path.Combine(_temp.Path, "dup.wav"), "x");
    var options = Options;
    options.CollisionPolicy = CollisionPolicy.Skip;
    var jobs = new[]
    {
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "dup!.wav"), null, null, null),
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "dup?.wav"), null, null, null),
    };

    var planned = new ConversionPlanner().Plan(jobs, options);

    Assert.Null(planned[0].OutputPath);
    Assert.Null(planned[1].OutputPath);
  }

  [Fact]
  public void SkipPolicyReturnsNullWhenReservedByEarlierJob()
  {
    var options = Options;
    options.CollisionPolicy = CollisionPolicy.Skip;
    var jobs = new[]
    {
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "dup!.wav"), null, null, null),
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "dup?.wav"), null, null, null),
    };

    var planned = new ConversionPlanner().Plan(jobs, options);

    Assert.Equal(Path.Combine(_temp.Path, "dup.wav"), planned[0].OutputPath);
    Assert.Null(planned[1].OutputPath);
  }

  [Fact]
  public void OverwritePolicyKeepsExistingTargetButRenamesIntraBatchCollision()
  {
    File.WriteAllText(Path.Combine(_temp.Path, "same.wav"), "x");
    var options = Options;
    options.CollisionPolicy = CollisionPolicy.Overwrite;
    var jobs = new[]
    {
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "same!.wav"), null, null, null),
      new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "same?.wav"), null, null, null),
    };

    var planned = new ConversionPlanner().Plan(jobs, options);

    Assert.Equal(Path.Combine(_temp.Path, "same.wav"), planned[0].OutputPath);
    Assert.Equal(Path.Combine(_temp.Path, "same_1.wav"), planned[1].OutputPath);
  }

  [Fact]
  public void SameNameInDifferentSubdirectoriesStaysSeparate()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    var first = Path.Combine(root, "a", "shot.wav");
    var second = Path.Combine(root, "b", "shot.wav");
    Directory.CreateDirectory(Path.GetDirectoryName(first)!);
    Directory.CreateDirectory(Path.GetDirectoryName(second)!);
    var output = Path.Combine(_temp.Path, "out");
    var options = Options;
    options.PreserveStructure = true;
    options.OutputDirectory = output;

    var planned = new ConversionPlanner().Plan(
      new[]
      {
        new ConversionJob(Guid.NewGuid(), first, root, null, null),
        new ConversionJob(Guid.NewGuid(), second, root, null, null),
      },
      options);

    Assert.Equal(Path.Combine(output, "a", "shot.wav"), planned[0].OutputPath);
    Assert.Equal(Path.Combine(output, "b", "shot.wav"), planned[1].OutputPath);
  }

  [Fact]
  public void DoesNotMutateInputJobsAndKeepsStableId()
  {
    var input = new ConversionJob(Guid.NewGuid(), Path.Combine(_temp.Path, "voice.ogg"), null, null, null);

    var planned = new ConversionPlanner().Plan(new[] { input }, Options);

    Assert.Null(input.OutputPath);
    Assert.NotNull(planned[0].OutputPath);
    Assert.Equal(input.Id, planned[0].Id);
  }
}
