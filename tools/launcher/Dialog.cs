using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HordesOfAzeroth.Launcher
{
    static class Dialog
    {
        const double Width = 460;

        public static Task<bool> AskAsync(Window owner, string question) => ShowAsync(owner, question, "Yes", "No");

        public static Task<bool> TellAsync(Window owner, string message) => ShowAsync(owner, message, "OK", null);

        static async Task<bool> ShowAsync(Window owner, string text, string confirm, string decline)
        {
            bool confirmed = false;
            Window window = new Window
            {
                Title = "Hordes of Azeroth",
                Width = Width,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Icon = owner.Icon,
                Background = (IBrush)Application.Current.FindResource("Bar"),
                Foreground = (IBrush)Application.Current.FindResource("Text"),
            };
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
            Button yes = DialogButton(confirm, true);
            yes.Click += (s, e) =>
            {
                confirmed = true;
                window.Close();
            };
            buttons.Children.Add(yes);
            if (decline != null)
            {
                Button no = DialogButton(decline, false);
                no.Click += (s, e) => window.Close();
                buttons.Children.Add(no);
            }
            window.Content = new StackPanel
            {
                Margin = new Thickness(26, 22, 26, 22),
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = text, FontSize = 13, LineHeight = 19, TextWrapping = TextWrapping.Wrap },
                    buttons,
                },
            };
            if (owner.IsVisible && owner.WindowState != WindowState.Minimized)
            {
                await window.ShowDialog(owner);
            }
            else
            {
                TaskCompletionSource<bool> closed = new TaskCompletionSource<bool>();
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Closed += (s, e) => closed.TrySetResult(true);
                window.Show();
                await closed.Task;
            }
            return confirmed;
        }

        static Button DialogButton(string text, bool primary)
        {
            Button button = new Button
            {
                Content = text,
                Width = 110,
                Height = 36,
                FontSize = 14,
                IsDefault = primary,
                IsCancel = !primary,
            };
            button.Theme = (Avalonia.Styling.ControlTheme)Application.Current.FindResource(primary ? "PrimaryButton" : "MoreButton");
            return button;
        }
    }
}
