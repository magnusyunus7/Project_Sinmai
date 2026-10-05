using System;
using System.Media;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Project_Sinmai;

public partial class ToastWindow : Window, IComponentConnector
{
	private readonly DispatcherTimer _closeTimer = new DispatcherTimer();

	private ToastWindow(string title, string message)
	{
		InitializeComponent();
		TxtTitle.Text = title;
		TxtMessage.Text = message;
		_closeTimer.Interval = TimeSpan.FromSeconds(6L);
		_closeTimer.Tick += delegate
		{
			Close();
		};
		base.MouseDown += delegate
		{
			Close();
		};
	}

	public static void ShowAtBottomRight(string title, string message)
	{
		try
		{
			SystemSounds.Asterisk.Play();
			ToastWindow toastWindow = new ToastWindow(title, message)
			{
				Opacity = 0.0
			};
			toastWindow.Show();
			toastWindow.UpdateLayout();
			Rect workArea = SystemParameters.WorkArea;
			toastWindow.Left = workArea.Right - toastWindow.ActualWidth - 12.0;
			toastWindow.Top = workArea.Bottom - toastWindow.ActualHeight - 12.0;
			toastWindow.Opacity = 1.0;
			toastWindow._closeTimer.Start();
		}
		catch
		{
		}
	}
}
