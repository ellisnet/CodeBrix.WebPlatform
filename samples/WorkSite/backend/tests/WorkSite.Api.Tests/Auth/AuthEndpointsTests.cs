using System;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.Api.Tests.Infrastructure;
using Xunit;

namespace WorkSite.Api.Tests.Auth;

public class AuthEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task register_creates_the_user_signs_in_and_answers_201()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var username = TestUsers.NewUsername("Reg");

        //Act
        var response = await client.PostAsync("/api/auth/register", new
        {
            firstName = " Ada ",
            lastName = "Lovelace",
            email = username + "@Example.com",
            username,
            password = TestUsers.Password,
        });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await ApiClient.JsonAsync(response);
        Guid.TryParse(user.GetProperty("id").GetString(), out _).Should().BeTrue();
        user.GetProperty("username").GetString().Should().Be(username);
        user.GetProperty("email").GetString().Should().Be(username + "@Example.com");
        user.GetProperty("firstName").GetString().Should().Be("Ada");
        user.GetProperty("lastName").GetString().Should().Be("Lovelace");
        user.GetProperty("createdAt").GetString().Should().EndWith("Z");
        user.TryGetProperty("passwordHash", out _).Should().BeFalse();
        client.Cookie("ws_access").Should().NotBeNullOrEmpty();
        client.Cookie("ws_refresh").Should().NotBeNullOrEmpty();
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task register_validates_every_field()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/auth/register", new
        {
            firstName = "",
            lastName = new string('x', 101),
            email = "not-an-email",
            username = "a b",
            password = "12345",
        });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("title").GetString().Should().Be("One or more fields are invalid.");
        problem.ErrorFields().Should().Equal("email", "firstName", "lastName", "password", "username");
        problem.FirstError("email").Should().Be("Enter a valid email address.");
        problem.FirstError("password").Should().Contain("at least 6");
        client.Cookie("ws_access").Should().BeNull();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("thirty-three-characters-long-name")]
    [InlineData("bad!name")]
    [InlineData("名前名前")]
    public async Task register_rejects_usernames_outside_the_allowed_pattern(string username)
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/auth/register", new
        {
            firstName = "A",
            lastName = "B",
            email = TestUsers.NewUsername() + "@example.com",
            username,
            password = TestUsers.Password,
        });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("username");
    }

    [Fact]
    public async Task register_rejects_a_password_over_two_hundred_characters()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/auth/register", new
        {
            firstName = "A",
            lastName = "B",
            email = TestUsers.NewUsername() + "@example.com",
            username = TestUsers.NewUsername(),
            password = new string('p', 201),
        });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("password");
    }

    [Fact]
    public async Task register_answers_409_for_a_taken_username_or_email_ignoring_case()
    {
        //Arrange
        using var first = _factory.CreateApiClient();
        using var second = _factory.CreateApiClient();
        var existing = await TestUsers.RegisterAsync(first);
        var username = existing.GetProperty("username").GetString();

        //Act
        var response = await second.PostAsync("/api/auth/register", new
        {
            firstName = "A",
            lastName = "B",
            email = existing.GetProperty("email").GetString().ToUpperInvariant(),
            username = username.ToUpperInvariant(),
            password = TestUsers.Password,
        });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
        problem.ErrorFields().Should().Equal("email", "username");
        problem.FirstError("username").Should().Be("That username is already taken.");
    }

    [Fact]
    public async Task login_by_username_or_email_ignoring_case_answers_200_with_cookies()
    {
        //Arrange
        using var registration = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(registration);
        using var byUsername = _factory.CreateApiClient();
        using var byEmail = _factory.CreateApiClient();

        //Act
        var usernameResponse = await byUsername.PostAsync("/api/auth/login",
            new { identifier = user.GetProperty("username").GetString().ToUpperInvariant(), password = TestUsers.Password });
        var emailResponse = await byEmail.PostAsync("/api/auth/login",
            new { identifier = user.GetProperty("email").GetString().ToUpperInvariant(), password = TestUsers.Password });

        //Assert
        usernameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        emailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiClient.JsonAsync(usernameResponse)).GetProperty("id").GetString().Should().Be(user.GetProperty("id").GetString());
        (await ApiClient.JsonAsync(emailResponse)).GetProperty("id").GetString().Should().Be(user.GetProperty("id").GetString());
        byUsername.Cookie("ws_access").Should().NotBeNullOrEmpty();
        byEmail.Cookie("ws_refresh").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task login_with_a_wrong_password_or_unknown_user_gives_the_same_401()
    {
        //Arrange
        using var registration = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(registration);
        using var client = _factory.CreateApiClient();

        //Act
        var wrongPassword = await client.PostAsync("/api/auth/login",
            new { identifier = user.GetProperty("username").GetString(), password = "wrong password" });
        var unknownUser = await client.PostAsync("/api/auth/login",
            new { identifier = TestUsers.NewUsername("nobody"), password = TestUsers.Password });

        //Assert
        var first = await wrongPassword.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid_credentials");
        var second = await unknownUser.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid_credentials");
        first.GetProperty("title").GetString().Should().Be("The username or password is incorrect.");
        second.GetProperty("title").GetString().Should().Be(first.GetProperty("title").GetString());
        first.TryGetProperty("errors", out _).Should().BeFalse();
        client.Cookie("ws_access").Should().BeNull();
    }

    [Fact]
    public async Task login_without_an_identifier_or_password_answers_400()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/auth/login", new { identifier = " ", password = "" });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("identifier", "password");
    }

    [Fact]
    public async Task me_answers_an_empty_401_when_signed_out()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.GetAsync("/api/auth/me");

        //Assert
        await response.ShouldBeEmptyUnauthorizedAsync();
    }

    [Fact]
    public async Task me_rejects_a_tampered_access_token()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var token = client.Cookie("ws_access");
        client.SetCookie("ws_access", token.Substring(0, token.Length - 3) + "abc", "/api");

        //Act
        var response = await client.GetAsync("/api/auth/me");

        //Assert
        await response.ShouldBeEmptyUnauthorizedAsync();
    }

    [Fact]
    public async Task profile_updates_the_name_and_email()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client);
        var email = TestUsers.NewUsername("new") + "@example.com";

        //Act
        var response = await client.PutAsync("/api/auth/profile", new { firstName = "Grace", lastName = "Hopper", email });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiClient.JsonAsync(response);
        body.GetProperty("firstName").GetString().Should().Be("Grace");
        body.GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("username").GetString().Should().Be(user.GetProperty("username").GetString());
    }

    [Fact]
    public async Task profile_answers_409_for_another_users_email_and_400_for_bad_values()
    {
        //Arrange
        using var other = _factory.CreateApiClient();
        var otherUser = await TestUsers.RegisterAsync(other);
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var taken = await client.PutAsync("/api/auth/profile",
            new { firstName = "A", lastName = "B", email = otherUser.GetProperty("email").GetString() });
        var invalid = await client.PutAsync("/api/auth/profile", new { firstName = "", lastName = "B", email = "x@" });

        //Assert
        var conflict = await taken.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
        conflict.ErrorFields().Should().Equal("email");
        var problem = await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("email", "firstName");
    }

    [Fact]
    public async Task profile_keeps_the_users_own_email()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.PutAsync("/api/auth/profile",
            new { firstName = "Same", lastName = "Email", email = user.GetProperty("email").GetString() });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task change_password_rejects_a_wrong_current_password()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);

        //Act
        var response = await client.PostAsync("/api/auth/change-password",
            new { currentPassword = "not it", newPassword = "brand new password" });

        //Assert
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.ErrorFields().Should().Equal("currentPassword");
    }

    [Fact]
    public async Task change_password_keeps_this_session_and_revokes_every_other_one()
    {
        //Arrange
        using var laptop = _factory.CreateApiClient();
        var user = await TestUsers.RegisterAsync(laptop);
        var username = user.GetProperty("username").GetString();
        using var phone = _factory.CreateApiClient();
        (await phone.PostAsync("/api/auth/login", new { identifier = username, password = TestUsers.Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        //Act
        var response = await laptop.PostAsync("/api/auth/change-password",
            new { currentPassword = TestUsers.Password, newPassword = "a whole new password" });

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await laptop.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        await (await phone.GetAsync("/api/auth/me")).ShouldBeEmptyUnauthorizedAsync();
        await (await phone.PostAsync("/api/auth/refresh")).ShouldBeEmptyUnauthorizedAsync();
        using var fresh = _factory.CreateApiClient();
        (await fresh.PostAsync("/api/auth/login", new { identifier = username, password = TestUsers.Password }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fresh.PostAsync("/api/auth/login", new { identifier = username, password = "a whole new password" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task logout_revokes_the_session_at_once()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var oldAccess = client.Cookie("ws_access");
        var oldRefresh = client.Cookie("ws_refresh");

        //Act
        var response = await client.PostAsync("/api/auth/logout");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        client.Cookie("ws_access").Should().BeNull();
        client.Cookie("ws_refresh").Should().BeNull();
        client.SetCookie("ws_access", oldAccess, "/api");
        await (await client.GetAsync("/api/auth/me")).ShouldBeEmptyUnauthorizedAsync();
        client.ClearCookies();
        client.SetCookie("ws_refresh", oldRefresh, "/api/auth");
        await (await client.PostAsync("/api/auth/refresh")).ShouldBeEmptyUnauthorizedAsync();
    }

    [Fact]
    public async Task logout_succeeds_when_nobody_is_signed_in()
    {
        //Arrange
        using var client = _factory.CreateApiClient();

        //Act
        var response = await client.PostAsync("/api/auth/logout");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task logout_with_only_a_refresh_cookie_revokes_that_session()
    {
        //Arrange
        using var client = _factory.CreateApiClient();
        await TestUsers.RegisterAsync(client);
        var refresh = client.Cookie("ws_refresh");
        var access = client.Cookie("ws_access");
        using var other = _factory.CreateApiClient();
        other.SetCookie("ws_refresh", refresh, "/api/auth");

        //Act
        var response = await other.PostAsync("/api/auth/logout");

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = _factory.CreateApiClient();
        check.SetCookie("ws_access", access, "/api");
        await (await check.GetAsync("/api/auth/me")).ShouldBeEmptyUnauthorizedAsync();
    }
}
