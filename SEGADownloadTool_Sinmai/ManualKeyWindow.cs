using System;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace SEGADownloadTool_Sinmai;

public partial class ManualKeyWindow : Window, IComponentConnector
{
	public byte[] KeyBytes { get; private set; } = Array.Empty<byte>();

	public byte[] IvBytes { get; private set; } = Array.Empty<byte>();

	public ManualKeyWindow()
	{
		InitializeComponent();
	}

	public ManualKeyWindow(string gameId)
		: this()
	{
		TxtId.Text = gameId;
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (TxtId.Text.Trim().Length == 0)
		{
			Show(Word.T("rune.needId"));
			return;
		}
		if (!TryHex(TxtKey.Text, 16, out byte[] data))
		{
			Show(Word.T("rune.badKey"));
			return;
		}
		if (!TryHex(TxtIv.Text, 16, out byte[] data2))
		{
			Show(Word.T("rune.badIv"));
			return;
		}
		KeyBytes = data;
		IvBytes = data2;
		base.DialogResult = true;
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}

	private static bool TryHex(string text, int bytes, out byte[] data)
	{
		data = Array.Empty<byte>();
		string text2 = string.Concat(text.Where((char c) => !char.IsWhiteSpace(c)));
		if (text2.Length != bytes * 2)
		{
			return false;
		}
		try
		{
			data = Convert.FromHexString(text2);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	private void Show(string message)
	{
		MessageBox.Show(message, Word.T("common.tip"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}
}
