﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using dnGREP.Common;
using dnGREP.Localization;
using dnGREP.Localization.Properties;
using ICSharpCode.AvalonEdit.Highlighting;

namespace dnGREP.WPF
{
    /// <summary>
    /// View model for the replace dialog
    /// </summary>
    public class ReplaceViewModel : CultureAwareViewModel
    {
        public event EventHandler LoadFile;
        public event EventHandler ReplaceMatch;
        public event EventHandler CloseTrue;

        private int fileIndex = -1;
        private int matchIndex = -1;

        public ReplaceViewModel()
        {
            Highlighters = ThemedHighlightingManager.Instance.HighlightingNames.ToList();
            Highlighters.Sort();
            Highlighters.Insert(0, Resources.Replace_SyntaxNone);
            CurrentSyntax = Resources.Replace_SyntaxNone;
            ApplicationFontFamily = GrepSettings.Instance.Get<string>(GrepSettings.Key.ApplicationFontFamily);
            ReplaceFormFontSize = GrepSettings.Instance.Get<double>(GrepSettings.Key.ReplaceFormFontSize);
        }

        public void SelectNextFile()
        {
            if (SearchResults != null && fileIndex < SearchResults.Count - 1)
            {
                fileIndex++;
                FileNumber++;

                SelectedSearchResult = SearchResults[fileIndex];

                FormatFileStatus();

                matchIndex = -1;
                SelectNextMatch();
            }
        }

        private void SelectPrevFile()
        {
            if (SearchResults != null && fileIndex > 0)
            {
                fileIndex--;
                FileNumber--;

                SelectedSearchResult = SearchResults[fileIndex];

                FormatFileStatus();

                matchIndex = -1;
                SelectNextMatch();
            }
        }

        private void FormatFileStatus()
        {
            var item = SelectedSearchResult;
            if (item != null)
            {
                string matchStr = string.Empty;
                string lineStr = string.Empty;
                int matchCount = item.Matches == null ? 0 : item.Matches.Count;
                if (matchCount > 0)
                {
                    var lineCount = item.Matches.Where(r => r.LineNumber > 0)
                       .Select(r => r.LineNumber).Distinct().Count();

                    matchStr = matchCount.ToString("N0", CultureInfo.CurrentCulture);
                    lineStr = lineCount.ToString("N0", CultureInfo.CurrentCulture);
                }

                string formattedText = TranslationSource.Format(Resources.Replace_FileNumberOfCountName,
                   FileNumber, FileCount, item.FileNameReal, matchStr, lineStr);

                if (Utils.IsReadOnly(item))
                {
                    formattedText += " " + Resources.Replace_ReadOnly;
                }

                FileStatus = formattedText;
            }
            else
            {
                FileStatus = string.Empty;
            }
        }

        private void SelectNextMatch()
        {
            if (SelectedGrepMatch != null)
                SelectedGrepMatch = null;

            if (SelectedSearchResult != null && IndividualReplaceEnabled)
            {
                if (matchIndex < SelectedSearchResult.Matches.Count - 1)
                {
                    matchIndex++;
                    SelectedGrepMatch = SelectedSearchResult.Matches[matchIndex];
                }
                else
                {
                    matchIndex = 0;
                    SelectedGrepMatch = SelectedSearchResult.Matches[matchIndex];
                }
            }
        }

        private void SelectPrevMatch()
        {
            if (SelectedSearchResult != null && IndividualReplaceEnabled)
            {
                if (matchIndex > 0)
                {
                    matchIndex--;
                    SelectedGrepMatch = SelectedSearchResult.Matches[matchIndex];
                }
                else
                {
                    matchIndex = SelectedSearchResult.Matches.Count - 1;
                    SelectedGrepMatch = SelectedSearchResult.Matches[matchIndex];
                }
            }
        }

