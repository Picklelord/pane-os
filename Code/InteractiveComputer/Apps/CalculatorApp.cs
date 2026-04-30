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

		foreach ( var key in new[] { "7", "8", "9", "/", "4", "5", "6", "*", "1", "2", "3", "-", "0", ".", "=", "+" } )
		{
			var button = new Button( key ) { Parent = grid };
			button.AddClass( "calculator-button" );
			button.AddEventListener( "onclick", () => OnKey( key ) );
		}

		var footer = new Panel { Parent = this };
		footer.AddClass( "calculator-footer" );
		var clearButton = new Button( "C" ) { Parent = footer };
		clearButton.AddClass( "calculator-clear" );
		clearButton.AddEventListener( "onclick", Clear );
	}

	private void OnKey( string key )
	{
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
