#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Runtime.CompilerServices;
using OpenRA.Widgets;

namespace OpenRA
{
	public class Input : IInputHandler
	{
		readonly IPlatformWindow window;
		Modifiers modifiers;
		public Modifiers GetModifierKeys() { return modifiers; }

		public bool WindowHasInputFocus => window.HasInputFocus;

		public bool HasModifier(Modifiers mod)
		{
			return modifiers.HasFlag(mod);
		}

		public Input(IPlatformWindow window)
		{
			this.window = window;
		}

		public void Pump()
		{
			window.PumpInput(this);
		}

		public string GetClipboardText()
		{
			return window.GetClipboardText();
		}

		public bool SetClipboardText(string text)
		{
			return window.SetClipboardText(text);
		}

		public void GrabWindowMouseFocus()
		{
			window.GrabWindowMouseFocus();
		}

		public void ReleaseWindowMouseFocus()
		{
			window.ReleaseWindowMouseFocus();
		}

		void IInputHandler.ModifierKeys(Modifiers mods)
		{
			modifiers = mods;
		}

		void IInputHandler.OnKeyInput(KeyInput input)
		{
			Sync.RunUnsynced(Game.Settings.Debug.SyncCheckUnsyncedCode, world, () => Ui.HandleKeyPress(input));
		}

		void IInputHandler.OnMouseInput(MouseInput input)
		{
			Sync.RunUnsynced(Game.Settings.Debug.SyncCheckUnsyncedCode, world, () => Ui.HandleInput(input));
		}

		void IInputHandler.OnTextInput(string text)
		{
			Sync.RunUnsynced(Game.Settings.Debug.SyncCheckUnsyncedCode, world, () => Ui.HandleTextInput(text));
		}
	}

	/*
	public class Platform1
	{
		public IFont CreateFont(byte[] data)
		{
			return platform.CreateFont(data);
		}

		public int DisplayCount => Window.DisplayCount;

		public int CurrentDisplay => Window.CurrentDisplay;
	}
	*/
}