        internal void MoveToMatch(int line, int column)
        {
            if (SelectedSearchResult != null && IndividualReplaceEnabled)
            {
                var lineMatch = SelectedSearchResult.SearchResults.Where(sr => sr.LineNumber == line)
                    .SelectMany(sr => sr.Matches)
                    .FirstOrDefault(m => m.StartLocation <= column && column <= m.EndPosition);

                if (lineMatch != null)
                {
                    var match = SelectedSearchResult.Matches.FirstOrDefault(m => m.FileMatchId == lineMatch.FileMatchId);
                    if (match != null)
                    {
                        matchIndex = SelectedSearchResult.Matches.IndexOf(match);
                        SelectedGrepMatch = match;
                    }
                }
            }
        }

        private List<GrepSearchResult> _searchResults = null;
        public List<GrepSearchResult> SearchResults
        {
            get { return _searchResults; }
            set
            {
                if (_searchResults == value)
                    return;

                _searchResults = value;

                FileNumber = 0;
                FileCount = _searchResults.Count;
                fileIndex = -1;

                base.OnPropertyChanged(() => SearchResults);
            }
        }

        private GrepSearchResult _selectedSearchResult = null;
        public GrepSearchResult SelectedSearchResult
        {
            get { return _selectedSearchResult; }
            set
            {
                if (_selectedSearchResult == value)
                    return;

                _selectedSearchResult = value;
                base.OnPropertyChanged(() => SelectedSearchResult);

                CurrentSyntax = Resources.Replace_SyntaxNone; // by default, turn off syntax highlighting (easier to see the match highlights)
                Encoding = _selectedSearchResult.Encoding;
                LineNumbers.Clear();
                FileText = string.Empty;
                FilePath = string.Empty;
                IndividualReplaceEnabled = true;

                FileInfo fileInfo = new FileInfo(_selectedSearchResult.FileNameReal);
                if (Utils.IsBinary(_selectedSearchResult.FileNameReal))
                {
                    FileText = Resources.Replace_ErrorThisIsABinaryFile;
                    IndividualReplaceEnabled = false;
                }
                else if (_selectedSearchResult.Matches.Count > 5000)
                {
                    FileText = Resources.Replace_ThisFileContainsTooManyMatchesForIndividualReplace;
                    IndividualReplaceEnabled = false;
                }
                else
                {
                    if (fileInfo.Length > 50000)
                    {
                        // Large files:  
                        // Use the already parsed matches and context lines only
                        // and map the original line numbers to clipped file

                        StringBuilder sb = new StringBuilder();
                        int tempLineNum = 1;
                        foreach (var line in _selectedSearchResult.SearchResults)
                        {
                            line.ClippedFileLineNumber = tempLineNum;
                            sb.Append(line.LineText).Append(_selectedSearchResult.EOL);
                            LineNumbers.Add(line.LineNumber);

                            tempLineNum++;
                        }
                        FileText = sb.ToString();
                    }
                    else
                    {
                        FilePath = _selectedSearchResult.FileNameReal;
                    }
                }

                LoadFile?.Invoke(this, EventArgs.Empty);
            }
        }

        public List<string> Highlighters { get; private set; }

        public Encoding Encoding { get; private set; }

        public IList<int> LineNumbers { get; } = new List<int>();

        public string FilePath { get; private set; }

        public string FileText { get; private set; }

        private GrepMatch _selectedGrepMatch = null;
        public GrepMatch SelectedGrepMatch
        {
            get { return _selectedGrepMatch; }
            set
            {
                if (_selectedGrepMatch == value)
                    return;

                _selectedGrepMatch = value;

                if (_selectedGrepMatch != null)
                {
                    var lineMatch = _selectedSearchResult.SearchResults
                        .FirstOrDefault(sr => sr.Matches.Any(m => m.FileMatchId == _selectedGrepMatch.FileMatchId));

                    if (lineMatch != null)
                    {
                        LineNumber = lineMatch.ClippedFileLineNumber;

                        ColNumber = lineMatch.Matches
                            .Where(m => m.FileMatchId == _selectedGrepMatch.FileMatchId)
                            .Select(m => m.StartLocation).FirstOrDefault();
                    }
                }

                base.OnPropertyChanged(() => SelectedGrepMatch);
            }
        }

        private string searchFor = string.Empty;
        public string SearchFor
        {
            get { return searchFor; }
            set
            {
                if (value == searchFor)
                    return;

                searchFor = value;

                base.OnPropertyChanged(() => SearchFor);
            }
        }

