using System.IO;
using System.Linq;
using System.Text;
using CompanionScan.Core;
using CompanionScan.Editor;
using NUnit.Framework;

namespace CompanionScan.Tests.Editor
{
    /// <summary>
    /// NOTICE.md says its notices and the licence texts it points to must travel with the APK; the
    /// build step copies them in and the in-app licences view pages through them (V14).
    /// </summary>
    public class LicenseNoticesTests
    {
        private string _tempFolder;

        [SetUp]
        public void SetUp()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "CompanionScanLicenseTests-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }

        [Test]
        public void Collect_IncludesTheRepositoryNoticesFirst()
        {
            var documents = LicenseNoticesBuildStep.Collect(LicenseNoticesBuildStep.ProjectFolder, LicenseNoticesBuildStep.RegisteredPackages());
            var titles = documents.Select(d => d.Title).ToList();

            Assert.AreEqual("Companion Scan licence (LICENSE)", titles[0]);
            Assert.AreEqual("Notices (NOTICE.md)", titles[1]);
            Assert.AreEqual("Third-party components (THIRD_PARTY.md)", titles[2]);
            CollectionAssert.Contains(titles, "Liberation Sans font licence (OFL-1.1)");
            CollectionAssert.Contains(titles, "NaughtyAttributes (LICENSE.md)");
        }

        [Test]
        public void Collect_IncludesThePackageNoticesThatNoticeMdPointsTo()
        {
            var sources = LicenseNoticesBuildStep
                .Collect(LicenseNoticesBuildStep.ProjectFolder, LicenseNoticesBuildStep.RegisteredPackages())
                .SelectMany(d => d.Sources)
                .ToList();

            // NOTICE.md section 3.1 names these files for the APK's components. URP, the render
            // pipeline core and uGUI are built-in packages in Unity 6.
            foreach (var (owner, file) in new[]
                     {
                         ("com.meta.xr.sdk.core ", "(NOTICE.txt)"),
                         ("com.meta.xr.mrutilitykit ", "(NOTICE.txt)"),
                         ("com.meta.xr.sdk.audio ", "(THIRD_PARTY_NOTICES.txt)"),
                         ("com.unity.xr.openxr ", "(Third Party Notices.md)"),
                         ("com.unity.nuget.newtonsoft-json ", "(Third Party Notices.md)"),
                         ("com.unity.render-pipelines.core ", "(THIRD PARTY NOTICES.md)"),
                         ("com.unity.render-pipelines.universal ", "(Third Party Notices.md)"),
                         ("com.unity.ugui ", "(LICENSE.md)"),
                         ("org.khronos.unitygltf ", "(LICENSE)"),
                         ("System.Text.Json.9.0.5 ", "(THIRD-PARTY-NOTICES.TXT)"),
                     })
            {
                Assert.IsTrue(sources.Any(s => s.StartsWith(owner) && s.EndsWith(file)), owner + file + " is missing");
            }
        }

        [Test]
        public void Collect_StoresIdenticalFilesOnce()
        {
            var documents = LicenseNoticesBuildStep.Collect(LicenseNoticesBuildStep.ProjectFolder, LicenseNoticesBuildStep.RegisteredPackages());
            var netLicense = documents.Single(d => d.Sources.Contains("System.Text.Json.9.0.5 (LICENSE.TXT)"));

            Assert.Greater(netLicense.Sources.Count, 1, "the .NET packages share one MIT licence text");
            Assert.AreEqual(documents.Count, documents.Select(d => d.Text.Replace("\r\n", "\n")).Distinct().Count());
        }

