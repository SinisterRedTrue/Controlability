using Controlability.Platform.HidHide;

namespace Controlability.Engine.Tests.HidHide;

public sealed class HiddenJournalTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("controlability-journal-tests").FullName;

    private string JournalPath => Path.Combine(_folder, "nested", "hidden.json");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Missing_file_is_an_empty_journal()
        => new HiddenJournal(JournalPath).Load().Should().BeEmpty();

    [Fact]
    public void Add_creates_the_file_and_a_new_instance_reads_it_back()
    {
        new HiddenJournal(JournalPath).Add(new HiddenEntry("pad1", @"HID\A\1"));

        File.Exists(JournalPath).Should().BeTrue();
        new HiddenJournal(JournalPath).Load().Should().Equal(new HiddenEntry("pad1", @"HID\A\1"));
    }

    [Fact]
    public void Adding_the_same_entry_twice_keeps_one()
    {
        var journal = new HiddenJournal(JournalPath);

        journal.Add(new HiddenEntry("pad1", @"HID\A\1"));
        journal.Add(new HiddenEntry("pad1", @"hid\a\1"));

        journal.Load().Should().ContainSingle();
    }

    [Fact]
    public void Remove_matches_the_path_ignoring_case_and_deletes_the_file_when_empty()
    {
        var journal = new HiddenJournal(JournalPath);
        journal.Add(new HiddenEntry("pad1", @"HID\A\1"));
        journal.Add(new HiddenEntry("pad2", @"HID\B\2"));

        journal.Remove(@"hid\a\1");
        journal.Load().Should().Equal(new HiddenEntry("pad2", @"HID\B\2"));

        journal.Remove(@"HID\B\2");
        File.Exists(JournalPath).Should().BeFalse();
    }

    [Fact]
    public void Removing_an_unknown_path_changes_nothing()
    {
        var journal = new HiddenJournal(JournalPath);
        journal.Add(new HiddenEntry("pad1", @"HID\A\1"));

        journal.Remove(@"HID\other\9");

        journal.Load().Should().ContainSingle();
    }

    [Fact]
    public void A_corrupt_file_reads_as_empty_and_can_be_written_again()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
        File.WriteAllText(JournalPath, "{ this is not json");
        var journal = new HiddenJournal(JournalPath);

        journal.Load().Should().BeEmpty();
        journal.Add(new HiddenEntry("pad1", @"HID\A\1"));

        journal.Load().Should().ContainSingle();
    }

    [Fact]
    public void No_temp_files_are_left_behind()
    {
        var journal = new HiddenJournal(JournalPath);
        journal.Add(new HiddenEntry("pad1", @"HID\A\1"));
        journal.Add(new HiddenEntry("pad2", @"HID\B\2"));

        Directory.GetFiles(Path.GetDirectoryName(JournalPath)!).Select(Path.GetFileName).Should().Equal("hidden.json");
    }
}
