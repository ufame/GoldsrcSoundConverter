using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GoldsrcSoundConverter.Core.Audio;

namespace GoldsrcSoundConverter.App.Controls;

public sealed class WaveformControl : FrameworkElement
{
  private const double HandleHitRadius = 8;

  private static readonly Brush BackgroundBrush = CreateFrozen(Color.FromRgb(0x10, 0x12, 0x15));
  private static readonly Brush WaveBrush = CreateFrozen(Color.FromRgb(0x4F, 0xC3, 0xF7));
  private static readonly Brush CenterLineBrush = CreateFrozen(Color.FromRgb(0x2A, 0x2E, 0x35));
  private static readonly Brush SelectionBrush = CreateFrozen(Color.FromArgb(0x33, 0x4F, 0xC3, 0xF7));
  private static readonly Brush SelectionBorderBrush = CreateFrozen(Color.FromRgb(0xFF, 0xB7, 0x4D));
  private static readonly Brush PlayheadBrush = CreateFrozen(Color.FromRgb(0xFF, 0x52, 0x52));
  private static readonly Brush BorderBrush = CreateFrozen(Color.FromRgb(0x36, 0x3D, 0x4A));
  private static readonly Brush MutedTextBrush = CreateFrozen(Color.FromRgb(0x99, 0xA1, 0xAD));

  private enum DragMode
  {
    None,
    Start,
    End,
    Move,
    Select,
  }

  private DragMode _dragMode;
  private double _dragOffset;
  private double _anchorTime;