        [Test]
        public void Write_PutsTheDocumentsAndAnIndexIntoTheApkAssets()
        {
            var documents = LicenseNoticesBuildStep.Collect(LicenseNoticesBuildStep.ProjectFolder, LicenseNoticesBuildStep.RegisteredPackages());
            var stale = Path.Combine(_tempFolder, "src", "main", "assets", LicenseNotices.AssetFolder, "999-stale.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(stale));
            File.WriteAllText(stale, "left over from an earlier build");

            var written = LicenseNoticesBuildStep.Write(_tempFolder, documents);

            var folder = Path.Combine(_tempFolder, "src", "main", "assets", LicenseNotices.AssetFolder);
            var index = LicenseNotices.ParseIndex(File.ReadAllText(Path.Combine(folder, LicenseNotices.IndexFileName), Encoding.UTF8));
            Assert.AreEqual(documents.Count, written);
            Assert.AreEqual(documents.Count, index.Count);
            Assert.IsFalse(File.Exists(stale), "files from an earlier build must not stay in the APK");
            Assert.AreEqual(index.Count, index.Select(e => e.FileName).Distinct().Count());
            foreach (var entry in index)
            {
                Assert.IsTrue(LicenseNotices.IsSafeFileName(entry.FileName), entry.FileName);
                Assert.IsTrue(File.Exists(Path.Combine(folder, entry.FileName)), entry.FileName);
            }

            StringAssert.Contains("PolyForm Noncommercial License 1.0.0", File.ReadAllText(Path.Combine(folder, index[0].FileName)));
            Assert.AreEqual(File.ReadAllText(Path.Combine(LicenseNoticesBuildStep.ProjectFolder, "NOTICE.md")),
                File.ReadAllText(Path.Combine(folder, index[1].FileName)));
        }

        [Test]
        public void ParseIndex_ReadsWhatTheBuildStepWrites_AndSkipsUnsafeNames()
        {
            var text = LicenseNotices.FormatIndexLine("000-LICENSE.txt", "Companion Scan licence (LICENSE)") + "\n" +
                       "../outside.txt\tescape\n" +
                       "sub/dir.txt\tfolder\n" +
                       "no-tab-line\n" +
                       LicenseNotices.FormatIndexLine("001-NOTICE.md.txt", "Notices\twith a tab") + "\r\n";

            var entries = LicenseNotices.ParseIndex(text);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("000-LICENSE.txt", entries[0].FileName);
            Assert.AreEqual("Companion Scan licence (LICENSE)", entries[0].Title);
            Assert.AreEqual("Notices with a tab", entries[1].Title);
        }

        [Test]
        public void FileNameFor_IsSafeAndKeepsTheOrder()
        {
            Assert.AreEqual("007-com.meta.xr.sdk.core-209.0.0-NOTICE.txt.txt",
                LicenseNoticesBuildStep.FileNameFor(7, "com.meta.xr.sdk.core 209.0.0 (NOTICE.txt)"));
            Assert.IsTrue(LicenseNotices.IsSafeFileName(LicenseNoticesBuildStep.FileNameFor(12, "../../etc/passwd")));
            Assert.AreEqual("003-document.txt", LicenseNoticesBuildStep.FileNameFor(3, "()"));
        }

        [Test]
        public void Paginate_WrapsLongLinesAtSpacesAndFillsPages()
        {
            var pages = LicenseNotices.Paginate("one two three four\n\nfive", columns: 9, linesPerPage: 2);

            CollectionAssert.AreEqual(new[] { "one two\nthree", "four\n", "five" }, pages);
        }

        [Test]
        public void Paginate_BreaksWordsLongerThanALine()
        {
            var pages = LicenseNotices.Paginate("abcdefghij", columns: 4, linesPerPage: 10);

            CollectionAssert.AreEqual(new[] { "abcd\nefgh\nij" }, pages);
        }

        [Test]
        public void Paginate_AlwaysReturnsAPage()
        {
            CollectionAssert.AreEqual(new[] { string.Empty }, LicenseNotices.Paginate(null));
            CollectionAssert.AreEqual(new[] { string.Empty }, LicenseNotices.Paginate("\r\n\n"));
        }

        [Test]
        public void Paginate_FitsTheLicencesView()
        {
            var notice = File.ReadAllText(Path.Combine(LicenseNoticesBuildStep.ProjectFolder, "NOTICE.md"));

            foreach (var page in LicenseNotices.Paginate(notice))
            {
                var lines = page.Split('\n');
                Assert.LessOrEqual(lines.Length, LicenseNotices.PageLines);
                Assert.IsTrue(lines.All(l => l.Length <= LicenseNotices.PageColumns));
            }
        }

        [Test]
        public void LoadIndex_InTheEditor_ReadsTheRepositoryFiles()
        {
            var entries = LicenseNotices.LoadIndexAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "LICENSE", "NOTICE.md", "THIRD_PARTY.md" }, entries.Select(e => e.FileName).ToArray());
            StringAssert.Contains("PolyForm Noncommercial", LicenseNotices.LoadDocumentAsync(entries[0]).GetAwaiter().GetResult());
        }
    }
}
