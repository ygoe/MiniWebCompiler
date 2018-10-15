using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Unclassified.UI
{
	/// <summary>
	/// Provides extension methods for WPF Windows.
	/// </summary>
	public static class WindowExtensions
	{
		// Based on: http://stackoverflow.com/a/6024229
		// Really hide the icon: http://stackoverflow.com/a/25139586
		// Set native enabled: https://stackoverflow.com/a/428782

		#region Native interop

		[DllImport("user32.dll")]
		private static extern uint GetWindowLong(IntPtr hwnd, int index);

		[DllImport("user32.dll")]
		private static extern uint SetWindowLong(IntPtr hwnd, int index, uint newStyle);

		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

		[StructLayout(LayoutKind.Sequential)]
		private struct FLASHWINFO
		{
			public uint cbSize;
			public IntPtr hwnd;
			public uint dwFlags;
			public uint uCount;
			public uint dwTimeout;
		}

		private const int GWL_EXSTYLE = -20;
		private const int WS_EX_DLGMODALFRAME = 0x0001;
		private const int SWP_NOSIZE = 0x0001;
		private const int SWP_NOMOVE = 0x0002;
		private const int SWP_NOZORDER = 0x0004;
		private const int SWP_FRAMECHANGED = 0x0020;
		private const int GWL_STYLE = -16;
		private const uint WS_MAXIMIZEBOX = 0x00010000;
		private const uint WS_MINIMIZEBOX = 0x00020000;
		private const uint WS_SYSMENU = 0x00080000;
		private const uint WS_DISABLED = 0x08000000;
		private const uint WS_POPUP = 0x80000000;
		private const uint DS_3DLOOK = 0x0004;
		private const uint DS_SETFONT = 0x40;
		private const uint DS_MODALFRAME = 0x80;
		private const uint WM_SETICON = 0x0080;

		// Stop flashing. The system restores the window to its original state.
		private const uint FLASHW_STOP = 0;
		// Flash the window caption.
		private const uint FLASHW_CAPTION = 1;
		// Flash the taskbar button.
		private const uint FLASHW_TRAY = 2;
		// Flash both the window caption and taskbar button.
		// This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
		private const uint FLASHW_ALL = 3;
		// Flash continuously, until the FLASHW_STOP flag is set.
		private const uint FLASHW_TIMER = 4;
		// Flash continuously until the window comes to the foreground.
		private const uint FLASHW_TIMERNOFG = 12;

		#endregion Native interop

		#region Title bar icons and buttons

		/// <summary>
		/// Hides the icon in the window title bar.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		public static void HideIcon(this Window window)
		{
			IntPtr hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd.ToInt64() == 0)
			{
				window.SourceInitialized += (s, a) => HideIcon(window);
			}
			else
			{
				uint exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
				SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_DLGMODALFRAME);
				SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
				SendMessage(hwnd, WM_SETICON, new IntPtr(1), IntPtr.Zero);   // Important if there's a native icon resource in the .exe file
				SendMessage(hwnd, WM_SETICON, IntPtr.Zero, IntPtr.Zero);
			}
		}

		/// <summary>
		/// Hides the minimize button in the window title bar.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		public static void HideMinimizeBox(this Window window)
		{
			IntPtr hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd.ToInt64() == 0)
			{
				window.SourceInitialized += (s, a) => HideMinimizeBox(window);
			}
			else
			{
				SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_MINIMIZEBOX);
			}
		}

		/// <summary>
		/// Hides the maximize button in the window title bar.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		public static void HideMaximizeBox(this Window window)
		{
			IntPtr hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd.ToInt64() == 0)
			{
				window.SourceInitialized += (s, a) => HideMaximizeBox(window);
			}
			else
			{
				SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_MAXIMIZEBOX);
			}
		}

		/// <summary>
		/// Hides the minimize and maximize buttons in the window title bar.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		public static void HideMinimizeAndMaximizeBoxes(this Window window)
		{
			IntPtr hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd.ToInt64() == 0)
			{
				window.SourceInitialized += (s, a) => HideMinimizeAndMaximizeBoxes(window);
			}
			else
			{
				SetWindowLong(hwnd, GWL_STYLE,
					GetWindowLong(hwnd, GWL_STYLE) & ~(WS_MAXIMIZEBOX | WS_MINIMIZEBOX));
			}
		}

		/// <summary>
		/// Hides the close button in the window title bar.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		public static void HideCloseBox(this Window window)
		{
			IntPtr hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd.ToInt64() == 0)
			{
				window.SourceInitialized += (s, a) => HideCloseBox(window);
			}
			else
			{
				SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
			}
		}

		#endregion Title bar icons and buttons

		#region Flashing

		/// <summary>
		/// Flashes the window in the task bar.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		/// <returns></returns>
		public static bool Flash(this Window window)
		{
			IntPtr hWnd = new WindowInteropHelper(window).Handle;
			if (hWnd.ToInt64() != 0)
			{
				var fInfo = new FLASHWINFO();
				fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
				fInfo.hwnd = hWnd;
				fInfo.dwFlags = FLASHW_TIMER | FLASHW_TRAY;
				fInfo.uCount = 3;
				fInfo.dwTimeout = 0;
				return FlashWindowEx(ref fInfo);
			}
			return false;
		}

		/// <summary>
		/// Stops flashing the window in the task bar.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		/// <returns></returns>
		public static bool StopFlashing(this Window window)
		{
			IntPtr hWnd = new WindowInteropHelper(window).Handle;
			if (hWnd.ToInt64() != 0)
			{
				var fInfo = new FLASHWINFO();
				fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
				fInfo.hwnd = hWnd;
				fInfo.dwFlags = FLASHW_STOP;
				fInfo.uCount = 0;
				fInfo.dwTimeout = 0;
				return FlashWindowEx(ref fInfo);
			}
			return false;
		}

		#endregion Flashing

		#region State

		/// <summary>
		/// Determines whether the window has been closed and cannot be shown anymore.
		/// </summary>
		/// <param name="window">The Window instance.</param>
		/// <returns>true, if the window is null or closed; otherwise, false.</returns>
		public static bool IsClosed(this Window window)
		{
			if (window == null) return true;
			var ps = PresentationSource.FromVisual(window);
			if (ps == null) return true;
			return ps.IsDisposed;
		}

		/// <summary>
		/// Sets the window's WS_DISABLED style without telling the framework about it. The window
		/// can be made disabled for the operating system and will not react to user input but still
		/// appear enabled.
		/// </summary>
		/// <param name="isEnabled">true, to enable the window; false, to disable it.</param>
		/// <returns>true, if the state was applied; otherwise, false.</returns>
		public static bool SetNativeEnabled(this Window window, bool isEnabled)
		{
			IntPtr hWnd = new WindowInteropHelper(window).Handle;
			if (hWnd.ToInt64() != 0)
			{
				long prevStyle = SetWindowLong(
					hWnd,
					GWL_STYLE,
					GetWindowLong(hWnd, GWL_STYLE) & ~WS_DISABLED | (isEnabled ? 0 : WS_DISABLED));
				return prevStyle != 0;
			}
			return false;
		}

		/// <summary>
		/// Gets the window's WS_DISABLED style. See <see cref="SetNativeEnabled(Window, bool)"/>.
		/// </summary>
		/// <returns>true, if the window is enabled; otherwise, false.</returns>
		public static bool IsNativeEnabled(this Window window)
		{
			IntPtr hWnd = new WindowInteropHelper(window).Handle;
			if (hWnd.ToInt64() != 0)
			{
				long style = GetWindowLong(hWnd, GWL_STYLE);
				return (style & WS_DISABLED) == 0;
			}
			return false;
		}

		#endregion State

		#region DPI

		/// <summary>
		/// Determines the DPI scaling of the window or Visual.
		/// </summary>
		/// <param name="visual"></param>
		/// <returns></returns>
		public static int GetDpi(this Visual visual)
		{
			DpiScale dpiScale = VisualTreeHelper.GetDpi(visual);
			return (int)dpiScale.PixelsPerInchX;
		}

		/// <summary>
		/// Returns the PixelsPerDip at which the text should be rendered.
		/// </summary>
		/// <param name="visual"></param>
		/// <returns></returns>
		public static double GetPixelsPerDip(this Visual visual)
		{
			DpiScale dpiScale = VisualTreeHelper.GetDpi(visual);
			return dpiScale.PixelsPerDip;
		}

		#endregion DPI

		#region Visible area

		/// <summary>
		/// Moves the window into the visible screen area.
		/// </summary>
		public static void MoveToVisibleArea(this Window window)
		{
			// Source: http://stackoverflow.com/a/37927012/143684
			//         (Taskbar detection is broken and not used here.)
			// Note that "window.BringIntoView()" does not work.
			// Note that Window bounds are already in logical ("virtual") pixels, not device pixels.
			if (window.Top < SystemParameters.VirtualScreenTop)
			{
				window.Top = SystemParameters.VirtualScreenTop;
			}
			if (window.Left < SystemParameters.VirtualScreenLeft)
			{
				window.Left = SystemParameters.VirtualScreenLeft;
			}
			if (window.Left + window.Width > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
			{
				window.Left = SystemParameters.VirtualScreenWidth + SystemParameters.VirtualScreenLeft - window.Width;
			}
			if (window.Top + window.Height > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
			{
				window.Top = SystemParameters.VirtualScreenHeight + SystemParameters.VirtualScreenTop - window.Height;
			}
		}

		#endregion Visible area
	}
}
