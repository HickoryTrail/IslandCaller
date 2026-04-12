using Avalonia.Controls;
using Avalonia;
using ClassIsland.Shared;
using IslandCaller.Services.IslandCallerService;
using System.ComponentModel;

namespace IslandCaller.Views;

public partial class PersonalCall : Window,INotifyPropertyChanged
{
    public double Num { get; set; }
    private IslandCallerService IslandCallerService { get; }
    private const int OwnerGapPx = 12;
    public PersonalCall()
    {
        IslandCallerService = IAppHost.GetService<IslandCallerService>();   
        InitializeComponent();
        DataContext = this;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Owner == null)
        {
            var screen = Screens.Primary; // 主屏幕
            if (screen is null) return;

            var workArea = screen.WorkingArea;

            Position = new PixelPoint(
                (int)(workArea.X + (workArea.Width - Width) / 2),
                (int)(workArea.Y + (workArea.Height - Height) / 2));
            return;
        }

        PositionNearOwner(Owner);
    }
    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close();
    }
    private void SureButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IslandCallerService.ShowRandomStudent((int)Num);
        this.Close();
    }

    private void PositionNearOwner(WindowBase owner)
    {
        if (owner is not Window ownerWindow)
        {
            return;
        }

        var screen = owner.Screens.ScreenFromWindow(ownerWindow) ?? owner.Screens.Primary;
        var screenBounds = screen.Bounds;

        var ownerScaling = owner.RenderScaling;
        var thisScaling = RenderScaling;

        var ownerLeft = ownerWindow.Position.X;
        var ownerTop = ownerWindow.Position.Y;
        var ownerWidth = (int)Math.Round(owner.Bounds.Width * ownerScaling);
        var ownerHeight = (int)Math.Round(owner.Bounds.Height * ownerScaling);
        var ownerRight = ownerLeft + ownerWidth;
        var ownerBottom = ownerTop + ownerHeight;
        var ownerCenterX = ownerLeft + ownerWidth / 2.0;

        var thisWidth = (int)Math.Round(Bounds.Width * thisScaling);
        var thisHeight = (int)Math.Round(Bounds.Height * thisScaling);

        if (thisWidth <= 0) thisWidth = (int)Math.Round(Width * thisScaling);
        if (thisHeight <= 0) thisHeight = (int)Math.Round(Height * thisScaling);

        var thirdWidth = screenBounds.Width / 3.0;
        var align = ownerCenterX < screenBounds.X + thirdWidth
            ? "left"
            : ownerCenterX > screenBounds.X + 2 * thirdWidth
                ? "right"
                : "center";

        int targetX = align switch
        {
            "left" => ownerLeft,
            "right" => ownerRight - thisWidth,
            _ => (int)Math.Round(ownerCenterX - thisWidth / 2.0)
        };

        int targetY = ownerTop - OwnerGapPx - thisHeight;
        if (targetY < screenBounds.Y)
        {
            targetY = ownerBottom + OwnerGapPx;
        }

        targetX = Clamp(targetX, screenBounds.X, screenBounds.X + screenBounds.Width - thisWidth);
        targetY = Clamp(targetY, screenBounds.Y, screenBounds.Y + screenBounds.Height - thisHeight);

        Position = new PixelPoint(targetX, targetY);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