        private string replaceWith = string.Empty;
        public string ReplaceWith
        {
            get { return replaceWith; }
            set
            {
                if (value == replaceWith)
                    return;

                replaceWith = value;

                base.OnPropertyChanged(() => ReplaceWith);
            }
        }

        private string fileStatus;
        public string FileStatus
        {
            get { return fileStatus; }
            set
            {
                if (value == fileStatus)
                    return;

                fileStatus = value;

                base.OnPropertyChanged(() => FileStatus);
            }
        }

        private int fileCount = 0;
        public int FileCount
        {
            get { return fileCount; }
            set
            {
                if (fileCount == value)
                    return;

                fileCount = value;
                base.OnPropertyChanged(() => FileCount);
                FormatFileStatus();
            }
        }

        private int fileNumber = 0;
        public int FileNumber
        {
            get { return fileNumber; }
            set
            {
                if (fileNumber == value)
                    return;

                fileNumber = value;
                base.OnPropertyChanged(() => FileNumber);
                FormatFileStatus();
            }
        }

        private string currentSyntax;
        public string CurrentSyntax
        {
            get { return currentSyntax; }
            set
            {
                if (value == currentSyntax)
                    return;

                currentSyntax = value;

                base.OnPropertyChanged(() => CurrentSyntax);
            }
        }

        private int lineNumber;
        public int LineNumber
        {
            get { return lineNumber; }
            set
            {
                if (value == lineNumber)
                    return;

                lineNumber = value;

                base.OnPropertyChanged(() => LineNumber);
            }
        }

        private int colNumber;
        public int ColNumber
        {
            get { return colNumber; }
            set
            {
                if (value == colNumber)
                    return;

                colNumber = value;

                base.OnPropertyChanged(() => ColNumber);
            }
        }

        private bool individualReplaceEnabled;
        public bool IndividualReplaceEnabled
        {
            get { return individualReplaceEnabled; }
            set
            {
                if (value == individualReplaceEnabled)
                    return;

                individualReplaceEnabled = value;

                base.OnPropertyChanged(() => IndividualReplaceEnabled);
            }
        }

        public IHighlightingDefinition HighlightingDefinition
        {
            get
            {
                return ThemedHighlightingManager.Instance.GetDefinition(CurrentSyntax);
            }
        }

        private string applicationFontFamily;
        public string ApplicationFontFamily
        {
            get { return applicationFontFamily; }
            set
            {
                if (applicationFontFamily == value)
                    return;

                applicationFontFamily = value;
                base.OnPropertyChanged(() => ApplicationFontFamily);
            }
        }

        private double replaceFormfontSize;
        public double ReplaceFormFontSize
        {
            get { return replaceFormfontSize; }
            set
            {
                if (replaceFormfontSize == value)
                    return;

                replaceFormfontSize = value;
                base.OnPropertyChanged(() => ReplaceFormFontSize);
            }
        }

        #region Commands

        private void ReplaceAll()
        {
            if (SearchResults != null)
            {
                foreach (GrepSearchResult gsr in SearchResults)
                {
                    foreach (var m in gsr.Matches)
                        m.ReplaceMatch = true;
                }

                ReplaceMatch?.Invoke(this, EventArgs.Empty);
            }

            CloseTrue?.Invoke(this, EventArgs.Empty);
        }

