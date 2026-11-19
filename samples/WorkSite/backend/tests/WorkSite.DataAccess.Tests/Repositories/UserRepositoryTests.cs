using System;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Repositories;

public class UserRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly UserRepository _users;
    private readonly DatabaseFixture _database;

    public UserRepositoryTests(DatabaseFixture database)
    {
        _database = database;
        _users = new UserRepository(database.DataSource);
    }

    [Fact]
    public async Task CreateAsync_returns_the_stored_user()
    {
        //Arrange
        var name = "ada_" + TestData.Unique();

        //Act
        var user = await _users.CreateAsync(new NewUser
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = name + "@example.com",
            Username = name,
            PasswordHash = "hash",
        }, TestContext.Current.CancellationToken);

        //Assert
        user.Id.Should().NotBe(Guid.Empty);
        user.FirstName.Should().Be("Ada");
        user.Username.Should().Be(name);
        user.PasswordHash.Should().Be("hash");
        user.EmailVerified.Should().BeFalse();
        user.IsDemo.Should().BeFalse();
        user.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        (await _users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken)).Email.Should().Be(name + "@example.com");
    }

    [Fact]
    public async Task FindByIdentifierAsync_matches_the_username_or_email_ignoring_case()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource, "Mixed");

        //Act
        var byUsername = await _users.FindByIdentifierAsync(user.Username.ToUpperInvariant(), TestContext.Current.CancellationToken);
        var byEmail = await _users.FindByIdentifierAsync("  " + user.Email.ToUpperInvariant() + " ", TestContext.Current.CancellationToken);
        var nobody = await _users.FindByIdentifierAsync("nobody_" + TestData.Unique(), TestContext.Current.CancellationToken);

        //Assert
        byUsername.Id.Should().Be(user.Id);
        byEmail.Id.Should().Be(user.Id);
        nobody.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_username_taken_in_another_case()
    {
        //Arrange
        var existing = await TestData.UserAsync(_database.DataSource);

        //Act
        Func<Task> act = () => _users.CreateAsync(new NewUser
        {
            FirstName = "B",
            LastName = "B",
            Email = "other_" + TestData.Unique() + "@example.com",
            Username = existing.Username.ToUpperInvariant(),
            PasswordHash = "hash",
        }, TestContext.Current.CancellationToken);

        //Assert
        (await act.Should().ThrowAsync<DuplicateValueException>()).Which.Field.Should().Be("username");
    }

    [Fact]
    public async Task CreateAsync_rejects_an_email_taken_in_another_case()
    {
        //Arrange
        var existing = await TestData.UserAsync(_database.DataSource);

        //Act
        Func<Task> act = () => _users.CreateAsync(new NewUser
        {
            FirstName = "B",
            LastName = "B",
            Email = existing.Email.ToUpperInvariant(),
            Username = "other_" + TestData.Unique(),
            PasswordHash = "hash",
        }, TestContext.Current.CancellationToken);

        //Assert
        (await act.Should().ThrowAsync<DuplicateValueException>()).Which.Field.Should().Be("email");
    }

    [Fact]
    public async Task UsernameExistsAsync_and_EmailExistsAsync_can_ignore_one_user()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var token = TestContext.Current.CancellationToken;

        //Act
        var usernameTaken = await _users.UsernameExistsAsync(user.Username.ToUpperInvariant(), cancellationToken: token);
        var usernameTakenByOthers = await _users.UsernameExistsAsync(user.Username, user.Id, token);
        var emailTaken = await _users.EmailExistsAsync(user.Email, cancellationToken: token);
        var emailTakenByOthers = await _users.EmailExistsAsync(user.Email, user.Id, token);
        var freeEmailTaken = await _users.EmailExistsAsync("free_" + TestData.Unique() + "@example.com", cancellationToken: token);

        //Assert
        usernameTaken.Should().BeTrue();
        usernameTakenByOthers.Should().BeFalse();
        emailTaken.Should().BeTrue();
        emailTakenByOthers.Should().BeFalse();
        freeEmailTaken.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProfileAsync_changes_the_name_and_email()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var email = "changed_" + TestData.Unique() + "@example.com";

        //Act
        var updated = await _users.UpdateProfileAsync(user.Id, "Grace", "Hopper", email, TestContext.Current.CancellationToken);

        //Assert
        updated.FirstName.Should().Be("Grace");
        updated.LastName.Should().Be("Hopper");
        updated.Email.Should().Be(email);
        updated.Username.Should().Be(user.Username);
    }

    [Fact]
    public async Task UpdateProfileAsync_rejects_another_users_email()
    {
        //Arrange
        var first = await TestData.UserAsync(_database.DataSource);
        var second = await TestData.UserAsync(_database.DataSource);

        //Act
        Func<Task> act = () => _users.UpdateProfileAsync(second.Id, "A", "B", first.Email, TestContext.Current.CancellationToken);

        //Assert
        (await act.Should().ThrowAsync<DuplicateValueException>()).Which.Field.Should().Be("email");
    }

    [Fact]
    public async Task UpdateProfileAsync_returns_null_for_an_unknown_user()
        => (await _users.UpdateProfileAsync(Guid.NewGuid(), "A", "B", "x_" + TestData.Unique() + "@example.com",
            TestContext.Current.CancellationToken)).Should().BeNull();

    [Fact]
    public async Task UpdatePasswordHashAsync_replaces_the_hash()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);

        //Act
        var changed = await _users.UpdatePasswordHashAsync(user.Id, "new-hash", TestContext.Current.CancellationToken);

        //Assert
        changed.Should().BeTrue();
        (await _users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken)).PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_id()
        => (await _users.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeNull();
}
