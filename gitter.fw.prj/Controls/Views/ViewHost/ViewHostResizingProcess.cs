#region Copyright Notice
/*
 * gitter - VCS repository management tool
 * Copyright (C) 2013  Popovskiy Maxim Vladimirovitch <amgine.gitter@gmail.com>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
#endregion

namespace gitter.Framework.Controls
{
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Windows.Forms;

	sealed class ViewHostResizingProcess : IMouseDragProcess, IDisposable
	{
		#region Data

		private readonly ViewHost _viewHost;
		private SplitterMarker _splitterMarker;
		private bool _isActive;
		private int _resizeOffset;
		private int _minimumPosition;
		private int _maximumPosition;

		#endregion

		#region .ctor

		public ViewHostResizingProcess(ViewHost viewHost)
		{
			Verify.Argument.IsNotNull(viewHost, nameof(viewHost));

			_viewHost = viewHost;
		}

		#endregion

		#region Properties

		public ViewHost ViewHost
		{
			get { return _viewHost; }
		}

		public bool IsActive
		{
			get { return _isActive; }
		}

		#endregion

		public bool Start(Point location)
		{
			Verify.State.IsFalse(IsActive);
			Verify.State.IsTrue(_viewHost.Status == ViewHostStatus.AutoHide);
			Verify.State.IsTrue(_viewHost.Visible);

			_isActive = true;
			var size = _viewHost.Size;
			Rectangle bounds;
			var side = _viewHost.DockSide;
			var grid = side.Grid;
			var rootCtlBounds = grid.RootControl.Bounds;
			Orientation orientation;
			switch(side.Side)
			{
				case AnchorStyles.Left:
					_minimumPosition = ViewConstants.MinimumHostWidth;
					_maximumPosition = grid.HorizontalClientSpace - ViewConstants.SideDockPanelBorderSize;
					bounds = new Rectangle(
						size.Width - ViewConstants.SideDockPanelBorderSize, 0,
						ViewConstants.SideDockPanelBorderSize, size.Height);
					_resizeOffset = location.X - bounds.X;
					orientation = Orientation.Horizontal;
					break;
				case AnchorStyles.Top:
					_minimumPosition = ViewConstants.MinimumHostHeight;
					_maximumPosition = grid.VerticalClientSpace - ViewConstants.SideDockPanelBorderSize;
					bounds = new Rectangle(
						0, size.Height - ViewConstants.SideDockPanelBorderSize,
						size.Width, ViewConstants.SideDockPanelBorderSize);
					_resizeOffset = location.Y - bounds.Y;
					orientation = Orientation.Vertical;
					break;
				case AnchorStyles.Right:
					_minimumPosition = size.Width - grid.HorizontalClientSpace;
					_maximumPosition = size.Width - ViewConstants.MinimumHostWidth;
					bounds = new Rectangle(
						0, 0,
						ViewConstants.SideDockPanelBorderSize, size.Height);
					_resizeOffset = location.X - bounds.X;
					orientation = Orientation.Horizontal;
					break;
				case AnchorStyles.Bottom:
					_minimumPosition = size.Height - grid.VerticalClientSpace;
					_maximumPosition = size.Height - ViewConstants.MinimumHostHeight;
					bounds = new Rectangle(
						0, 0,
						size.Width, ViewConstants.SideDockPanelBorderSize);
					_resizeOffset = location.Y - bounds.Y;
					orientation = Orientation.Vertical;
					break;
				default:
					throw new ApplicationException();
			}
			bounds.Location = _viewHost.PointToClient(bounds.Location);
			SpawnMarker(bounds, orientation);
			return true;
		}

		public void Update(Point location)
		{
			Verify.State.IsTrue(IsActive);

			switch(_viewHost.DockSide.Orientation)
			{
				case Orientation.Vertical:
					{
						var x = location.X - _resizeOffset;
						if(x < _minimumPosition)
						{
							x = _minimumPosition;
						}
						else if(x > _maximumPosition)
						{
							x = _maximumPosition;
						}
						location = new Point(x, 0);
					}
					break;
				case Orientation.Horizontal:
					{
						var y = location.Y - _resizeOffset;
						if(y < _minimumPosition)
						{
							y = _minimumPosition;
						}
						else if(y > _maximumPosition)
						{
							y = _maximumPosition;
						}
						location = new Point(0, y);
					}
					break;
				default:
					throw new ApplicationException("Unexpected ViewDockSide.Orientation: " + _viewHost.DockSide.Orientation);
			}
			location = _viewHost.PointToScreen(location);
			_splitterMarker.Location = location;
		}

		public void Commit(Point e)
		{
			Verify.State.IsTrue(IsActive);

			KillMarker();
			_isActive = false;
			switch(_viewHost.DockSide.Side)
			{
				case AnchorStyles.Left:
					{
						var x = e.X - _resizeOffset;
						if(x < _minimumPosition)
						{
							x = _minimumPosition;
						}
						else if(x > _maximumPosition)
						{
							x = _maximumPosition;
						}
						_viewHost.Width = x + ViewConstants.SideDockPanelBorderSize;
					}
					break;
				case AnchorStyles.Top:
					{
						var y = e.Y - _resizeOffset;
						if(y < _minimumPosition)
						{
							y = _minimumPosition;
						}
						else if(y > _maximumPosition)
						{
							y = _maximumPosition;
						}
						_viewHost.Height = y + ViewConstants.SideDockPanelBorderSize;
					}
					break;
				case AnchorStyles.Right:
					{
						var x = e.X - _resizeOffset;
						if(x < _minimumPosition)
						{
							x = _minimumPosition;
						}
						else if(x > _maximumPosition)
						{
							x = _maximumPosition;
						}
						var w = _viewHost.Width - x;
						var dw = _viewHost.Width - w;
						 _viewHost.SetBounds(_viewHost.Left + dw, 0, w, 0, BoundsSpecified.X | BoundsSpecified.Width);
					}
					break;
				case AnchorStyles.Bottom:
					{
						var y = e.Y - _resizeOffset;
						if(y < _minimumPosition)
						{
							y = _minimumPosition;
						}
						else if(y > _maximumPosition)
						{
							y = _maximumPosition;
						}
						var h = _viewHost.Height - y;
						var dh = _viewHost.Height - h;
						_viewHost.SetBounds(0, _viewHost.Top + dh, 0, h, BoundsSpecified.Y | BoundsSpecified.Height);
					}
					break;
				default:
					throw new ApplicationException("Unexpected ViewDockSide.Side: " + _viewHost.DockSide.Side);
			}
		}

		public void Cancel()
		{
			Verify.State.IsTrue(IsActive);

			KillMarker();
			_isActive = false;
		}

		private void SpawnMarker(Rectangle bounds, Orientation orientation)
		{
			Verify.State.IsTrue(_splitterMarker == null);

			_splitterMarker = new SplitterMarker(bounds, orientation);
			_splitterMarker.Show();
		}

		private void KillMarker()
		{
			if(_splitterMarker != null)
			{
				_splitterMarker.Close();
				_splitterMarker.Dispose();
				_splitterMarker = null;
			}
		}

		public void Dispose()
		{
			if(_splitterMarker != null)
			{
				_splitterMarker.Dispose();
				_splitterMarker = null;
			}
			_isActive = false;
		}
	}
}
