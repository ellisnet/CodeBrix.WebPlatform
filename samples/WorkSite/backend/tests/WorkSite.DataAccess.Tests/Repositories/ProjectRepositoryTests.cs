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

public class ProjectRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly ProjectRepository _projects;
    private readonly DatabaseFixture _database;

    public ProjectRepositoryTests(DatabaseFixture database)
    {
        _database = database;
        _projects = new ProjectRepository(database.DataSource);
    }

    [Fact]
    public async Task CreateAsync_stores_every_value_with_the_client_name()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id, "Cedar & Pine");

        //Act
        var project = await _projects.CreateAsync(user.Id, new ProjectInput
        {
            ClientId = client.Id,
            Name = "Portal",
            Description = "Client portal",
            HourlyRate = 142.5m,
            IsBillable = true,
        }, TestContext.Current.CancellationToken);

        //Assert
        project.ClientId.Should().Be(client.Id);
        project.ClientName.Should().Be("Cedar & Pine");
        project.Name.Should().Be("Portal");
        project.Description.Should().Be("Client portal");
        project.HourlyRate.Should().Be(142.5m);
        project.IsBillable.Should().BeTrue();
        project.IsArchived.Should().BeFalse();
        project.TotalHours.Should().Be(0m);
    }

    [Fact]
    public async Task CreateAsync_stores_a_missing_rate_as_null()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);

        //Act
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, rate: null, billable: false);

        //Assert
        project.HourlyRate.Should().BeNull();
        project.IsBillable.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_orders_by_client_then_project_and_hides_archived_work()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var beta = await TestData.ClientAsync(_database.DataSource, user.Id, "Beta");
        var alpha = await TestData.ClientAsync(_database.DataSource, user.Id, "alpha");
        var gone = await TestData.ClientAsync(_database.DataSource, user.Id, "Gone", archived: true);
        await TestData.ProjectAsync(_database.DataSource, user.Id, beta.Id, "zeta");
        await TestData.ProjectAsync(_database.DataSource, user.Id, beta.Id, "Eta");
        await TestData.ProjectAsync(_database.DataSource, user.Id, alpha.Id, "Theta");
        await TestData.ProjectAsync(_database.DataSource, user.Id, alpha.Id, "Old", archived: true);
        await TestData.ProjectAsync(_database.DataSource, user.Id, gone.Id, "Under archived client");

        //Act
        var active = await _projects.ListAsync(user.Id, null, includeArchived: false, TestContext.Current.CancellationToken);
        var all = await _projects.ListAsync(user.Id, null, includeArchived: true, TestContext.Current.CancellationToken);
        var betaOnly = await _projects.ListAsync(user.Id, beta.Id, includeArchived: false, TestContext.Current.CancellationToken);

        //Assert
        active.Select(p => p.Name).Should().Equal("Theta", "Eta", "zeta");
        all.Select(p => p.Name).Should().Equal("Old", "Theta", "Eta", "zeta", "Under archived client");
        betaOnly.Select(p => p.Name).Should().Equal("Eta", "zeta");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_name_the_client_already_has_ignoring_case()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, "Website");

        //Act
        Func<Task> act = () => TestData.ProjectAsync(_database.DataSource, user.Id, client.Id, "WEBSITE");

        //Assert
        (await act.Should().ThrowAsync<DuplicateValueException>()).Which.Field.Should().Be("name");
    }

    [Fact]
    public async Task CreateAsync_allows_the_same_name_under_another_client()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var first = await TestData.ClientAsync(_database.DataSource, user.Id);
        var second = await TestData.ClientAsync(_database.DataSource, user.Id);
        await TestData.ProjectAsync(_database.DataSource, user.Id, first.Id, "Website");

        //Act
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, second.Id, "Website");

        //Assert
        project.ClientId.Should().Be(second.Id);
    }

    [Fact]
    public async Task CreateAsync_rejects_another_users_client()
    {
        //Arrange
        var owner = await TestData.UserAsync(_database.DataSource);
        var intruder = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, owner.Id);

        //Act
        Func<Task> act = () => TestData.ProjectAsync(_database.DataSource, intruder.Id, client.Id);

        //Assert
        (await act.Should().ThrowAsync<ReferenceNotFoundException>()).Which.Field.Should().Be("clientId");
    }

    [Fact]
    public async Task UpdateAsync_can_move_a_project_to_another_client()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var from = await TestData.ClientAsync(_database.DataSource, user.Id);
        var to = await TestData.ClientAsync(_database.DataSource, user.Id, "Destination");
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, from.Id, "Moving");

        //Act
        var updated = await _projects.UpdateAsync(user.Id, project.Id, new ProjectInput
        {
            ClientId = to.Id,
            Name = "Moved",
            HourlyRate = null,
            IsBillable = false,
            IsArchived = true,
        }, TestContext.Current.CancellationToken);

        //Assert
        updated.ClientId.Should().Be(to.Id);
        updated.ClientName.Should().Be("Destination");
        updated.Name.Should().Be("Moved");
        updated.HourlyRate.Should().BeNull();
        updated.IsBillable.Should().BeFalse();
        updated.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_refuses_a_project_with_entries_and_deletes_one_without()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var used = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        var unused = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        await TestData.EntryAsync(_database.DataSource, user.Id, used.Id, new DateOnly(2026, 11, 2), 1m);

        //Act
        var usedOutcome = await _projects.DeleteAsync(user.Id, used.Id, TestContext.Current.CancellationToken);
        var unusedOutcome = await _projects.DeleteAsync(user.Id, unused.Id, TestContext.Current.CancellationToken);
        var unknownOutcome = await _projects.DeleteAsync(user.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        //Assert
        usedOutcome.Should().Be(DeleteOutcome.InUse);
        unusedOutcome.Should().Be(DeleteOutcome.Deleted);
        unknownOutcome.Should().Be(DeleteOutcome.NotFound);
    }

    [Fact]
    public async Task GetAsync_sums_the_projects_hours()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var client = await TestData.ClientAsync(_database.DataSource, user.Id);
        var project = await TestData.ProjectAsync(_database.DataSource, user.Id, client.Id);
        await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 11, 2), 7.5m);
        await TestData.EntryAsync(_database.DataSource, user.Id, project.Id, new DateOnly(2026, 11, 3), 0.25m);

        //Act
        var read = await _projects.GetAsync(user.Id, project.Id, TestContext.Current.CancellationToken);

        //Assert
        read.TotalHours.Should().Be(7.75m);
    }
}
