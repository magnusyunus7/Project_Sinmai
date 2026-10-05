using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace Project_Sinmai;

public sealed class Tr : MarkupExtension
{
	public string Key { get; set; } = "";

	public Tr()
	{
	}

	public Tr(string key)
	{
		Key = key;
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		return new Binding("[" + Key + "]")
		{
			Source = Loc.Instance,
			Mode = BindingMode.OneWay
		}.ProvideValue(serviceProvider);
	}
}
