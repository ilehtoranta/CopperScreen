/*
 * Copyright (C) 2026 Ilkka Lehtoranta
 * SPDX-License-Identifier: MIT
 */

using Avalonia.Controls;

namespace CopperScreen;

/// <summary>
/// Hosts configuration independently from the emulator presentation window.
/// </summary>
internal sealed class SettingsWindow : Window
{
	private readonly Action _requestClose;

	public SettingsWindow(Control content, Action requestClose)
	{
		_requestClose = requestClose;
		Title = "CopperScreen Settings";
		Width = 980;
		Height = 760;
		MinWidth = 780;
		MinHeight = 620;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Content = content;
		Closing += OnClosing;
	}

	public bool AllowClose { get; set; }

	private void OnClosing(object? sender, WindowClosingEventArgs args)
	{
		if (AllowClose)
		{
			return;
		}

		args.Cancel = true;
		_requestClose();
	}
}
