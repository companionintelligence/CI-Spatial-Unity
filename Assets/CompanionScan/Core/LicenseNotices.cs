using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace CompanionScan.Core
{
    /// <summary>
    /// The open-source licences and notices that travel inside the APK.
    ///
    /// NOTICE.md says its notices and the licence texts it points to must go with every binary
    /// copy. The build step <c>CompanionScan.Editor.LicenseNoticesBuildStep</c> copies them into
    /// the APK's assets under <see cref="AssetFolder"/>, with an <see cref="IndexFileName"/> that
    /// lists each document as "file name, tab, title" in reading order. The licences view
    /// (<c>CompanionScan.UI.LicensesPanel</c>) reads them through this class. In the Editor, where
    /// no build has run, it reads LICENSE, NOTICE.md and THIRD_PARTY.md from the project folder.
    /// </summary>
    public static class LicenseNotices
    {
        /// <summary>Folder under the APK's assets (Application.streamingAssetsPath).</summary>
        public const string AssetFolder = "licenses";

        /// <summary>Index of the documents in <see cref="AssetFolder"/>.</summary>
        public const string IndexFileName = "index.txt";

        /// <summary>Characters per line in the licences view.</summary>
        public const int PageColumns = 88;

        /// <summary>Lines per page in the licences view.</summary>
        public const int PageLines = 22;

        /// <summary>One document of the licences view.</summary>
        public readonly struct Entry
        {
            public Entry(string fileName, string title)
            {
                FileName = fileName;
                Title = title;
            }

            public string FileName { get; }
            public string Title { get; }
        }

        /// <summary>A document's index line: file name, tab, title.</summary>
        public static string FormatIndexLine(string fileName, string title)
        {
            return fileName + "\t" + (title ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        /// <summary>Reads an index written with <see cref="FormatIndexLine"/>; skips malformed lines.</summary>
        public static List<Entry> ParseIndex(string text)
        {
            var entries = new List<Entry>();
            if (string.IsNullOrEmpty(text))
            {
                return entries;
            }

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                var tab = line.IndexOf('\t');
                if (tab <= 0)
                {
                    continue;
                }

                var fileName = line.Substring(0, tab).Trim();
                var title = line.Substring(tab + 1).Trim();
                if (!IsSafeFileName(fileName))
                {
                    continue;
                }

                entries.Add(new Entry(fileName, title.Length > 0 ? title : fileName));
            }

            return entries;
        }

        /// <summary>A plain file name inside <see cref="AssetFolder"/>: no folders, no "..".</summary>
        public static bool IsSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || fileName.StartsWith("."))
            {
                return false;
            }

            foreach (var c in fileName)
            {
                if (!(char.IsLetterOrDigit(c) && c < 128) && c != '.' && c != '-' && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Splits <paramref name="text"/> into pages of at most <paramref name="linesPerPage"/>
        /// lines of at most <paramref name="columns"/> characters, wrapping long lines at spaces.
        /// Always returns at least one page.
        /// </summary>
        public static List<string> Paginate(string text, int columns = PageColumns, int linesPerPage = PageLines)
        {
            if (columns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            if (linesPerPage < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(linesPerPage));
            }

            var lines = new List<string>();
            var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            foreach (var rawLine in normalized.Split('\n'))
            {
                var line = rawLine.TrimEnd();
                while (line.Length > columns)
                {
                    var cut = line.LastIndexOf(' ', columns);
                    if (cut <= 0)
                    {
                        lines.Add(line.Substring(0, columns));
                        line = line.Substring(columns);
                    }
                    else
                    {
                        lines.Add(line.Substring(0, cut).TrimEnd());
                        line = line.Substring(cut + 1).TrimStart();
                    }
                }

                lines.Add(line);
            }

            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            var pages = new List<string>();
            var page = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0 && i % linesPerPage == 0)
                {
                    pages.Add(page.ToString());
                    page.Clear();
                }

                if (page.Length > 0)
                {
                    page.Append('\n');
                }

                page.Append(lines[i]);
            }

            pages.Add(page.ToString());
            return pages;
        }

        /// <summary>The documents to show, in order. Empty when none are found.</summary>
        public static async Task<List<Entry>> LoadIndexAsync()
        {
#if UNITY_EDITOR
            await Task.CompletedTask;
            var entries = new List<Entry>();
            foreach (var (fileName, title) in EditorDocuments)
            {
                if (File.Exists(Path.Combine(EditorProjectFolder, fileName)))
                {
                    entries.Add(new Entry(fileName, title));
                }
            }

            return entries;
#else
            return ParseIndex(await ReadAssetAsync(IndexFileName));
#endif
        }

        /// <summary>The text of one document of the index, or null when it cannot be read.</summary>
        public static async Task<string> LoadDocumentAsync(Entry entry)
        {
            if (!IsSafeFileName(entry.FileName))
            {
                return null;
            }
#if UNITY_EDITOR
            await Task.CompletedTask;
            var path = Path.Combine(EditorProjectFolder, entry.FileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
#else
            return await ReadAssetAsync(entry.FileName);
#endif
        }

#if UNITY_EDITOR
        private static readonly (string fileName, string title)[] EditorDocuments =
        {
            ("LICENSE", "Companion Scan licence"),
            ("NOTICE.md", "Notices"),
            ("THIRD_PARTY.md", "Third-party components"),
        };

        private static string EditorProjectFolder => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#else
        // On Android the streaming assets path is a jar: URL inside the APK, readable only through UnityWebRequest.
        private static async Task<string> ReadAssetAsync(string fileName)
        {
            var url = Application.streamingAssetsPath + "/" + AssetFolder + "/" + fileName;
            using (var request = UnityWebRequest.Get(url))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Licences] Could not read {AssetFolder}/{fileName}: {request.error}");
                    return null;
                }

                return request.downloadHandler.text;
            }
        }
#endif
    }
}
