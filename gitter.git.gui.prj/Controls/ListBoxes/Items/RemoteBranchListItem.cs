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

namespace gitter.Git.Gui.Controls
{
	using System;
	using System.Drawing;
	using System.Windows.Forms;

	using gitter.Framework;
	using gitter.Framework.Controls;

	/// <summary>A <see cref="CustomListBoxItem"/> representing <see cref="RemoteBranch"/> object.</summary>
	public sealed class RemoteBranchListItem : ReferenceListItemBase<RemoteBranch>
	{
		#region Static

		private static readonly Bitmap ImgBranchRemote = CachedResources.Bitmaps["ImgBranchRemote"];

		#endregion

		#region .ctor

		/// <summary>Create <see cref="RemoteBranchListItem"/>.</summary>
		/// <param name="branch">Related <see cref="RemoteBranch"/>.</param>
		/// <exception cref="ArgumentNullException"><paramref name="branch"/> == <c>null</c>.</exception>
		public RemoteBranchListItem(RemoteBranch branch)
			: base(branch)
		{
			Verify.Argument.IsNotNull(branch, nameof(branch));
		}

		#endregion

		#region Overrides

		protected override Image Image => ImgBranchRemote;

		protected override Size OnMeasureSubItem(SubItemMeasureEventArgs measureEventArgs)
		{
			switch((ColumnId)measureEventArgs.SubItemId)
			{
				case ColumnId.Name:
					var rli = Parent as RemoteListItem;
					return measureEventArgs.MeasureImageAndText(ImgBranchRemote,
						rli != null ? DataContext.Name.Substring(rli.DataContext.Name.Length + 1) : DataContext.Name);
				default:
					return base.OnMeasureSubItem(measureEventArgs);
			}
		}

		protected override void OnPaintSubItem(SubItemPaintEventArgs paintEventArgs)
		{
			switch((ColumnId)paintEventArgs.SubItemId)
			{
				case ColumnId.Name:
					var rli = Parent as RemoteListItem;
					paintEventArgs.PaintImageAndText(ImgBranchRemote,
						rli != null ? DataContext.Name.Substring(rli.DataContext.Name.Length + 1) : DataContext.Name);
					break;
				default:
					base.OnPaintSubItem(paintEventArgs);
					break;
			}
		}

		public override ContextMenuStrip GetContextMenu(ItemContextMenuRequestEventArgs requestEventArgs)
		{
			var menu = new BranchMenu(DataContext);
			Utility.MarkDropDownForAutoDispose(menu);
			return menu;
		}

		#endregion
	}
}
