﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using dnGREP.Common;
using dnGREP.Engines;
using dnGREP.Localization;
using Resources = dnGREP.Localization.Properties.Resources;

namespace dnGREP.WPF
{
    public class TestPatternViewModel : BaseMainViewModel
    {
        private bool hasMatches;
        private int searchHash;
        private int replaceHash;
        private List<GrepSearchResult> grepResults = new List<GrepSearchResult>();
        private readonly string horizontalBar = new string(char.ConvertFromUtf32(0x2015)[0], 80);

        public TestPatternViewModel()
        {
            ApplicationFontFamily = GrepSettings.Instance.Get<string>(GrepSettings.Key.ApplicationFontFamily);
            DialogFontSize = GrepSettings.Instance.Get<double>(GrepSettings.Key.DialogFontSize);
        }

        private int GetSearchHash()
        {
            unchecked
            {
                int hashCode = 13;
                hashCode = (hashCode * 397) ^ SampleText?.GetHashCode() ?? 5;
                hashCode = (hashCode * 397) ^ SearchFor?.GetHashCode() ?? 5;
                hashCode = (hashCode * 397) ^ TypeOfSearch.GetHashCode();
                hashCode = (hashCode * 397) ^ CaseSensitive.GetHashCode();
                hashCode = (hashCode * 397) ^ WholeWord.GetHashCode();
                hashCode = (hashCode * 397) ^ Multiline.GetHashCode();
                hashCode = (hashCode * 397) ^ Singleline.GetHashCode();
                hashCode = (hashCode * 397) ^ BooleanOperators.GetHashCode();
                hashCode = (hashCode * 397) ^ HighlightCaptureGroups.GetHashCode();
                return hashCode;
            }
        }

        private int GetReplaceHash()
        {
            unchecked
            {
                int hashCode = GetSearchHash();
                hashCode = (hashCode * 397) ^ ReplaceWith?.GetHashCode() ?? 5;
                return hashCode;
            }
        }