  public static readonly DependencyProperty WaveformProperty = DependencyProperty.Register(
    nameof(Waveform),
    typeof(WaveformData),
    typeof(WaveformControl),
    new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

  public static readonly DependencyProperty DurationSecondsProperty = DependencyProperty.Register(
    nameof(DurationSeconds),
    typeof(double),
    typeof(WaveformControl),
    new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

  public static readonly DependencyProperty SelectionStartSecondsProperty = DependencyProperty.Register(
    nameof(SelectionStartSeconds),
    typeof(double),
    typeof(WaveformControl),
    new FrameworkPropertyMetadata(
      0.0,
      FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

  public static readonly DependencyProperty SelectionEndSecondsProperty = DependencyProperty.Register(
    nameof(SelectionEndSeconds),
    typeof(double),
    typeof(WaveformControl),
    new FrameworkPropertyMetadata(
      0.0,
      FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

  public static readonly DependencyProperty PositionSecondsProperty = DependencyProperty.Register(
    nameof(PositionSeconds),
    typeof(double),
    typeof(WaveformControl),
    new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

  public WaveformData? Waveform
  {
    get => (WaveformData?)GetValue(WaveformProperty);
    set => SetValue(WaveformProperty, value);
  }

  public double DurationSeconds
  {
    get => (double)GetValue(DurationSecondsProperty);
    set => SetValue(DurationSecondsProperty, value);
  }

  public double SelectionStartSeconds
  {
    get => (double)GetValue(SelectionStartSecondsProperty);
    set => SetValue(SelectionStartSecondsProperty, value);
  }

  public double SelectionEndSeconds
  {
    get => (double)GetValue(SelectionEndSecondsProperty);
    set => SetValue(SelectionEndSecondsProperty, value);
  }

  public double PositionSeconds
  {
    get => (double)GetValue(PositionSecondsProperty);
    set => SetValue(PositionSecondsProperty, value);
  }

  private double EffectiveDuration => DurationSeconds > 0
    ? DurationSeconds
    : Waveform?.Duration.TotalSeconds ?? 0;

  protected override void OnRender(DrawingContext drawingContext)
  {
    var width = ActualWidth;
    var height = ActualHeight;
    if (width <= 1 || height <= 1)
    {
      return;
    }

    drawingContext.DrawRectangle(BackgroundBrush, new Pen(BorderBrush, 1), new Rect(0, 0, width, height));

    var duration = EffectiveDuration;
    if (Waveform is null || duration <= 0)
    {
      DrawPlaceholder(drawingContext, width, height);
      return;
    }

    var middle = height / 2;
    drawingContext.DrawLine(new Pen(CenterLineBrush, 1), new Point(0, middle), new Point(width, middle));

    DrawWaveform(drawingContext, width, height);
    DrawSelection(drawingContext, width, height, duration);
    DrawPlayhead(drawingContext, width, height, duration);
  }

  protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
  {
    base.OnMouseLeftButtonDown(e);

    var duration = EffectiveDuration;
    if (Waveform is null || duration <= 0)
    {
      return;
    }

    var x = e.GetPosition(this).X;
    var time = TimeAt(x, duration);

    if (e.ClickCount == 2)
    {
      SelectionStartSeconds = 0;
      SelectionEndSeconds = duration;
      InvalidateVisual();
      return;
    }

    var startX = TimeToX(SelectionStartSeconds, duration);
    var endX = TimeToX(SelectionEndSeconds, duration);
    var hasSelection = SelectionEndSeconds - SelectionStartSeconds > 0.0005;

    if (Math.Abs(x - startX) <= HandleHitRadius)
    {
      _dragMode = DragMode.Start;
    }
    else if (Math.Abs(x - endX) <= HandleHitRadius)
    {
      _dragMode = DragMode.End;
    }
    else if (hasSelection && time > SelectionStartSeconds && time < SelectionEndSeconds)
    {
      _dragMode = DragMode.Move;
      _dragOffset = time - SelectionStartSeconds;
    }
    else
    {
      _dragMode = DragMode.Select;
      _anchorTime = time;
      SelectionStartSeconds = time;
      SelectionEndSeconds = time;
    }

    CaptureMouse();
    e.Handled = true;
  }

  protected override void OnMouseMove(MouseEventArgs e)
  {
    base.OnMouseMove(e);

    var duration = EffectiveDuration;
    if (!IsMouseCaptured || duration <= 0)
    {
      return;
    }

    var time = TimeAt(e.GetPosition(this).X, duration);

    switch (_dragMode)
    {
      case DragMode.Start:
        SelectionStartSeconds = Math.Min(time, SelectionEndSeconds);
        break;
      case DragMode.End:
        SelectionEndSeconds = Math.Max(time, SelectionStartSeconds);
        break;
      case DragMode.Move:
      {
        var length = SelectionEndSeconds - SelectionStartSeconds;
        var newStart = Math.Clamp(time - _dragOffset, 0, Math.Max(0, duration - length));
        SelectionStartSeconds = newStart;
        SelectionEndSeconds = newStart + length;
        break;
      }
      case DragMode.Select:
        if (time < _anchorTime)
        {
          SelectionStartSeconds = time;
          SelectionEndSeconds = _anchorTime;
        }
        else
        {
          SelectionStartSeconds = _anchorTime;
          SelectionEndSeconds = time;
        }

        break;
    }

    InvalidateVisual();
  }

  protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
  {
    base.OnMouseLeftButtonUp(e);
    _dragMode = DragMode.None;
    ReleaseMouseCapture();
    InvalidateVisual();
  }

  private void DrawWaveform(DrawingContext drawingContext, double width, double height)
  {
    var waveform = Waveform!;
    var duration = EffectiveDuration;
    var middle = height / 2;
    var half = Math.Max(1, (height - 10) / 2);
    var pen = new Pen(WaveBrush, 1);
    pen.Freeze();

    var columns = (int)Math.Max(1, width);
    var secondsPerColumn = duration / columns;

    for (var column = 0; column < columns; column++)
    {
      var startBucket = (int)(column * secondsPerColumn / duration * waveform.BucketCount);
      var endBucket = (int)((column + 1) * secondsPerColumn / duration * waveform.BucketCount) + 1;
      var (min, max) = waveform.Aggregate(startBucket, endBucket);

      var top = middle - max * half;
      var bottom = middle - min * half;
      if (bottom - top < 1)
      {
        bottom = top + 1;
      }

      drawingContext.DrawLine(pen, new Point(column + 0.5, top), new Point(column + 0.5, bottom));
    }
  }

  private void DrawSelection(DrawingContext drawingContext, double width, double height, double duration)
  {
    if (SelectionEndSeconds - SelectionStartSeconds <= 0.0005)
    {
      return;
    }

    var startX = Math.Clamp(TimeToX(SelectionStartSeconds, duration), 0, width);
    var endX = Math.Clamp(TimeToX(SelectionEndSeconds, duration), 0, width);
    if (endX - startX < 0.5)
    {
      return;
    }

    drawingContext.DrawRectangle(SelectionBrush, null, new Rect(startX, 1, endX - startX, height - 2));
    var borderPen = new Pen(SelectionBorderBrush, 2);
    borderPen.Freeze();

    if (startX > 0.5)
    {
      drawingContext.DrawLine(borderPen, new Point(startX, 1), new Point(startX, height - 1));
    }

    if (endX < width - 0.5)
    {
      drawingContext.DrawLine(borderPen, new Point(endX, 1), new Point(endX, height - 1));
    }
  }

  private void DrawPlayhead(DrawingContext drawingContext, double width, double height, double duration)
  {
    if (PositionSeconds <= 0 && duration <= 0)
    {
      return;
    }

    var x = Math.Clamp(TimeToX(PositionSeconds, duration), 0, width);
    var pen = new Pen(PlayheadBrush, 2);
    pen.Freeze();
    drawingContext.DrawLine(pen, new Point(x, 1), new Point(x, height - 1));
  }

  private void DrawPlaceholder(DrawingContext drawingContext, double width, double height)
  {
    var text = new FormattedText(
      "Нет данных волновой формы",
      CultureInfo.CurrentUICulture,
      FlowDirection.LeftToRight,
      new Typeface("Segoe UI"),
      12,
      MutedTextBrush,
      VisualTreeHelper.GetDpi(this).PixelsPerDip);

    drawingContext.DrawText(
      text,
      new Point((width - text.Width) / 2, (height - text.Height) / 2));
  }

  private double TimeAt(double x, double duration)
  {
    return ActualWidth <= 0 ? 0 : Math.Clamp(x / ActualWidth * duration, 0, duration);
  }

  private double TimeToX(double seconds, double duration)
  {
    return duration <= 0 ? 0 : seconds / duration * ActualWidth;
  }

  private static SolidColorBrush CreateFrozen(Color color)
  {
    var brush = new SolidColorBrush(color);
    brush.Freeze();
    return brush;
  }
}
