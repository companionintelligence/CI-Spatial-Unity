using System;
using System.Collections.Generic;
using CompanionScan.Core;
using TMPro;
using UnityEngine;

namespace CompanionScan.UI
{
    /// <summary>
    /// Open-source licences view: pages through the licences and notices the build step put in
    /// the APK (<see cref="LicenseNotices"/>). Opened from the settings panel; while it is open,
    /// <see cref="XRControllerInput"/> routes A to <see cref="NextPage"/>, X to
    /// <see cref="PreviousPage"/>, B to <see cref="NextDocument"/>, Y to
    /// <see cref="PreviousDocument"/> and Menu to <see cref="Close"/>.
    /// <see cref="SettingsPanel"/> adds this component at run time.
    /// </summary>
    public class LicensesPanel : MonoBehaviour
    {
        private const float PanelWidth = 1000f;
        private const float PanelHeight = 820f;
        private const float PanelDistance = 1.1f;
        private const float PanelScale = 0.001f;
        private const string Hint = "A  next page     X  previous page     B  next document     Y  previous document     Menu  back";

        private static readonly Color BackgroundColor = new(0.05f, 0.05f, 0.1f, 0.95f);
        private static readonly Color TextColor = Color.white;

        private HeadsetPanel _panel;
        private TMP_Text _headingText;
        private TMP_Text _bodyText;
        private List<LicenseNotices.Entry> _documents;
        private List<string> _pages = new List<string> { string.Empty };
        private int _documentIndex;
        private int _pageIndex;
        private int _loadVersion;
        private Action _onClosed;

        /// <summary>Most recently enabled view (the scene normally has one).</summary>
        public static LicensesPanel Active { get; private set; }

        public bool IsOpen { get; private set; }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void OnDestroy()
        {
            _panel?.Destroy();
        }

        private void Update()
        {
            if (IsOpen)
            {
                _panel?.KeepInView();
            }
        }

        /// <summary>Opens the view at the current document; <paramref name="onClosed"/> runs when it closes.</summary>
        public async void Open(Action onClosed = null)
        {
            _onClosed = onClosed;
            IsOpen = true;
            EnsurePanel();
            _panel.Show();

            if (_documents == null)
            {
                Show("Open-source licences", "Loading...");
                _documents = await LicenseNotices.LoadIndexAsync();
                if (!IsOpen)
                {
                    return;
                }
            }

            if (_documents.Count == 0)
            {
                Show("Open-source licences", "The licence files are missing from this build.");
                return;
            }

            await LoadDocumentAsync(_documentIndex);
        }

        /// <summary>Menu: close the view and go back to where it was opened from.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            _panel?.Hide();
            var onClosed = _onClosed;
            _onClosed = null;
            onClosed?.Invoke();
        }

        public void NextPage()
        {
            if (_pageIndex + 1 < _pages.Count)
            {
                _pageIndex++;
                Render();
            }
        }

        public void PreviousPage()
        {
            if (_pageIndex > 0)
            {
                _pageIndex--;
                Render();
            }
        }

        public async void NextDocument()
        {
            if (_documents == null || _documents.Count == 0)
            {
                return;
            }

            await LoadDocumentAsync((_documentIndex + 1) % _documents.Count);
        }

        public async void PreviousDocument()
        {
            if (_documents == null || _documents.Count == 0)
            {
                return;
            }

            await LoadDocumentAsync((_documentIndex + _documents.Count - 1) % _documents.Count);
        }

        private async System.Threading.Tasks.Task LoadDocumentAsync(int index)
        {
            var version = ++_loadVersion;
            _documentIndex = index;
            var entry = _documents[index];
            Show(entry.Title, "Loading...");

            var text = await LicenseNotices.LoadDocumentAsync(entry);
            if (version != _loadVersion)
            {
                return; // a later request replaced this one
            }

            _pages = LicenseNotices.Paginate(text ?? "This document could not be read.");
            _pageIndex = 0;
            Render();
        }

        private void Render()
        {
            if (_documents == null || _documents.Count == 0)
            {
                return;
            }

            var entry = _documents[_documentIndex];
            var heading = $"Document {_documentIndex + 1} of {_documents.Count}, page {_pageIndex + 1} of {_pages.Count}: {entry.Title}";
            Show(heading, _pages[_pageIndex]);
        }

        private void Show(string heading, string body)
        {
            if (_headingText != null)
            {
                _headingText.text = heading;
            }

            if (_bodyText != null)
            {
                _bodyText.text = body;
            }
        }

        private void EnsurePanel()
        {
            if (_panel != null)
            {
                return;
            }

            _panel = new HeadsetPanel("LicensesPanel", PanelWidth, PanelHeight, PanelScale, PanelDistance, BackgroundColor);
            _panel.AddText("Title", "Open-source licences", 34f, 46f, FontStyles.Bold, TextColor);
            // Licence texts contain angle brackets (<https://...>, <package>): no rich text.
            _headingText = _panel.AddText("Heading", string.Empty, 22f, 32f, FontStyles.Italic, TextColor, richText: false);
            _bodyText = _panel.AddText("Page", string.Empty, 20f, 620f, FontStyles.Normal, TextColor,
                wrap: true, richText: false);
            _panel.AddText("Hint", Hint, 20f, 30f, FontStyles.Normal, TextColor);
        }
    }
}
