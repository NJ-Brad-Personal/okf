using Devlooped;

namespace Tests;

public class IndexGeneratorTests
{
    static string NewBundle()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "okf-idxgen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(bundle);
        return bundle;
    }

    [Fact]
    public void Creates_missing_index_for_subdirectory()
    {
        var bundle = NewBundle();
        try
        {
            Directory.CreateDirectory(Path.Combine(bundle, "sub"));
            File.WriteAllText(Path.Combine(bundle, "index.md"), """
                # Root

                * [Doc](sub/) - A subdirectory.
                """);
            File.WriteAllText(Path.Combine(bundle, "sub", "other.md"), """
                ---
                type: Reference
                title: Other
                description: Something in sub.
                ---

                Body.
                """);

            var created = IndexGenerator.Generate(bundle);

            Assert.Single(created);
            Assert.Equal("sub/index.md", created[0].Path);

            var subIndexPath = Path.Combine(bundle, "sub", "index.md");
            Assert.True(File.Exists(subIndexPath));
            var written = File.ReadAllText(subIndexPath);
            Assert.Contains("* [Other](other.md) - Something in sub.", written);

            // Root already had an index.md; must be left untouched.
            var rootIndex = File.ReadAllText(Path.Combine(bundle, "index.md"));
            Assert.Contains("# Root", rootIndex);
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void Dry_run_reports_without_writing()
    {
        var bundle = NewBundle();
        try
        {
            File.WriteAllText(Path.Combine(bundle, "only.md"), """
                ---
                type: Reference
                title: Only
                ---

                Body.
                """);

            var created = IndexGenerator.Generate(bundle, dryRun: true);

            Assert.Single(created);
            Assert.Equal("index.md", created[0].Path);
            Assert.False(File.Exists(Path.Combine(bundle, "index.md")));
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void Returns_empty_when_every_directory_already_indexed()
    {
        var bundle = NewBundle();
        try
        {
            File.WriteAllText(Path.Combine(bundle, "index.md"), """
                # Root

                * [Doc](doc.md) - A doc.
                """);
            File.WriteAllText(Path.Combine(bundle, "doc.md"), """
                ---
                type: Reference
                title: Doc
                ---

                Body.
                """);

            var created = IndexGenerator.Generate(bundle);

            Assert.Empty(created);
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void Force_overwrites_existing_index_and_picks_up_renames()
    {
        var bundle = NewBundle();
        try
        {
            File.WriteAllText(Path.Combine(bundle, "index.md"), """
                # Root

                * [Old Name](old.md) - Stale entry for a file that got renamed.
                """);
            File.WriteAllText(Path.Combine(bundle, "new.md"), """
                ---
                type: Reference
                title: New Name
                description: The renamed file.
                ---

                Body.
                """);

            var written = IndexGenerator.Generate(bundle, force: true);

            Assert.Single(written);
            Assert.Equal("index.md", written[0].Path);
            Assert.True(written[0].Existed);

            var rootIndex = File.ReadAllText(Path.Combine(bundle, "index.md"));
            Assert.DoesNotContain("old.md", rootIndex);
            Assert.Contains("* [New Name](new.md) - The renamed file.", rootIndex);
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void Force_dry_run_reports_without_writing()
    {
        var bundle = NewBundle();
        try
        {
            File.WriteAllText(Path.Combine(bundle, "index.md"), """
                # Root

                * [Doc](doc.md) - A doc.
                """);
            File.WriteAllText(Path.Combine(bundle, "doc.md"), """
                ---
                type: Reference
                title: Doc
                ---

                Body.
                """);

            var original = File.ReadAllText(Path.Combine(bundle, "index.md"));

            var written = IndexGenerator.Generate(bundle, force: true, dryRun: true);

            Assert.Single(written);
            Assert.True(written[0].Existed);
            Assert.Equal(original, File.ReadAllText(Path.Combine(bundle, "index.md")));
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void Without_force_existing_index_marked_not_existed_is_never_reported()
    {
        var bundle = NewBundle();
        try
        {
            Directory.CreateDirectory(Path.Combine(bundle, "sub"));
            File.WriteAllText(Path.Combine(bundle, "index.md"), """
                # Root

                * [Sub](sub/) - A subdirectory.
                """);
            File.WriteAllText(Path.Combine(bundle, "sub", "only.md"), """
                ---
                type: Reference
                title: Only
                ---

                Body.
                """);

            var written = IndexGenerator.Generate(bundle);

            Assert.Single(written);
            Assert.Equal("sub/index.md", written[0].Path);
            Assert.False(written[0].Existed);
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void Handles_directory_names_containing_spaces()
    {
        var bundle = NewBundle();
        try
        {
            Directory.CreateDirectory(Path.Combine(bundle, "Renamed Folder"));
            File.WriteAllText(Path.Combine(bundle, "Renamed Folder", "thing.md"), """
                ---
                type: Reference
                title: Thing
                ---

                Body.
                """);

            var written = IndexGenerator.Generate(bundle);

            Assert.Contains(written, w => w.Path == "Renamed Folder/index.md");
            Assert.True(File.Exists(Path.Combine(bundle, "Renamed Folder", "index.md")));
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void Generated_files_pass_bundle_check()
    {
        var bundle = NewBundle();
        try
        {
            Directory.CreateDirectory(Path.Combine(bundle, "tables"));
            File.WriteAllText(Path.Combine(bundle, "tables", "orders.md"), """
                ---
                type: Table
                title: Orders
                description: One row per order.
                ---

                Body.
                """);
            File.WriteAllText(Path.Combine(bundle, "tables", "customers.md"), """
                ---
                type: Table
                title: Customers
                ---

                Body.
                """);

            IndexGenerator.Generate(bundle);

            var result = new BundleChecker(bundle).Check();
            Assert.Empty(result.Errors);
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }
}
