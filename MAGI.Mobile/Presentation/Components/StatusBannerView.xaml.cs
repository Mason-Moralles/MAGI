namespace MAGI.Mobile.Presentation.Components;

public partial class StatusBannerView : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(StatusBannerView), string.Empty);

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message), typeof(string), typeof(StatusBannerView), string.Empty);

    public static readonly BindableProperty BannerBackgroundProperty = BindableProperty.Create(
        nameof(BannerBackground), typeof(Color), typeof(StatusBannerView), Colors.Transparent);

    public StatusBannerView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public Color BannerBackground
    {
        get => (Color)GetValue(BannerBackgroundProperty);
        set => SetValue(BannerBackgroundProperty, value);
    }
}