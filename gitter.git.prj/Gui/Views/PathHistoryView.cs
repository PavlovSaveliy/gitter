﻿namespace gitter.Git.Gui.Views
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Drawing;
	using System.Windows.Forms;

	using gitter.Framework;
	using gitter.Framework.Controls;
	using gitter.Framework.Configuration;

	using gitter.Git.Gui.Controls;

	using Resources = gitter.Git.Properties.Resources;

	[ToolboxItem(false)]
	partial class PathHistoryView : GitViewBase, ISearchableView<HistorySearchOptions>
	{
		#region Data

		private PathLogSource _logSource;
		private RevisionLog _revisionLog;
		private LogOptions _options;
		private PathHistoryToolbar _toolBar;
		private HistorySearchToolBar<PathHistoryView> _searchToolbar;
		private bool _autoShowDiff;
		private AsyncLogRequest _pendingRequest;

		#endregion

		#region Events

		public event EventHandler LogOptionsChanged;

		#endregion

		public PathHistoryView(IDictionary<string, object> parameters, GuiProvider gui)
			: base(Guids.PathHistoryViewGuid, gui, parameters)
		{
			InitializeComponent();

			_autoShowDiff = true;

			for(int i = 0; i < _lstRevisions.Columns.Count; ++i)
			{
				if(_lstRevisions.Columns[i].Id == (int)ColumnId.Graph)
				{
					_lstRevisions.Columns.RemoveAt(i);
					break;
				}
			}
			_lstRevisions.Text = Resources.StrNoCommitsToDisplay;
			_lstRevisions.Multiselect = true;
			_lstRevisions.SelectionChanged += (sender, e) =>
				{
					if(ShowDetails)
					{
						ShowSelectedCommitDetails();
					}
				};
			_lstRevisions.ItemActivated += OnItemActivated;
			_lstRevisions.PreviewKeyDown += OnKeyDown;
			_options = new LogOptions();
			_options.Changed += OnLogOptionsChanged;
			AddTopToolStrip(_toolBar = new PathHistoryToolbar(this));
			ApplyParameters(parameters);
		}

		public override void ApplyParameters(IDictionary<string, object> parameters)
		{
			base.ApplyParameters(parameters);
			_logSource = (PathLogSource)parameters["source"];
			if(_logSource != null)
			{
				Text = Resources.StrHistory + ": " + _logSource.ToString();
				var request = new AsyncLogRequest(Repository, _logSource.GetLogAsync(_options));
				_pendingRequest = request;
				request.Query.BeginInvoke(this, _lstRevisions.ProgressMonitor, OnHistoryLoaded, request);
			}
			else
			{
				Text = Resources.StrHistory;
				_lstRevisions.Clear();
			}
		}

		/// <summary>Returns a value indicating whether this instance is a document.</summary>
		/// <value><c>true</c> if this instance is a document; otherwise, <c>false</c>.</value>
		public override bool IsDocument
		{
			get { return true; }
		}

		public LogOptions LogOptions
		{
			get { return _options; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				if(_options != value)
				{
					_options.Changed -= OnLogOptionsChanged;
					_options = value;
					_options.Changed += OnLogOptionsChanged;
					LogOptionsChanged.Raise(this);
					RefreshContent();
				}
			}
		}

		private void OnItemActivated(object sender, ItemEventArgs e)
		{
			var revItem = e.Item as RevisionListItem;
			if(revItem != null)
			{
				ShowDiffTool(new RevisionChangesDiffSource(revItem.DataContext, new[] { _logSource.Path }));
				return;
			}
			var fakeItem = e.Item as FakeRevisionListItem;
			if(fakeItem != null)
			{
				switch(fakeItem.Type)
				{
					case FakeRevisionItemType.StagedChanges:
						ShowDiffTool(new IndexChangesDiffSource(Repository, true, new[] { _logSource.Path }));
						break;
					case FakeRevisionItemType.UnstagedChanges:
						ShowDiffTool(new IndexChangesDiffSource(Repository, false, new[] { _logSource.Path }));
						break;
				}
				return;
			}
		}

		public override Image Image
		{
			get
			{
				if(_logSource != null)
				{
					if(_logSource.Path.EndsWith('/'))
					{
						return CachedResources.Bitmaps["ImgFolderHistory"];
					}
				}
				return CachedResources.Bitmaps["ImgFileHistory"];
			}
		}

		public void SelectRevision(IRevisionPointer revision)
		{
			_lstRevisions.SelectRevision(revision);
		}

		public Revision SelectedRevision
		{
			get
			{
				if(_lstRevisions.SelectedItems.Count == 0) return null;
				var item = _lstRevisions.SelectedItems[0] as RevisionListItem;
				if(item == null) return null;
				return item.DataContext;
			}
		}

		public IEnumerable<Revision> SelectedRevisions
		{
			get
			{
				foreach(var item in _lstRevisions.SelectedItems)
				{
					var rli = item as RevisionListItem;
					if(rli != null) yield return rli.DataContext;
				}
			}
		}

		private sealed class AsyncLogRequest
		{
			private readonly Repository _repository;
			private readonly IAsyncFunc<RevisionLog> _query;

			public AsyncLogRequest(Repository repository, IAsyncFunc<RevisionLog> query)
			{
				_repository = repository;
				_query = query;
			}

			public IAsyncFunc<RevisionLog> Query
			{
				get { return _query; }
			}

			public Repository Repository
			{
				get { return _repository; }
			}
		}

		private void OnHistoryLoaded(IAsyncResult ar)
		{
			if(!IsDisposed)
			{
				var request = (AsyncLogRequest)ar.AsyncState;
				if(request == _pendingRequest)
				{
					var log = request.Query.EndInvoke(ar);
					BeginInvoke(
						new MethodInvoker(
						() =>
						{
							if(!IsDisposed)
							{
								if(_pendingRequest == request)
								{
									_pendingRequest = null;
									_lstRevisions.SetLog(log);
									if(_lstRevisions.UnstagedItem != null)
									{
										_lstRevisions.UnstagedItem.FocusAndSelect();
										return;
									}
									if(_lstRevisions.StagedItem != null)
									{
										_lstRevisions.StagedItem.FocusAndSelect();
										return;
									}
									if(_lstRevisions.HeadItem != null)
									{
										_lstRevisions.HeadItem.FocusAndSelect();
										return;
									}
								}
							}
						}));
				}
			}
		}

		protected override void DetachFromRepository(Repository repository)
		{
			base.DetachFromRepository(repository);

			_lstRevisions.Clear();
			_logSource = null;
			_revisionLog = null;
			_options.Changed -= OnLogOptionsChanged;
			_options.Reset();
			_options.Changed += OnLogOptionsChanged;
			_pendingRequest = null;
			LogOptionsChanged.Raise(this);
		}

		private void OnStashDeleted(object sender, StashedStateEventArgs e)
		{
			if(e.Object.Index == 0)
			{
				var item = _lstRevisions.TryGetItem(e.Object.Revision);
				if(item != null)
				{
					RefreshContent();
				}
			}
		}

		private void OnLogOptionsChanged(object sender, EventArgs e)
		{
			RefreshContent();
			LogOptionsChanged.Raise(this);
		}

		/// <summary>Refreshes the content.</summary>
		public override void RefreshContent()
		{
			if(InvokeRequired)
			{
				BeginInvoke(new MethodInvoker(RefreshContent));
			}
			else
			{
				if(_pendingRequest != null) return;
				if(Repository != null && _logSource != null)
				{
					Cursor = Cursors.WaitCursor;
					_lstRevisions.BeginUpdate();
					_revisionLog = _logSource.GetLog(_options);
					var state = _lstRevisions.GetState();
					_lstRevisions.SetLog(_revisionLog);
					_lstRevisions.SetState(state);
					_lstRevisions.EndUpdate();
					Cursor = Cursors.Default;
				}
			}
		}

		private void ShowSelectedCommitDetails()
		{
			switch(_lstRevisions.SelectedItems.Count)
			{
				case 1:
					{
						var item = _lstRevisions.SelectedItems[0];
						var revisionItem = item as RevisionListItem;
						if(revisionItem != null)
						{
							ShowContextualDiffTool(new RevisionChangesDiffSource(revisionItem.DataContext, new[] { _logSource.Path }));
							return;
						}
						var fakeItem = item as FakeRevisionListItem;
						if(fakeItem != null)
						{
							IDiffSource diff = null;
							switch(fakeItem.Type)
							{
								case FakeRevisionItemType.StagedChanges:
									diff = new IndexChangesDiffSource(Repository, true, new[] { _logSource.Path });
									break;
								case FakeRevisionItemType.UnstagedChanges:
									diff = new IndexChangesDiffSource(Repository, false, new[] { _logSource.Path });
									break;
							}
							if(diff != null)
							{
								ShowContextualDiffTool(diff);
							}
						}
					}
					break;
				case 2:
					{
						var item1 = _lstRevisions.SelectedItems[0];
						var revisionItem1 = item1 as RevisionListItem;
						if(revisionItem1 == null) return;
						var item2 = _lstRevisions.SelectedItems[1];
						var revisionItem2 = item2 as RevisionListItem;
						if(revisionItem2 == null) return;
						ShowContextualDiffTool(new RevisionCompareDiffSource(
							revisionItem1.DataContext, revisionItem2.DataContext, new[] { _logSource.Path }));
					}
					break;
				default:
					break;
			}
		}

		public RevisionListBox RevisionListBox
		{
			get { return _lstRevisions; }
		}

		public bool ShowDetails
		{
			get { return _autoShowDiff; }
			set
			{
				if(value != _autoShowDiff)
				{
					_autoShowDiff = value;
					if(value)
					{
						ShowSelectedCommitDetails();
					}
				}
			}
		}

		private bool TestItem(RevisionListItem item, HistorySearchOptions search)
		{
			var rev = item.DataContext;
			if(rev.Subject.Contains(search.Text)) return true;
			if(rev.Body.Contains(search.Text)) return true;
			if(rev.Author.Name.Contains(search.Text)) return true;
			if(rev.Committer.Name.Contains(search.Text)) return true;
			if(rev.SHA1.StartsWith(search.Text)) return true;
			if(rev.TreeHash.StartsWith(search.Text)) return true;
			lock(rev.RefsSyncRoot)
			{
				foreach(var reference in rev.Refs.Values)
				{
					if(reference.FullName.Contains(search.Text)) return true;
				}
			}
			return false;
		}

		private bool Search(int start, HistorySearchOptions search, int direction)
		{
			if(search.Text.Length == 0) return true;
			int count = _lstRevisions.Items.Count;
			if(count == 0) return false;
			int end;
			if(direction == 1)
			{
				start = (start + 1) % count;
				end = start - 1;
				if(end < 0) end += count;
			}
			else
			{
				start = (start - 1);
				if(start < 0) start += count;
				end = (start + 1) % count;
			}
			while(start != end)
			{
				var item = _lstRevisions.Items[start] as RevisionListItem;
				if(item != null)
				{
					if(TestItem(item, search))
					{
						item.FocusAndSelect();
						return true;
					}
				}
				if(direction == 1)
				{
					start = (start + 1) % count;
				}
				else
				{
					--start;
					if(start < 0) start = count - 1;
				}
			}
			return false;
		}

		public bool SearchFirst(HistorySearchOptions search)
		{
			if(search == null)
			{
				throw new ArgumentNullException("search");
			}

			return Search(-1, search, 1);
		}

		public bool SearchNext(HistorySearchOptions search)
		{
			if(search == null)
				throw new ArgumentNullException("search");

			if(search.Text.Length == 0) return true;
			if(_lstRevisions.SelectedItems.Count == 0)
				return Search(-1, search, 1);
			var start = _lstRevisions.Items.IndexOf(_lstRevisions.SelectedItems[0]);
			return Search(start, search, 1);
		}

		public bool SearchPrevious(HistorySearchOptions search)
		{
			if(search == null) throw new ArgumentNullException("search");

			if(search.Text.Length == 0) return true;
			if(_lstRevisions.SelectedItems.Count == 0) return Search(-1, search, 1);
			var start = _lstRevisions.Items.IndexOf(_lstRevisions.SelectedItems[0]);
			return Search(start, search, -1);
		}

		public bool SearchToolBarVisible
		{
			get { return _searchToolbar != null && _searchToolbar.Visible; }
			set
			{
				if(value)
				{
					ShowSearchToolBar();
				}
				else
				{
					HideSearchToolBar();
				}
			}
		}

		private void ShowSearchToolBar()
		{
			if(_searchToolbar == null)
			{
				AddBottomToolStrip(_searchToolbar = new HistorySearchToolBar<PathHistoryView>(this));
			}
			_searchToolbar.FocusSearchTextBox();
		}

		private void HideSearchToolBar()
		{
			if(_searchToolbar != null)
			{
				RemoveToolStrip(_searchToolbar);
				_searchToolbar.Dispose();
				_searchToolbar = null;
			}
		}

		protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
		{
			OnKeyDown(this, e);
			base.OnPreviewKeyDown(e);
		}

		private void OnKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			switch(e.KeyCode)
			{
				case Keys.F:
					if(e.Modifiers == Keys.Control)
					{
						ShowSearchToolBar();
						e.IsInputKey = true;
					}
					break;
				case Keys.F5:
					RefreshContent();
					break;
			}
		}

		protected override void SaveMoreViewTo(Section section)
		{
			base.SaveMoreViewTo(section);
			var layoutNode = section.GetCreateSection("Layout");
			layoutNode.SetValue("ShowDetails", ShowDetails);
			var listNode = section.GetCreateSection("RevisionList");
			_lstRevisions.SaveViewTo(listNode);
		}

		protected override void LoadMoreViewFrom(Section section)
		{
			base.LoadMoreViewFrom(section);
			var layoutNode = section.TryGetSection("Layout");
			if(layoutNode != null)
			{
				//_toolbar.ShowDiffButton.Checked = ShowDetails = layoutNode.GetValue("ShowDetails", ShowDetails);
			}
			var listNode = section.TryGetSection("RevisionList");
			if(listNode != null)
			{
				_lstRevisions.LoadViewFrom(listNode);
			}
		}
	}
}