        private void MarkAllInFile()
        {
            if (SelectedSearchResult != null)
            {
                foreach (var match in SelectedSearchResult.Matches)
                {
                    match.ReplaceMatch = true;
                }
                ReplaceMatch?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UndoAllMarksInFile()
        {
            if (SelectedSearchResult != null)
            {
                foreach (var match in SelectedSearchResult.Matches)
                {
                    match.ReplaceMatch = false;
                }
                ReplaceMatch?.Invoke(this, EventArgs.Empty);
            }
        }

        private void MarkMatchForReplace()
        {
            if (SelectedGrepMatch != null)
            {
                SelectedGrepMatch.ReplaceMatch = true;
                ReplaceMatch?.Invoke(this, EventArgs.Empty);
            }

            SelectNextMatch();
        }

        private void UndoMarkMatchForReplace()
        {
            if (SelectedGrepMatch != null)
            {
                SelectedGrepMatch.ReplaceMatch = false;
                ReplaceMatch?.Invoke(this, EventArgs.Empty);
            }
        }

        private RelayCommand _replaceAllCommand;
        public ICommand ReplaceAllCommand
        {
            get
            {
                if (_replaceAllCommand == null)
                {
                    _replaceAllCommand = new RelayCommand(
                        p => ReplaceAll(),
                        q => SearchResults != null
                        );
                }
                return _replaceAllCommand;
            }
        }

        private RelayCommand _nextFileCommand;
        public ICommand NextFileCommand
        {
            get
            {
                if (_nextFileCommand == null)
                {
                    _nextFileCommand = new RelayCommand(
                        p => SelectNextFile(),
                        q => SearchResults != null && SearchResults.Count > 1 && fileIndex < SearchResults.Count - 1
                        );
                }
                return _nextFileCommand;
            }
        }

        private RelayCommand _prevFileCommand;
        public ICommand PrevFileCommand
        {
            get
            {
                if (_prevFileCommand == null)
                {
                    _prevFileCommand = new RelayCommand(
                        p => SelectPrevFile(),
                        q => SearchResults != null && SearchResults.Count > 1 && fileIndex > 0
                        );
                }
                return _prevFileCommand;
            }
        }

        private RelayCommand _prevMatchCommand;
        public ICommand PrevMatchCommand
        {
            get
            {
                if (_prevMatchCommand == null)
                {
                    _prevMatchCommand = new RelayCommand(
                        p => SelectPrevMatch(),
                        q => SelectedSearchResult != null && SelectedSearchResult.Matches.Count > 1 && IndividualReplaceEnabled
                        );
                }
                return _prevMatchCommand;
            }
        }

        private RelayCommand _nextMatchCommand;
        public ICommand NextMatchCommand
        {
            get
            {
                if (_nextMatchCommand == null)
                {
                    _nextMatchCommand = new RelayCommand(
                        p => SelectNextMatch(),
                        q => SelectedSearchResult != null && SelectedSearchResult.Matches.Count > 1 && IndividualReplaceEnabled
                        );
                }
                return _nextMatchCommand;
            }
        }

        private RelayCommand _replaceAllInFileCommand;
        public ICommand ReplaceAllInFileCommand
        {
            get
            {
                if (_replaceAllInFileCommand == null)
                {
                    _replaceAllInFileCommand = new RelayCommand(
                        p => MarkAllInFile(),
                        q => SelectedSearchResult != null && !SelectedSearchResult.Matches.All(m => m.ReplaceMatch)
                        );
                }
                return _replaceAllInFileCommand;
            }
        }

        RelayCommand _undoFileCommand;
        public ICommand UndoFileCommand
        {
            get
            {
                if (_undoFileCommand == null)
                {
                    _undoFileCommand = new RelayCommand(
                        param => this.UndoAllMarksInFile(),
                        param => SelectedSearchResult != null && SelectedSearchResult.Matches.Any(m => m.ReplaceMatch)
                        );
                }
                return _undoFileCommand;
            }
        }

        private RelayCommand _replaceMatchCommand;
        public ICommand ReplaceMatchCommand
        {
            get
            {
                if (_replaceMatchCommand == null)
                {
                    _replaceMatchCommand = new RelayCommand(
                        p => MarkMatchForReplace(),
                        q => SelectedGrepMatch != null && !SelectedGrepMatch.ReplaceMatch
                        );
                }
                return _replaceMatchCommand;
            }
        }

        RelayCommand _undoMatchCommand;
        public ICommand UndoMatchCommand
        {
            get
            {
                if (_undoMatchCommand == null)
                {
                    _undoMatchCommand = new RelayCommand(
                        param => UndoMarkMatchForReplace(),
                        param => SelectedGrepMatch != null && SelectedGrepMatch.ReplaceMatch
                        );
                }
                return _undoMatchCommand;
            }
        }

        #endregion
    }
}
