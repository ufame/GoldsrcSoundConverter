using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GoldsrcSoundConverter.App.ViewModels;

namespace GoldsrcSoundConverter.App;

public partial class MainWindow : Window
{
  private const int DwmwaUseImmersiveDarkMode = 20;
  private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

  private readonly MainViewModel _viewModel;

  public MainWindow(MainViewModel viewModel)
  {
    InitializeComponent();
    _viewModel = viewModel;
    DataContext = _viewModel;
    Width = _viewModel.Settings.InitialWindowWidth;
    Height = _viewModel.Settings.InitialWindowHeight;
    _viewModel.Log.Entries.CollectionChanged += OnLogEntriesChanged;
  }

  protected override void OnSourceInitialized(EventArgs e)
  {
    base.OnSourceInitialized(e);
    EnableDarkTitleBar();
  }

  private void EnableDarkTitleBar()
  {
    try
    {
      var handle = new WindowInteropHelper(this).Handle;
      if (handle == IntPtr.Zero)
      {
        return;
      }

      var enabled = 1;
      if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
      {
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
      }
    }
    catch (DllNotFoundException)
    {
    }
    catch (EntryPointNotFoundException)
    {
    }
  }

  [DllImport("dwmapi.dll", PreserveSig = true)]
  private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

  private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
    {
      LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
    }
  }

  private void OnWindowDragOver(object sender, DragEventArgs e)
  {
    e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
      ? DragDropEffects.Copy
      : DragDropEffects.None;
    e.Handled = true;
  }

  private void OnWindowDrop(object sender, DragEventArgs e)
  {
    if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
    {
      _viewModel.Queue.AddPaths(paths);
    }
  }

  private void OnWindowClosing(object? sender, CancelEventArgs e)
  {
    _viewModel.Settings.SaveWithWindow(Width, Height);
    _viewModel.Dispose();
  }
}
