using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CompanionScan.Core;
using UnityEditor.Android;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace CompanionScan.Editor
{
    /// <summary>
    /// Puts the open-source licences and notices into every Android build, for the in-app
    /// licences view (<see cref="LicenseNotices"/>, <c>CompanionScan.UI.LicensesPanel</c>).
    ///
    /// NOTICE.md requires its notices and the licence texts it points to to travel with binary
    /// copies. After Unity generates the Gradle project, this step copies into the APK's assets,
    /// under <see cref="LicenseNotices.AssetFolder"/>:
    /// <list type="bullet">
    /// <item>LICENSE, NOTICE.md and THIRD_PARTY.md from the repository;</item>
    /// <item>the licence and notice files vendored under Assets (TextMesh Pro's Liberation Sans
    /// font licence, NaughtyAttributes, the NuGet packages in Assets/Packages);</item>
    /// <item>the top-level LICENSE, NOTICE and third-party notice files of every package the
    /// project resolves, so they stay current when a package changes.</item>
    /// </list>
    /// Files with identical text are stored once, with every source named at the top. The index
    /// file lists the documents in reading order.
    /// </summary>
    public sealed class LicenseNoticesBuildStep : IPostGenerateGradleAndroidProject
    {
        /// <summary>A document for the licences view: its title, the files it comes from and their text.</summary>
        public sealed class Document
        {
            public Document(string title, string text)
            {
                Title = title;
                Text = text;
            }

            public string Title { get; }
            public string Text { get; }
            public List<string> Sources { get; } = new List<string>();
        }

        private static readonly Regex NoticeFileName = new Regex(
            @"^(licen[cs]e|notice|copying|third[ _-]?party[ _-]?notices?)(\.(md|txt))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // After the Meta XR SDK's OVRGradleGeneration (99999) and AndroidNetworkSecurityConfig (100000).
        public int callbackOrder => 100001;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var documents = Collect(ProjectFolder, RegisteredPackages());
            var count = Write(path, documents);
            Debug.Log($"[Licences] Copied {count} licence and notice documents into the APK's assets/{LicenseNotices.AssetFolder}.");
        }

        /// <summary>The project folder (the parent of Assets).</summary>
        public static string ProjectFolder => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>
        /// Name, version and folder of every package the project resolves. Built-in packages count:
        /// Unity 6 ships URP, the render pipeline core, uGUI and other core packages as built-in, and
        /// their notices go into the APK too. The engine modules have no notice files.
        /// </summary>
        public static IEnumerable<(string name, string version, string folder)> RegisteredPackages()
        {
            return PackageInfo.GetAllRegisteredPackages()
                .Where(package => !string.IsNullOrEmpty(package.resolvedPath))
                .OrderBy(package => package.name, StringComparer.Ordinal)
                .Select(package => (package.name, package.version, package.resolvedPath));
        }

        /// <summary>
        /// The documents of the licences view, in reading order: the repository's own files first,
        /// then the vendored files, then the packages. Missing files are skipped.
        /// </summary>
        public static List<Document> Collect(string projectFolder, IEnumerable<(string name, string version, string folder)> packages)
        {
            var sources = new List<(string label, string path)>
            {
                ("Companion Scan licence (LICENSE)", Path.Combine(projectFolder, "LICENSE")),
                ("Notices (NOTICE.md)", Path.Combine(projectFolder, "NOTICE.md")),
                ("Third-party components (THIRD_PARTY.md)", Path.Combine(projectFolder, "THIRD_PARTY.md")),
                ("Liberation Sans font licence (OFL-1.1)",
                    Path.Combine(projectFolder, "Assets", "TextMesh Pro", "Fonts", "LiberationSans - OFL.txt")),
            };

            sources.AddRange(NoticeFilesIn(Path.Combine(projectFolder, "Assets", "NaughtyAttributes"), "NaughtyAttributes"));

            var nugetFolder = Path.Combine(projectFolder, "Assets", "Packages");
            if (Directory.Exists(nugetFolder))
            {
                foreach (var folder in Directory.GetDirectories(nugetFolder).OrderBy(f => f, StringComparer.Ordinal))
                {
                    sources.AddRange(NoticeFilesIn(folder, Path.GetFileName(folder)));
                }
            }

            foreach (var (name, version, folder) in packages)
            {
                sources.AddRange(NoticeFilesIn(folder, string.IsNullOrEmpty(version) ? name : name + " " + version));
            }

            var documents = new List<Document>();
            var byHash = new Dictionary<string, Document>();
            foreach (var (label, path) in sources)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var text = File.ReadAllText(path);
                var hash = Hash(text);
                if (!byHash.TryGetValue(hash, out var document))
                {
                    document = new Document(label, text);
                    byHash.Add(hash, document);
                    documents.Add(document);
                }

                document.Sources.Add(label);
            }

            return documents;
        }

        /// <summary>
        /// Writes the documents and the index into the Gradle project at <paramref name="unityLibraryPath"/>,
        /// replacing what an earlier build left there. Returns the number of documents written.
        /// </summary>
        public static int Write(string unityLibraryPath, IReadOnlyList<Document> documents)
        {
            var folder = Path.Combine(unityLibraryPath, "src", "main", "assets", LicenseNotices.AssetFolder);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);
            var utf8 = new UTF8Encoding(false);
            var index = new StringBuilder();
            for (var i = 0; i < documents.Count; i++)
            {
                var document = documents[i];
                var fileName = FileNameFor(i, document.Title);
                File.WriteAllText(Path.Combine(folder, fileName), Header(document) + document.Text, utf8);
                var title = document.Sources.Count > 1
                    ? $"{document.Title} and {document.Sources.Count - 1} more"
                    : document.Title;
                index.Append(LicenseNotices.FormatIndexLine(fileName, title)).Append('\n');
            }

            File.WriteAllText(Path.Combine(folder, LicenseNotices.IndexFileName), index.ToString(), utf8);
            return documents.Count;
        }

        /// <summary>A safe, unique asset file name: the position, then the title reduced to plain characters.</summary>
        public static string FileNameFor(int position, string title)
        {
            var plain = Regex.Replace(title ?? string.Empty, "[^A-Za-z0-9._-]+", "-").Trim('-', '.');
            if (plain.Length > 60)
            {
                plain = plain.Substring(0, 60).TrimEnd('-', '.');
            }

            return $"{position:000}-{(plain.Length > 0 ? plain : "document")}.txt";
        }

        private static IEnumerable<(string label, string path)> NoticeFilesIn(string folder, string owner)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return Enumerable.Empty<(string, string)>();
            }

            return Directory.GetFiles(folder)
                .Where(file => NoticeFileName.IsMatch(Path.GetFileName(file)))
                .OrderBy(file => file, StringComparer.Ordinal)
                .Select(file => ($"{owner} ({Path.GetFileName(file)})", file))
                .ToList();
        }

        private static string Header(Document document)
        {
            return document.Sources.Count > 1
                ? "Applies to: " + string.Join("; ", document.Sources) + "\n\n"
                : string.Empty;
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n")));
                return BitConverter.ToString(bytes);
            }
        }
    }
}
