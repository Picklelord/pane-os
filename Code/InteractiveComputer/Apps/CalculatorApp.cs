using System;
using Sandbox.UI;

namespace PaneOS.InteractiveComputer.Apps;

[ComputerApp( "system.calculator", "Calculator", Icon = "CA", SortOrder = 22 )]
public sealed class CalculatorApp : IComputerApp
{
	public ComputerAppSession Run( ComputerAppContext context )
	{
		return new ComputerAppSession
		{
			Title = "Calculator",
			Icon = "CA",
			Content = new CalculatorPanel()
		};
	}
}

[StyleSheet( "InteractiveComputerApps.scss" )]
public sealed class CalculatorPanel : ComputerWarmupPanel
{
	private Label display = null!;
	private float accumulator;
	private string pendingOperator = "";
	private bool resetOnNextDigit;

	public CalculatorPanel()
	{
		AddClass( "calculator-app" );
		BuildUi();
	}

	protected override void WarmupRefresh()
	{
		BuildUi();
	}

	private void BuildUi()
	{
		var displayText = display?.Text ?? "0";
		DeleteChildren( true );

		display = new Label( displayText ) { Parent = this };
		display.AddClass( "calculator-display" );

		var grid = new Panel { Parent = this };
		grid.AddClass( "calculator-grid" );

		var rows = new[]
		{
			new[] { "C", "+/-", "%", "/" },
			new[] { "7", "8", "9", "*" },
			new[] { "4", "5", "6", "-" },
			new[] { "1", "2", "3", "+" },
			new[] { "0", ".", "=", "<-" }
		};

		foreach ( var rowKeys in rows )
		{
			var row = new Panel { Parent = grid };
			row.AddClass( "calculator-row" );
			foreach ( var key in rowKeys )
			{
				var button = new Button( key ) { Parent = row };
				button.AddClass( "calculator-button" );
				button.AddEventListener( "onclick", () => OnKey( key ) );
			}
		}
	}

	private void OnKey( string key )
	{
		if ( key == "C" )
		{
			Clear();
			return;
		}

		if ( key == "<-" )
		{
			display.Text = display.Text.Length <= 1 ? "0" : display.Text[..^1];
			return;
		}

		if ( key == "+/-" )
		{
			display.Text = display.Text.StartsWith( "-", StringComparison.Ordinal )
				? display.Text[1..]
				: $"-{display.Text}";
			return;
		}

		if ( key == "%" )
		{
			if ( float.TryParse( display.Text, out var percentValue ) )
				display.Text = (percentValue / 100f).ToString( "0.###" );
			return;
		}

		if ( key is "+" or "-" or "*" or "/" )
		{
			CommitPendingOperation();
			pendingOperator = key;
			resetOnNextDigit = true;
			return;
		}

		if ( key == "=" )
		{
			CommitPendingOperation();
			pendingOperator = "";
			resetOnNextDigit = true;
			return;
		}

		if ( resetOnNextDigit || display.Text == "0" )
			display.Text = key == "." ? "0." : key;
		else if ( key != "." || !display.Text.Contains( "." ) )
			display.Text += key;

		resetOnNextDigit = false;
	}

	private void CommitPendingOperation()
	{
		if ( !float.TryParse( display.Text, out var value ) )
			value = 0f;

		if ( string.IsNullOrWhiteSpace( pendingOperator ) )
		{
			accumulator = value;
			return;
		}

		accumulator = pendingOperator switch
		{
			"+" => accumulator + value,
			"-" => accumulator - value,
			"*" => accumulator * value,
			"/" => value == 0f ? 0f : accumulator / value,
			_ => value
		};

		display.Text = accumulator.ToString( "0.###" );
	}

	private void Clear()
	{
		accumulator = 0f;
		pendingOperator = "";
		resetOnNextDigit = false;
		display.Text = "0";
	}
}
