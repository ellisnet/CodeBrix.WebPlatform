using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Repositories;

public class ClientRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly ClientRepository _clients;
    private readonly DatabaseFixture _database;

    public ClientRepositoryTests(DatabaseFixture database)
    {
        _database = database;
        _clients = new ClientRepository(database.DataSource);
    }

    [Fact]
    public async Task CreateAsync_stores_every_value()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);

        //Act
        var client = await _clients.CreateAsync(user.Id, new ClientInput
        {
            Name = "Harbor Light",
            ContactName = "Maya",
            ContactEmail = "maya@example.com",
            Notes = "Pays on time.",
            Color = "amber",
        }, TestContext.Current.CancellationToken);

        //Assert
        var read = await _clients.GetAsync(user.Id, client.Id, TestContext.Current.CancellationToken);
        read.Name.Should().Be("Harbor Light");
        read.ContactName.Should().Be("Maya");
        read.ContactEmail.Should().Be("maya@example.com");
        read.Notes.Should().Be("Pays on time.");
        read.Color.Should().Be("amber");
        read.IsArchived.Should().BeFalse();
        read.ProjectCount.Should().Be(0);
        read.TotalHours.Should().Be(0m);
    }

    [Fact]
    public async Task CreateAsync_stores_missing_optional_text_as_empty()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);

        //Act
        var client = await _clients.CreateAsync(user.Id, new ClientInput
        {
            Name = "Bare",
            ContactName = null,
            ContactEmail = null,
            Notes = null,
            Color = "slate",
        }, TestContext.Current.CancellationToken);

        //Assert
        client.ContactName.Should().Be("");
        client.ContactEmail.Should().Be("");
        client.Notes.Should().Be("");
    }

    [Fact]
    public async Task ListAsync_orders_by_name_and_hides_archived_clients_unless_asked()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        await TestData.ClientAsync(_database.DataSource, user.Id, "charlie");
        await TestData.ClientAsync(_database.DataSource, user.Id, "Alpha");
        await TestData.ClientAsync(_database.DataSource, user.Id, "bravo", archived: true);

        //Act
        var active = await _clients.ListAsync(user.Id, includeArchived: false, TestContext.Current.CancellationToken);
        var all = await _clients.ListAsync(user.Id, includeArchived: true, TestContext.Current.CancellationToken);

        //Assert
        active.Select(c => c.Name).Should().Equal("Alpha", "charlie");
        all.Select(c => c.Name).Should().Equal("Alpha", "bravo", "charlie");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_name_the_user_already_has_ignoring_case()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        await TestData.ClientAsync(_database.DataSource, user.Id, "Same Name");

        //Act
        Func<Task> act = () => TestData.ClientAsync(_database.DataSource, user.Id, "SAME name");

        //Assert
        (await act.Should().ThrowAsync<DuplicateValueException>()).Which.Field.Should().Be("name");
    }

    [Fact]
    public async Task CreateAsync_allows_a_name_another_user_has()
    {
        //Arrange
        var first = await TestData.UserAsync(_database.DataSource);
        var second = await TestData.UserAsync(_database.DataSource);
        await TestData.ClientAsync(_database.DataSource, first.Id, "Shared Name");

        //Act
        var client = await TestData.ClientAsync(_database.DataSource, second.Id, "Shared Name");

        //Assert
        client.Name.Should().Be("Shared Name");
    }

    [Fact]
    public async Task UpdateAsync_replaces_the_values()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id, "Before");

        //Act
        var updated = await _clients.UpdateAsync(user.Id, client.Id, new ClientInput
        {
            Name = "After",
            Notes = "Changed",
            Color = "violet",
            IsArchived = true,
        }, TestContext.Current.CancellationToken);

        //Assert
        updated.Name.Should().Be("After");
        updated.Notes.Should().Be("Changed");
        updated.Color.Should().Be("violet");
        updated.IsArchived.Should().BeTrue();
        updated.CreatedAt.Should().Be(client.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_unknown_client()
        => (await _clients.UpdateAsync((await TestData.UserAsync(_database.DataSource)).Id, Guid.NewGuid(),
            new ClientInput { Name = "X" }, TestContext.Current.CancellationToken)).Should().BeNull();

    [Fact]
    public async Task GetAsync_counts_projects_and_sums_hours()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var first = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        var second = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, archived: true);
        await TestData.EntryAsync(_database.DataSource, user.Id, first.Id, new DateOnly(2026, 11, 2), 2.5m);
        await TestData.EntryAsync(_database.DataSource, user.Id, second.Id, new DateOnly(2026, 11, 3), 1.25m);

        //Act
        var read = await _clients.GetAsync(user.Id, client.Id, TestContext.Current.CancellationToken);

        //Assert
        read.ProjectCount.Should().Be(2);
        read.TotalHours.Should().Be(3.75m);
    }

    [Fact]
    public async Task DeleteAsync_deletes_a_client_without_projects()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);

        //Act
        var outcome = await _clients.DeleteAsync(user.Id, client.Id, TestContext.Current.CancellationToken);

        //Assert
        outcome.Should().Be(DeleteOutcome.Deleted);
        (await _clients.GetAsync(user.Id, client.Id, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_refuses_a_client_with_projects()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);

        //Act
        var outcome = await _clients.DeleteAsync(user.Id, client.Id, TestContext.Current.CancellationToken);

        //Assert
        outcome.Should().Be(DeleteOutcome.InUse);
        (await _clients.GetAsync(user.Id, client.Id, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_reports_an_unknown_client()
        => (await _clients.DeleteAsync((await TestData.UserAsync(_database.DataSource)).Id, Guid.NewGuid(),
            TestContext.Current.CancellationToken)).Should().Be(DeleteOutcome.NotFound);
}
