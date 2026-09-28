namespace GoldsrcSoundConverter.App.Services;

public sealed class ActionProgress<T> : IProgress<T>
{
  private readonly Action<T> _action;

  public ActionProgress(Action<T> action)
  {
    _action = action;
  }

  public void Report(T value)
  {
    _action(value);
  }
}