        public override void UpdateState(string name)
        {
            base.UpdateState(name);

            if (name == nameof(HighlightCaptureGroups))
            {
                settings.Set(GrepSettings.Key.HighlightCaptureGroups, HighlightCaptureGroups);
            }

            switch (name)
            {
                case nameof(SampleText):
                case nameof(SearchFor):
                case nameof(TypeOfSearch):
                case nameof(CaseSensitive):
                case nameof(WholeWord):
                case nameof(Multiline):
                case nameof(Singleline):
                case nameof(BooleanOperators):
                case nameof(HighlightCaptureGroups):
                    int sHash = GetSearchHash();
                    if (IsValidPattern && sHash != searchHash)
                    {
                        Search();
                        Replace();

                        searchHash = sHash;
                        replaceHash = GetReplaceHash();
                    }
                    break;

                case nameof(ReplaceWith):
                    int rHash = GetReplaceHash();
                    if (IsValidPattern && rHash != replaceHash)
                    {
                        Replace();

                        replaceHash = rHash;
                    }
                    break;

                case nameof(ValidationMessage):
                    HasValidationMessage = !string.IsNullOrWhiteSpace(ValidationMessage);
                    break;
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

        private double dialogfontSize;
        public double DialogFontSize
        {
            get { return dialogfontSize; }
            set
            {
                if (dialogfontSize == value)
                    return;

                dialogfontSize = value;
                base.OnPropertyChanged(() => DialogFontSize);
            }
        }


        private bool hasValidationMessage;

        public bool HasValidationMessage
        {
            get { return hasValidationMessage; }
            set
            {
                if (value == hasValidationMessage)
                    return;

                hasValidationMessage = value;
                base.OnPropertyChanged(() => HasValidationMessage);
            }
        }


        private static string sampleText;
        public string SampleText
        {
            get { return sampleText; }
            set
            {
                if (value == sampleText)
                    return;

                sampleText = value;
                base.OnPropertyChanged(() => SampleText);
            }
        }

        private InlineCollection searchOutput;
        public InlineCollection SearchOutput
        {
            get { return searchOutput; }
            set
            {
                if (value == searchOutput)
                    return;

                searchOutput = value;
                base.OnPropertyChanged(() => SearchOutput);
            }
        }

        private InlineCollection replaceOutput;
        public InlineCollection ReplaceOutput
        {
            get { return replaceOutput; }
            set
            {
                if (value == replaceOutput)
                    return;

                replaceOutput = value;
                base.OnPropertyChanged(() => ReplaceOutput);
            }
        }

        private string replaceOutputText;
        public string ReplaceOutputText
        {
            get { return replaceOutputText; }
            set
            {
                if (value == replaceOutputText)
                    return;

                replaceOutputText = value;

                base.OnPropertyChanged(() => ReplaceOutputText);
            }
        }

        private string replaceErrorText;
        public string ReplaceErrorText
        {
            get { return replaceErrorText; }
            set
            {
                if (value == replaceErrorText)
                    return;

                replaceErrorText = value;

                base.OnPropertyChanged(() => ReplaceErrorText);
            }
        }

        private GrepEngineInitParams InitParameters
        {
            get
            {
                return new GrepEngineInitParams(
                    GrepSettings.Instance.Get<bool>(GrepSettings.Key.ShowLinesInContext),
                    GrepSettings.Instance.Get<int>(GrepSettings.Key.ContextLinesBefore),
                    GrepSettings.Instance.Get<int>(GrepSettings.Key.ContextLinesAfter),
                    GrepSettings.Instance.Get<double>(GrepSettings.Key.FuzzyMatchThreshold),
                    GrepSettings.Instance.Get<bool>(GrepSettings.Key.ShowVerboseMatchCount),
                false);
            }
        }

        private GrepSearchOption SearchOptions
        {
            get
            {
                GrepSearchOption searchOptions = GrepSearchOption.None;
                if (Multiline)
                    searchOptions |= GrepSearchOption.Multiline;
                if (CaseSensitive)
                    searchOptions |= GrepSearchOption.CaseSensitive;
                if (Singleline)
                    searchOptions |= GrepSearchOption.SingleLine;
                if (WholeWord)
                    searchOptions |= GrepSearchOption.WholeWord;
                if (BooleanOperators)
                    searchOptions |= GrepSearchOption.BooleanOperators;

                return searchOptions;
            }
        }

        private async void Search()
        {
            hasMatches = false;
            grepResults.Clear();

            if (string.IsNullOrEmpty(SampleText) || string.IsNullOrEmpty(SearchFor))
            {
                SearchOutput = new Paragraph().Inlines;
                return;
            }

            GrepEnginePlainText engine = new GrepEnginePlainText();
            engine.Initialize(InitParameters, new FileFilter());

            using (Stream inputStream = new MemoryStream(Encoding.Unicode.GetBytes(SampleText)))
            {
                try
                {
                    grepResults = engine.Search(inputStream, "test.txt", SearchFor, TypeOfSearch,
                        SearchOptions, Encoding.Unicode);

                    if (grepResults != null)
                    {
                        using (StringReader reader = new StringReader(SampleText))
                        {
                            foreach (var result in grepResults)
                            {
                                if (!result.HasSearchResults)
                                    result.SearchResults = Utils.GetLinesEx(reader, result.Matches, 0, 0);
                            }
                        }
                    }
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(Resources.MessageBox_IncorrectPattern + ex.Message, 
                        Resources.MessageBox_DnGrep, 
                        MessageBoxButton.OK, MessageBoxImage.Warning,
                        MessageBoxResult.OK, TranslationSource.Instance.FlowDirection);
                }
            }

            SearchResults.Clear();
            SearchResults.AddRangeForTestView(grepResults);
            Paragraph paragraph = new Paragraph();
            if (SearchResults.Count == 1)
            {
                await SearchResults[0].FormattedLines.LoadAsync();
                foreach (FormattedGrepLine line in SearchResults[0].FormattedLines)
                {
                    if (line.IsSectionBreak)
                    {
                        paragraph.Inlines.Add(new Run(horizontalBar)
                        {
                            FontWeight = FontWeights.Light,
                            Foreground = Application.Current.Resources["TreeView.Section.Border"] as Brush,
                        });
                        paragraph.Inlines.Add(new LineBreak());
                    }

                    // Copy children Inline to a temporary array.
                    paragraph.Inlines.AddRange(line.FormattedText.ToList());
                    paragraph.Inlines.Add(new LineBreak());
                    hasMatches = true;
                }
            }
            else
            {
                paragraph.Inlines.Add(new Run(Resources.Test_NoMatchesFound));
            }
            SearchOutput = paragraph.Inlines;
        }

        private void Replace()
        {
            ReplaceErrorText = string.Empty;

            if (string.IsNullOrEmpty(SampleText) || string.IsNullOrEmpty(SearchFor) || !hasMatches)
            {
                ReplaceOutput = new Paragraph().Inlines;
                return;
            }

            string replaceString = ReplaceWith ?? string.Empty;

            GrepEnginePlainText engine = new GrepEnginePlainText();
            engine.Initialize(InitParameters, new FileFilter());

            string replacedString = string.Empty;
            try
            {
                // mark all matches for replace
                if (grepResults.Count > 0)
                {
                    foreach (var match in grepResults[0].Matches)
                    {
                        match.ReplaceMatch = true;
                    }
                }
                else
                {
                    ReplaceOutput = new Paragraph().Inlines;
                    return;
                }

                using (Stream inputStream = new MemoryStream(Encoding.Unicode.GetBytes(SampleText)))
                using (Stream writeStream = new MemoryStream())
                {
                    engine.Replace(inputStream, writeStream, SearchFor, replaceString, TypeOfSearch,
                        SearchOptions, Encoding.Unicode, grepResults[0].Matches);
                    writeStream.Position = 0;
                    using (StreamReader reader = new StreamReader(writeStream))
                    {
                        replacedString = reader.ReadToEnd();
                    }
                }
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(Resources.MessageBox_IncorrectPattern + ex.Message, 
                    Resources.MessageBox_DnGrep, 
                    MessageBoxButton.OK, MessageBoxImage.Warning,
                    MessageBoxResult.OK, TranslationSource.Instance.FlowDirection);
            }
            catch (XmlException)
            {
                ReplaceErrorText = Resources.Test_ReplaceTextIsNotValidXML;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Resources.MessageBox_Error + ex.Message, Resources.MessageBox_DnGrep, 
                    MessageBoxButton.OK, MessageBoxImage.Error,
                    MessageBoxResult.OK, TranslationSource.Instance.FlowDirection);
            }

            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(replacedString));
            ReplaceOutput = paragraph.Inlines;
            ReplaceOutputText = replacedString;
        }
    }
}
