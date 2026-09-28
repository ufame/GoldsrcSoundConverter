using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Files;

public interface IConversionPlanner
{
  IReadOnlyList<ConversionJob> Plan(IReadOnlyList<ConversionJob> jobs, ConversionOptions options);
}

public sealed class ConversionPlanner : IConversionPlanner
{
  public IReadOnlyList<ConversionJob> Plan(IReadOnlyList<ConversionJob> jobs, ConversionOptions options)
  {
    var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var planned = new List<ConversionJob>(jobs.Count);

    foreach (var job in jobs)
    {
      var outputPath = OutputPathResolver.Resolve(job.SourcePath, job.SourceRoot, options, reserved);
      planned.Add(job with { OutputPath = outputPath });

      if (outputPath is not null)
      {
        reserved.Add(Path.GetFullPath(outputPath));
      }
    }

    return planned;
  }
}
