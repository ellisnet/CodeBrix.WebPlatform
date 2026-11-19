using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;
using WorkSite.DataAccess.Tests.Infrastructure;
using Xunit;

namespace WorkSite.DataAccess.Tests.Repositories;

public class SessionRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly SessionRepository _sessions;
    private readonly DatabaseFixture _database;

    public SessionRepositoryTests(DatabaseFixture database)
    {
        _database = database;
        _sessions = new SessionRepository(database.DataSource);
    }

    [Fact]
    public async Task CreateAsync_starts_a_live_session()
    {
        //Arrange
        var user = await TestData.UserAsync(_database.DataSource);
        var hash = NewHash();

        //Act
        var session = await _sessions.CreateAsync(user.Id, hash, DateTime.UtcNow.AddDays(14), TestContext.Current.CancellationToken);

        //Assert
        session.UserId.Should().Be(user.Id);
        session.RefreshTokenHash.Should().Equal(hash);
        session.PreviousRefreshTokenHash.Should().BeNull();
        session.RevokedAt.Should().BeNull();
        (await _sessions.IsLiveAsync(session.Id, user.Id, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task IsLiveAsync_is_false_for_another_user_a_revoked_or_an_expired_session()
    {
        //Arrange
        var token = TestContext.Current.CancellationToken;
        var user = await TestData.UserAsync(_database.DataSource);
        var other = await TestData.UserAsync(_database.DataSource);
        var revoked = await _sessions.CreateAsync(user.Id, NewHash(), DateTime.UtcNow.AddDays(1), token);
        await _sessions.RevokeAsync(revoked.Id, token);
        var expired = await _sessions.CreateAsync(user.Id, NewHash(), DateTime.UtcNow.AddMinutes(-1), token);
        var live = await _sessions.CreateAsync(user.Id, NewHash(), DateTime.UtcNow.AddDays(1), token);

        //Act
        var asOtherUser = await _sessions.IsLiveAsync(live.Id, other.Id, token);
        var whenRevoked = await _sessions.IsLiveAsync(revoked.Id, user.Id, token);
        var whenExpired = await _sessions.IsLiveAsync(expired.Id, user.Id, token);
        var whenUnknown = await _sessions.IsLiveAsync(Guid.NewGuid(), user.Id, token);

        //Assert
        asOtherUser.Should().BeFalse();
        whenRevoked.Should().BeFalse();
        whenExpired.Should().BeFalse();
        whenUnknown.Should().BeFalse();
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_replaces_the_token_and_keeps_the_previous_hash()
    {
        //Arrange
        var token = TestContext.Current.CancellationToken;
        var user = await TestData.UserAsync(_database.DataSource);
        var first = NewHash();
        var second = NewHash();
        var session = await _sessions.CreateAsync(user.Id, first, DateTime.UtcNow.AddDays(1), token);
        var newExpiry = DateTime.UtcNow.AddDays(14);

        //Act
        var result = await _sessions.RotateRefreshTokenAsync(first, second, newExpiry, token);

        //Assert
        result.Outcome.Should().Be(RefreshOutcome.Rotated);
        result.Session.Id.Should().Be(session.Id);
        result.Session.RefreshTokenHash.Should().Equal(second);
        result.Session.PreviousRefreshTokenHash.Should().Equal(first);
        result.Session.LastRefreshedAt.Should().NotBeNull();
        result.Session.ExpiresAt.Should().BeCloseTo(newExpiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_with_a_replaced_token_revokes_the_session()
    {
        //Arrange
        var token = TestContext.Current.CancellationToken;
        var user = await TestData.UserAsync(_database.DataSource);
        var first = NewHash();
        var session = await _sessions.CreateAsync(user.Id, first, DateTime.UtcNow.AddDays(1), token);
        await _sessions.RotateRefreshTokenAsync(first, NewHash(), DateTime.UtcNow.AddDays(14), token);

        //Act
        var reuse = await _sessions.RotateRefreshTokenAsync(first, NewHash(), DateTime.UtcNow.AddDays(14), token);

        //Assert
        reuse.Outcome.Should().Be(RefreshOutcome.Reused);
        reuse.Session.RevokedAt.Should().NotBeNull();
        (await _sessions.IsLiveAsync(session.Id, user.Id, token)).Should().BeFalse();
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_rejects_unknown_revoked_and_expired_tokens()
    {
        //Arrange
        var token = TestContext.Current.CancellationToken;
        var user = await TestData.UserAsync(_database.DataSource);
        var revokedHash = NewHash();
        var revoked = await _sessions.CreateAsync(user.Id, revokedHash, DateTime.UtcNow.AddDays(1), token);
        await _sessions.RevokeAsync(revoked.Id, token);
        var expiredHash = NewHash();
        await _sessions.CreateAsync(user.Id, expiredHash, DateTime.UtcNow.AddSeconds(-5), token);

        //Act
        var unknown = await _sessions.RotateRefreshTokenAsync(NewHash(), NewHash(), DateTime.UtcNow.AddDays(1), token);
        var ofRevoked = await _sessions.RotateRefreshTokenAsync(revokedHash, NewHash(), DateTime.UtcNow.AddDays(1), token);
        var ofExpired = await _sessions.RotateRefreshTokenAsync(expiredHash, NewHash(), DateTime.UtcNow.AddDays(1), token);

        //Assert
        unknown.Outcome.Should().Be(RefreshOutcome.Invalid);
        unknown.Session.Should().BeNull();
        ofRevoked.Outcome.Should().Be(RefreshOutcome.Invalid);
        ofExpired.Outcome.Should().Be(RefreshOutcome.Invalid);
    }

    [Fact]
    public async Task RevokeByRefreshTokenHashAsync_revokes_the_holding_session()
    {
        //Arrange
        var token = TestContext.Current.CancellationToken;
        var user = await TestData.UserAsync(_database.DataSource);
        var hash = NewHash();
        var session = await _sessions.CreateAsync(user.Id, hash, DateTime.UtcNow.AddDays(1), token);

        //Act
        var revoked = await _sessions.RevokeByRefreshTokenHashAsync(hash, token);
        var again = await _sessions.RevokeByRefreshTokenHashAsync(hash, token);

        //Assert
        revoked.Should().BeTrue();
        again.Should().BeFalse();
        (await _sessions.GetAsync(session.Id, token)).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeOtherSessionsAsync_keeps_only_the_named_session()
    {
        //Arrange
        var token = TestContext.Current.CancellationToken;
        var user = await TestData.UserAsync(_database.DataSource);
        var other = await TestData.UserAsync(_database.DataSource);
        var keep = await _sessions.CreateAsync(user.Id, NewHash(), DateTime.UtcNow.AddDays(1), token);
        var drop1 = await _sessions.CreateAsync(user.Id, NewHash(), DateTime.UtcNow.AddDays(1), token);
        var drop2 = await _sessions.CreateAsync(user.Id, NewHash(), DateTime.UtcNow.AddDays(1), token);
        var otherUsers = await _sessions.CreateAsync(other.Id, NewHash(), DateTime.UtcNow.AddDays(1), token);

        //Act
        var count = await _sessions.RevokeOtherSessionsAsync(user.Id, keep.Id, token);

        //Assert
        count.Should().Be(2);
        (await _sessions.IsLiveAsync(keep.Id, user.Id, token)).Should().BeTrue();
        (await _sessions.IsLiveAsync(drop1.Id, user.Id, token)).Should().BeFalse();
        (await _sessions.IsLiveAsync(drop2.Id, user.Id, token)).Should().BeFalse();
        (await _sessions.IsLiveAsync(otherUsers.Id, other.Id, token)).Should().BeTrue();
    }

    private static byte[] NewHash() => SHA256.HashData(Guid.NewGuid().ToByteArray());
}
