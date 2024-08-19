﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.SolutionExplorer;

internal sealed class AnalyzersFolderItemSource : IAttachedCollectionSource
{
    private readonly IThreadingContext _threadingContext;
    private readonly Workspace _workspace;
    private readonly ProjectId _projectId;
    private readonly IVsHierarchyItem _projectHierarchyItem;
    private readonly IAnalyzersCommandHandler _commandHandler;
    private readonly ObservableCollection<AnalyzersFolderItem> _folderItems;

    public AnalyzersFolderItemSource(
        IThreadingContext threadingContext,
        Workspace workspace,
        ProjectId projectId,
        IVsHierarchyItem projectHierarchyItem,
        IAnalyzersCommandHandler commandHandler)
    {
        _threadingContext = threadingContext;
        _workspace = workspace;
        _projectId = projectId;
        _projectHierarchyItem = projectHierarchyItem;
        _commandHandler = commandHandler;

        _folderItems = [new AnalyzersFolderItem(
            _threadingContext,
            _workspace,
            _projectId,
            _projectHierarchyItem,
            _commandHandler.AnalyzerFolderContextMenuController)];
    }

    public bool HasItems => true;

    public IEnumerable Items => _folderItems;

    public object SourceItem => _projectHierarchyItem;
}
