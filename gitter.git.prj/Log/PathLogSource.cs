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

namespace gitter.Git
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	using gitter.Framework;

	using Resources = gitter.Git.Properties.Resources;

	public sealed class PathLogSource : LogSourceBase
	{
		#region .ctor

		/// <summary>Initializes a new instance of the <see cref="PathLogSource"/> class.</summary>
		/// <param name="revision">Revision to start history log from.</param>
		/// <param name="path">Inspected path.</param>
		/// <param name="followRenames">if set to <c>true</c> follow file renames.</param>
		public PathLogSource(IRevisionPointer revision, string path, bool followRenames = true)
		{
			Verify.Argument.IsNotNull(revision, nameof(revision));
			Verify.Argument.IsNotNull(path, nameof(path));

			Revision = revision;
			Path = path;
			FollowRenames = followRenames;
		}

		#endregion

		#region Properties

		public override Repository Repository => Revision.Repository;

		public bool FollowRenames { get; }

		public IRevisionPointer Revision { get; }

		public string Path { get; }

		#endregion

		#region Overrides

		public override async Task<RevisionLog> GetRevisionLogAsync(LogOptions options, IProgress<OperationProgress> progress, CancellationToken cancellationToken)
		{
			if(Repository.IsEmpty)
			{
				var tcs = new TaskCompletionSource<RevisionLog>();
				if(cancellationToken.IsCancellationRequested)
				{
					tcs.SetCanceled();
				}
				else
				{
					tcs.SetResult(new RevisionLog(Repository, new Revision[0]));
				}
				return await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				var parameters = options.GetLogParameters();
				parameters.References = new[] { Revision.Pointer };
				parameters.Paths = new[] { Path };
				parameters.Follow = FollowRenames;

				progress?.Report(new OperationProgress(Resources.StrsFetchingLog.AddEllipsis()));
				var revisionData = await Repository
					.Accessor
					.QueryRevisions
					.InvokeAsync(parameters, progress, cancellationToken)
					.ConfigureAwait(continueOnCapturedContext: false);
				progress?.Report(OperationProgress.Completed);
				var revisions = Repository.Revisions.Resolve(revisionData);
				return new RevisionLog(Repository, revisions);
			}
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
		/// <returns>
		///   <c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
		/// </returns>
		public override bool Equals(object obj) => obj is PathLogSource other && Revision == other.Revision && Path == other.Path;

		/// <summary>
		/// Returns a hash code for this instance.
		/// </summary>
		/// <returns>
		/// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
		/// </returns>
		public override int GetHashCode() => Revision.GetHashCode() ^ Path.GetHashCode();

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String"/> that represents this instance.
		/// </returns>
		public override string ToString()
			=> Revision is Revision
				? Path + " @ " + Revision.Pointer.Substring(0, 7)
				: Path + " @ " + Revision.Pointer;

		#endregion
	}
}
